using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class VerificationMailer
{
    public static bool Enabled(IConfiguration config) => config.GetValue<bool>("EmailVerification:Enabled");
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.ToUpperInvariant())));
    public static async Task<bool> IsVerifiedAsync(ApplicationDbContext db, AppUser user, CancellationToken ct) =>
        await db.VerifiedEmails.AnyAsync(v => v.UserId == user.Id && v.Email == user.Username, ct);

    public static async Task SendAsync(ApplicationDbContext db, IConfiguration config, IWebHostEnvironment environment, AppUser user, CancellationToken ct)
    {
        var host = config["PasswordReset:Smtp:Host"];
        var sender = config["PasswordReset:Smtp:Sender"];
        var username = config["PasswordReset:Smtp:Username"];
        var password = config["PasswordReset:Smtp:Password"];
        var baseUrl = config["PasswordReset:BaseUrl"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) ||
            !(origin.Scheme == Uri.UriSchemeHttps || (environment.IsDevelopment() && origin.IsLoopback && origin.Scheme == Uri.UriSchemeHttp)) ||
            !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) || string.IsNullOrWhiteSpace(user.Username))
            throw new InvalidOperationException("Email delivery is not configured.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.EmailVerifications.Add(new EmailVerification { TokenHash = Hash(token), UserId = user.Id, Email = user.Username, ExpiresUtc = DateTime.UtcNow.AddHours(24) });
        await db.SaveChangesAsync(ct);
        var link = baseUrl!.TrimEnd('/') + "/VerifyEmail?token=" + token;
        using var message = new MailMessage(sender, user.Username, "Verify your Tap, Task, Done email", $"Confirm your email using this link. It expires in 24 hours and can be used once.\n\n{link}\n\nIf you did not create this account, ignore this email.");
        using var smtp = new SmtpClient(host, int.TryParse(config["PasswordReset:Smtp:Port"], out var port) ? port : 587)
        { EnableSsl = true, Credentials = new NetworkCredential(username, password), Timeout = 10000 };
        await smtp.SendMailAsync(message, ct);
    }
}
