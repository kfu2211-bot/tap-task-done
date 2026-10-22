using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[EnableRateLimiting("PasswordResetLimit")]
public class VerifyEmailModel(ApplicationDbContext db, IConfiguration config, IWebHostEnvironment environment, ILogger<VerifyEmailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Token { get; set; }
    [BindProperty] public string? Email { get; set; }
    public bool Completed { get; set; }
    public bool Sent { get; set; }
    public string? ErrorMessage { get; set; }
    public void OnGet() { Protect(); }
    private void Protect() { Response.Headers.CacheControl = "no-store"; Response.Headers["Referrer-Policy"] = "no-referrer"; }

    public async Task<IActionResult> OnPostConfirmAsync(CancellationToken ct)
    {
        Protect();
        if (Token is not { Length: 64 } || !Token.All(Uri.IsHexDigit)) { ErrorMessage = "This verification link is invalid or expired."; return Page(); }
        try
        {
            var hash = VerificationMailer.Hash(Token);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var request = await db.EmailVerifications.AsNoTracking().SingleOrDefaultAsync(v => v.TokenHash == hash && v.ExpiresUtc > DateTime.UtcNow, ct);
            if (request is null || await db.EmailVerifications.Where(v => v.TokenHash == hash && v.ExpiresUtc > DateTime.UtcNow).ExecuteDeleteAsync(ct) != 1)
            { ErrorMessage = "This verification link is invalid or expired."; return Page(); }
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, ct);
            if (user is null || !string.Equals(user.Username, request.Email, StringComparison.OrdinalIgnoreCase))
            { ErrorMessage = "This verification link is invalid or expired."; return Page(); }
            var verified = await db.VerifiedEmails.SingleOrDefaultAsync(v => v.UserId == user.Id, ct);
            if (verified is null) db.VerifiedEmails.Add(new VerifiedEmail { UserId = user.Id, Email = user.Username!, VerifiedUtc = DateTime.UtcNow });
            else { verified.Email = user.Username!; verified.VerifiedUtc = DateTime.UtcNow; }
            await db.SaveChangesAsync(ct);
            await db.EmailVerifications.Where(v => v.UserId == user.Id).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            Token = null;
            Completed = true;
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning("Email verification failed ({ErrorType}).", ex.GetType().Name); ErrorMessage = "Unable to verify your email. Please try again later."; }
        return Page();
    }

    public async Task<IActionResult> OnPostResendAsync(CancellationToken ct)
    {
        Protect(); Token = null;
        var email = (Email ?? "").Trim();
        if (!VerificationMailer.Enabled(config)) { ErrorMessage = "Email verification is not configured yet."; return Page(); }
        if (email.Length == 0 || email.Length > 45 || !new EmailAddressAttribute().IsValid(email)) { ErrorMessage = "Enter a valid email address."; return Page(); }
        try
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == email, ct);
            if (user is not null && !await VerificationMailer.IsVerifiedAsync(db, user, ct))
                await VerificationMailer.SendAsync(db, config, environment, user, ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException or System.Net.Mail.SmtpException or FormatException)
        { logger.LogWarning("Verification request failed ({ErrorType}).", ex.GetType().Name); }
        Sent = true;
        return Page();
    }
}
