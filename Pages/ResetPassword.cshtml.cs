using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[EnableRateLimiting("PasswordResetLimit")]
public class ResetPasswordModel(ApplicationDbContext db, AccountStore accounts, IConfiguration config, ILogger<ResetPasswordModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Token { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? ConfirmPassword { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Completed { get; set; }
    private string? TokenHash() => Token is { Length: 64 } && Token.All(Uri.IsHexDigit) ? Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Token.ToUpperInvariant()))) : null;
    public void OnGet() { Response.Headers.CacheControl = "no-store"; Response.Headers["Referrer-Policy"] = "no-referrer"; }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var hash = TokenHash();
        if (hash is null) { ErrorMessage = "This reset link is invalid or expired. Request a new link."; return Page(); }
        if (Password is null || Password.Length < 8 || Password.Length > 256 || Password != ConfirmPassword)
        { ErrorMessage = "Passwords must match and contain at least 8 characters."; return Page(); }
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var request = await db.PasswordResetRequests.AsNoTracking().SingleOrDefaultAsync(r => r.TokenHash == hash && r.ExpiresUtc > DateTime.UtcNow, ct);
            if (request is null) { ErrorMessage = "This reset link is invalid or expired. Request a new link."; return Page(); }
            var consumed = await db.PasswordResetRequests.Where(r => r.TokenHash == hash && r.ExpiresUtc > DateTime.UtcNow).ExecuteDeleteAsync(ct);
            if (consumed != 1) { ErrorMessage = "This reset link is invalid or expired. Request a new link."; return Page(); }
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, ct);
            if (user is null || !VerificationMailer.Enabled(config) || !await VerificationMailer.IsVerifiedAsync(db, user, ct)) { ErrorMessage = "This reset link is invalid or expired. Request a new link."; return Page(); }
            accounts.SetPassword(user, Password);
            await db.SaveChangesAsync(ct);
            await db.PasswordResetRequests.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            Password = ConfirmPassword = Token = null;
            Completed = true;
            return Page();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning("Password reset could not be completed ({ErrorType}).", ex.GetType().Name); ErrorMessage = "Unable to reset your password. Please try again later."; return Page(); }
    }
}
