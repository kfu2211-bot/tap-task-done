using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MimeKit;

namespace tts.Data;

// The safe reason contains no provider response, credential, recipient or email body.
public sealed class GmailDeliveryException(string reason) : System.Net.Mail.SmtpException("Gmail API delivery failed: " + reason)
{
    public string Reason { get; } = reason;
}

public sealed class GmailApiMailer(HttpClient client)
{
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? cachedToken, cachedKey;
    private DateTime tokenExpiresUtc;
    private sealed record Settings(string Sender, string ClientId, string ClientSecret, string RefreshToken);

    private static Settings ReadSettings(IConfiguration config)
    {
        var sender = config["EmailDelivery:Gmail:Sender"]?.Trim();
        var id = config["EmailDelivery:Gmail:ClientId"]?.Trim();
        var secret = config["EmailDelivery:Gmail:ClientSecret"];
        var refresh = config["EmailDelivery:Gmail:RefreshToken"];
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(refresh))
            throw new GmailDeliveryException("gmail_configuration_missing");
        if (sender.Contains('\r') || sender.Contains('\n') || !MailboxAddress.TryParse(sender, out var mailbox) || mailbox.Address != sender)
            throw new GmailDeliveryException("gmail_sender_invalid");
        return new Settings(mailbox.Address.ToLowerInvariant(), id, secret, refresh);
    }
    public async Task SendAsync(IConfiguration config, string recipient, string subject, string text, CancellationToken ct, string? html = null)
    {
        var settings = ReadSettings(config);
        if (recipient.Contains('\r') || recipient.Contains('\n') || !MailboxAddress.TryParse(recipient, out var to) || to.Address != recipient || subject.Contains('\r') || subject.Contains('\n'))
            throw new GmailDeliveryException("gmail_message_invalid");
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Tap, Task, Done", settings.Sender)); mime.To.Add(to);
        mime.Subject = subject; mime.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();
        using var memory = new MemoryStream();
        await mime.WriteToAsync(FormatOptions.Default, memory, ct);
        var raw = Convert.ToBase64String(memory.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings))));
        try
        {
            string? rejectedToken = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var accessToken = await AccessTokenAsync(settings, key, rejectedToken, ct);
                // Addressing this exact user prevents silently sending from a different authorized mailbox.
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/" + Uri.EscapeDataString(settings.Sender) + "/messages/send");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = JsonContent.Create(new { raw });
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { rejectedToken = accessToken; continue; }
                if (!response.IsSuccessStatusCode) throw new GmailDeliveryException(await FailureReasonAsync(response, false, ct));
                // Gmail's accepted message ID confirms acceptance, not arrival in the recipient's inbox.
                using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (result.RootElement.ValueKind != JsonValueKind.Object || !result.RootElement.TryGetProperty("id", out var messageId) || messageId.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(messageId.GetString())) throw new GmailDeliveryException("gmail_invalid_response");
                return;
            }
            throw new GmailDeliveryException("gmail_unauthorized");
        }
        catch (HttpRequestException) { throw new GmailDeliveryException("gmail_network_error"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new GmailDeliveryException("gmail_timeout"); }
        catch (JsonException) { throw new GmailDeliveryException("gmail_invalid_response"); }
    }
    private async Task<string> AccessTokenAsync(Settings settings, string key, string? rejectedToken, CancellationToken ct)
    {
        await tokenLock.WaitAsync(ct);
        try
        {
            if (cachedKey == key && cachedToken is not null && cachedToken != rejectedToken && tokenExpiresUtc > DateTime.UtcNow.AddSeconds(60)) return cachedToken;
            cachedToken = null; cachedKey = null;
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId, ["client_secret"] = settings.ClientSecret,
                ["refresh_token"] = settings.RefreshToken, ["grant_type"] = "refresh_token"
            });
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new GmailDeliveryException(await FailureReasonAsync(response, true, ct));
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()) ||
                !root.TryGetProperty("expires_in", out var expiry) || expiry.ValueKind != JsonValueKind.Number || !expiry.TryGetInt32(out var seconds) || seconds <= 0 ||
                !root.TryGetProperty("token_type", out var type) || type.ValueKind != JsonValueKind.String || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase))
                throw new GmailDeliveryException("gmail_invalid_token_response");
            cachedToken = token.GetString(); cachedKey = key; tokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Min(seconds, 86400));
            return cachedToken!;
        }
        finally { tokenLock.Release(); }
    }
    private static async Task<string> FailureReasonAsync(HttpResponseMessage response, bool oauth, CancellationToken ct)
    {
        // Use only known categories; never propagate arbitrary Google response text into logs/UI.
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error))
            {
                if (oauth && error.ValueKind == JsonValueKind.String)
                    return error.GetString() switch { "invalid_grant" => "gmail_authorization_expired_or_revoked", "invalid_client" => "gmail_client_credentials_invalid", "unauthorized_client" => "gmail_client_not_authorized", _ => "gmail_token_request_failed" };
                if (!oauth && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                    foreach (var entry in errors.EnumerateArray())
                        if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String && reason.GetString() is "accessNotConfigured" or "serviceDisabled") return "gmail_api_not_enabled";
            }
        }
        catch (JsonException) { }
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "gmail_unauthorized", HttpStatusCode.Forbidden => "gmail_sender_or_scope_denied",
            HttpStatusCode.TooManyRequests => "gmail_rate_limited", _ => oauth ? "gmail_token_request_failed" : "gmail_send_rejected"
        };
    }
}
