using MailKit.Security;
using MimeKit;

namespace tts.Data;

public static class SmtpMailer
{
    public static async Task SendAsync(IConfiguration config, string recipient, string subject, string text, CancellationToken ct)
    {
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
        message.Body = new TextPart("plain") { Text = text };
        var port = int.TryParse(config["PasswordReset:Smtp:Port"], out var configuredPort) ? configuredPort : 465;
        using var smtp = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
        try
        {
            await smtp.ConnectAsync(host, port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(username, password, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is MailKit.ServiceNotConnectedException or MailKit.ServiceNotAuthenticatedException or MailKit.Security.AuthenticationException or MailKit.Net.Smtp.SmtpCommandException or MailKit.Net.Smtp.SmtpProtocolException or MailKit.Security.SslHandshakeException or System.IO.IOException or System.Net.Sockets.SocketException)
        {
            throw new System.Net.Mail.SmtpException("Email delivery failed.", ex);
        }
    }
}
