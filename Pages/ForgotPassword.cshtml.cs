using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[EnableRateLimiting("PasswordResetLimit")]
public class ForgotPasswordModel(ApplicationDbContext db, IConfiguration config, AccountStore accounts, ILogger<ForgotPasswordModel> logger) : PageModel
{
    private const string CookieName = "tts.reset.challenge";
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string[] CodeDigits { get; set; } = [];
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? ConfirmPassword { get; set; }
    public string Step { get; set; } = "email";
    public string? ErrorMessage { get; set; }
    public string? Notice { get; set; }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string CodeHash(string challenge, string code) => Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(challenge), Encoding.UTF8.GetBytes(code)));
    private string? ResetChallenge => Request.Cookies[CookieName] is string c && c.Length == 64 && c.All(Uri.IsHexDigit) ? c : null;
    private void NoStore() { Response.Headers.CacheControl = "no-store"; Response.Headers["Referrer-Policy"] = "no-referrer"; }
    public void OnGet() { NoStore(); Response.Cookies.Delete(CookieName); }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken ct)
    {
        NoStore();
        var email = (Email ?? "").Trim();
        if (email.Length == 0 || email.Length > 45 || !new EmailAddressAttribute().IsValid(email)) { ErrorMessage = "Enter a valid email address."; return Page(); }
        var stage = "account lookup";
        try
        {
            // Account lookup needs only the ID, not encrypted profile-photo bytes.
            var user = await db.Users.AsNoTracking().Where(u => u.Username == email).Select(u => new { u.Id }).SingleOrDefaultAsync(ct);
            if (user is null)
            {
                Response.Cookies.Delete(CookieName);
                ErrorMessage = "No account was found for this email address. Please check your email or create an account.";
                return Page();
            }

            var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            stage = "reset-code storage";
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.PasswordResetCodes.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
            db.PasswordResetCodes.Add(new PasswordResetCode { ChallengeHash = Hash(challenge), UserId = user.Id, CodeHash = CodeHash(challenge, code), ExpiresUtc = DateTime.UtcNow.AddMinutes(10) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            stage = "email delivery";
            await SmtpMailer.SendAsync(config, email, "Your Tap, Task, Done verification code", $"Your password reset code is {code}. It expires in 10 minutes. If you did not request this, ignore this message.", ct);
            Response.Cookies.Append(CookieName, challenge, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromMinutes(10), IsEssential = true });
            Step = "code"; Notice = "A verification code was sent. Check your inbox and spam folder."; return Page();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or SmtpException or InvalidOperationException or OperationCanceledException or TimeoutException or FormatException or MimeKit.ParseException)
        {
            // Log a safe diagnostic category without recording credentials, addresses or codes.
            var databaseError = ex as MySqlConnector.MySqlException ?? ex.InnerException as MySqlConnector.MySqlException;
            logger.LogWarning("Password reset request failed at {Stage}: {ErrorType}, cause {CauseType}, database code {DatabaseCode}, trace {TraceId}.",
                stage, ex.GetType().Name, ex.InnerException?.GetType().Name, databaseError?.Number, HttpContext.TraceIdentifier);
            ErrorMessage = "Unable to send a verification code. Please try again later."; return Page();
        }
    }

    public async Task<IActionResult> OnPostVerifyAsync(CancellationToken ct)
    {
        NoStore(); Step = "code";
        var challenge = ResetChallenge; var code = string.Concat(CodeDigits);
        if (challenge is null || code.Length != 6 || code.Any(c => c < '0' || c > '9')) { ErrorMessage = "Enter the 6-digit code."; return Page(); }
        try
        {
            var hash = Hash(challenge); var now = DateTime.UtcNow;
            var userId = await db.PasswordResetCodes.AsNoTracking().Where(x => x.ChallengeHash == hash && x.ExpiresUtc > now && !x.Verified).Select(x => (int?)x.UserId).SingleOrDefaultAsync(ct);
            if (userId is null) { ErrorMessage = "Invalid or expired code. Request a new code if needed."; return Page(); }
            var cutoff = now.AddMinutes(-15);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO password_reset_code_limits (UserId, WindowStartedUtc, Attempts) VALUES ({userId.Value}, {now}, 0)", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE password_reset_code_limits SET Attempts = 0, WindowStartedUtc = {now} WHERE UserId = {userId.Value} AND WindowStartedUtc <= {cutoff}", ct);
            var allowed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE password_reset_code_limits SET Attempts = Attempts + 1 WHERE UserId = {userId.Value} AND Attempts < 10", ct);
            if (allowed != 1) { ErrorMessage = "Too many attempts. Please try again later."; return Page(); }
            var incremented = await db.PasswordResetCodes.Where(x => x.ChallengeHash == hash && x.ExpiresUtc > now && x.Attempts < 5 && !x.Verified).ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
            var request = incremented == 1 ? await db.PasswordResetCodes.AsNoTracking().SingleOrDefaultAsync(x => x.ChallengeHash == hash, ct) : null;
            var expected = CodeHash(challenge, code);
            if (request is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(request.CodeHash), Convert.FromHexString(expected))) { ErrorMessage = "Invalid or expired code. Request a new code if needed."; return Page(); }
            var verified = await db.PasswordResetCodes.Where(x => x.ChallengeHash == hash && x.ExpiresUtc > now && !x.Verified).ExecuteUpdateAsync(s => s.SetProperty(x => x.Verified, true), ct);
            if (verified != 1) { ErrorMessage = "Invalid or expired code. Request a new code."; return Page(); }
            Step = "password"; Notice = "Code verified. Choose a new password."; return Page();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException or FormatException) { logger.LogWarning("Code verification failed ({ErrorType}).", ex.GetType().Name); ErrorMessage = "Unable to verify the code. Please try again later."; return Page(); }
    }

    public async Task<IActionResult> OnPostResetAsync(CancellationToken ct)
    {
        NoStore(); Step = "password";
        if (Password is null || Password.Length < 8 || Password.Length > 256 || Password != ConfirmPassword) { ErrorMessage = "Passwords must match and contain at least 8 characters."; return Page(); }
        var challenge = ResetChallenge;
        if (challenge is null) { Step = "email"; ErrorMessage = "Your reset session expired. Request a new code."; return Page(); }
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var hash = Hash(challenge); var now = DateTime.UtcNow;
            var request = await db.PasswordResetCodes.AsNoTracking().SingleOrDefaultAsync(x => x.ChallengeHash == hash && x.Verified && x.ExpiresUtc > now, ct);
            if (request is null || await db.PasswordResetCodes.Where(x => x.ChallengeHash == hash && x.Verified && x.ExpiresUtc > now).ExecuteDeleteAsync(ct) != 1) { Step = "email"; ErrorMessage = "Your reset session expired. Request a new code."; return Page(); }
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct);
            if (user is null) { Step = "email"; ErrorMessage = "Your reset session expired. Request a new code."; return Page(); }
            accounts.SetPassword(user, Password);
            await db.SaveChangesAsync(ct);
            await db.PasswordResetCodes.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.PasswordResetRequests.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            Response.Cookies.Delete(CookieName); Password = ConfirmPassword = null; Step = "done"; return Page();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning("Password reset failed ({ErrorType}).", ex.GetType().Name); ErrorMessage = "Unable to reset your password. Please try again later."; return Page(); }
    }
}
