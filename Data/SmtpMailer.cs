using MailKit.Security;
using MimeKit;

namespace tts.Data;

public static class SmtpMailer
{
    private static readonly GmailApiMailer Gmail = new(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 65536 });
    public static string Diagnostic(Exception ex) => ex is GmailDeliveryException gmail ? gmail.Reason : ex.InnerException is TimeoutException ? "smtp_timeout" : "email_delivery_failed";
    public static async Task SendAsync(IConfiguration config, string recipient, string subject, string text, CancellationToken ct, string? html = null)
    {
        var provider = config["EmailDelivery:Provider"] ?? "Smtp";
        if (string.Equals(provider, "GmailApi", StringComparison.OrdinalIgnoreCase))
        { await Gmail.SendAsync(config, recipient, subject, text, ct, html); return; }
        if (!string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("EmailDelivery:Provider must be GmailApi or Smtp.");
        var host = config["PasswordReset:Smtp:Host"];
        var sender = config["PasswordReset:Smtp:Sender"];
        var username = config["PasswordReset:Smtp:Username"];
        var password = config["PasswordReset:Smtp:Password"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Email delivery is not configured.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(sender));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();
        var port = int.TryParse(config["PasswordReset:Smtp:Port"], out var configuredPort) ? configuredPort : 465;
        using var smtp = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
        try
        {
            await smtp.ConnectAsync(host, port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(username, password, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is MailKit.ServiceNotConnectedException or MailKit.ServiceNotAuthenticatedException or MailKit.Security.AuthenticationException or MailKit.Net.Smtp.SmtpCommandException or MailKit.Net.Smtp.SmtpProtocolException or MailKit.Security.SslHandshakeException or System.IO.IOException or System.Net.Sockets.SocketException or TimeoutException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new System.Net.Mail.SmtpException("Email delivery failed.", ex);
        }
    }
}
