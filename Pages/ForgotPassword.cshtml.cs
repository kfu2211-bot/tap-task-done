using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[EnableRateLimiting("PasswordResetLimit")]
public class ForgotPasswordModel(ApplicationDbContext db, IConfiguration config, IWebHostEnvironment environment, ILogger<ForgotPasswordModel> logger) : PageModel
{
    [BindProperty] public string? Email { get; set; }
    public bool Submitted { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var host = config["PasswordReset:Smtp:Host"];
        var sender = config["PasswordReset:Smtp:Sender"];
        var username = config["PasswordReset:Smtp:Username"];
        var password = config["PasswordReset:Smtp:Password"];
        var baseUrl = config["PasswordReset:BaseUrl"];
        var validUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) &&
            (origin.Scheme == Uri.UriSchemeHttps || (environment.IsDevelopment() && origin.IsLoopback && origin.Scheme == Uri.UriSchemeHttp)) &&
            string.IsNullOrEmpty(origin.UserInfo) && string.IsNullOrEmpty(origin.Query) && string.IsNullOrEmpty(origin.Fragment);
        if (!VerificationMailer.Enabled(config) || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || !validUrl)
        {
            ErrorMessage = "Password reset is not configured yet. Please try again later.";
            return Page();
        }
        var email = (Email ?? "").Trim();
        if (email.Length == 0 || email.Length > 45 || !new EmailAddressAttribute().IsValid(email))
        {
            ErrorMessage = "Please enter a valid email address.";
            return Page();
        }
        try
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == email, ct);
            if (user is not null && VerificationMailer.Enabled(config) && await VerificationMailer.IsVerifiedAsync(db, user, ct))
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
                db.PasswordResetRequests.Add(new PasswordResetRequest { TokenHash = hash, UserId = user.Id, ExpiresUtc = DateTime.UtcNow.AddMinutes(30) });
                await db.SaveChangesAsync(ct);
                var link = baseUrl!.TrimEnd('/') + "/ResetPassword?token=" + token;
                using var message = new MailMessage(sender!, email, "Reset your Tap, Task, Done password", $"Use this link to reset your password. It expires in 30 minutes and can be used once.\n\n{link}\n\nIf you did not request this, ignore this email.");
                using var smtp = new SmtpClient(host!, int.TryParse(config["PasswordReset:Smtp:Port"], out var port) ? port : 587)
                { EnableSsl = true, Credentials = new NetworkCredential(username, password), Timeout = 10000 };
                await smtp.SendMailAsync(message, ct);
            }
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or SmtpException or InvalidOperationException or FormatException)
        {
            // Do not include recipient, token or reset URL in logs or public responses.
            logger.LogWarning("Password reset request could not be processed ({ErrorType}).", ex.GetType().Name);
        }
        Submitted = true;
        return Page();
    }
}
