using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[EnableRateLimiting("PasswordResetLimit")]
public sealed class VerifyEmailModel(ApplicationDbContext db, IConfiguration config, ILogger<VerifyEmailModel> logger) : PageModel
{
    [BindProperty] public string? Code { get; set; }
    [BindProperty] public string? NewEmail { get; set; }
    [BindProperty] public string? Password { get; set; }
    public string? DestinationEmail { get; private set; }
    public int ResendSeconds { get; private set; }
    public bool Pending { get; private set; }
    public bool Completed { get; private set; }
    public string? ErrorMessage { get; private set; }
    [TempData] public string? RegistrationNotice { get; set; }
    private void Protect()
    { Response.Headers.CacheControl = "no-store"; Response.Headers["Referrer-Policy"] = "no-referrer"; }
    private async Task LoadAsync(CancellationToken ct)
    {
        Protect();
        if (!VerificationMailer.Enabled(config)) { ErrorMessage = "Email verification is not enabled yet."; return; }
        var row = await RegistrationVerification.PendingAsync(db, HttpContext, ct);
        Pending = row is not null;
        DestinationEmail = row?.Email;
        ResendSeconds = row is null ? 0 : RegistrationVerification.SendWaitSeconds(row, DateTime.UtcNow);
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning("Registration verification page failed ({Type}).", ex.GetType().Name); ErrorMessage = "Verification is temporarily unavailable. Return to login to try again."; }
        return Page();
    }
    public async Task<IActionResult> OnPostConfirmAsync(CancellationToken ct)
    {
        Protect();
        if (!VerificationMailer.Enabled(config)) return RedirectToPage("/Login");
        Code = (Code ?? "").Trim();
        try
        {
            await LoadAsync(ct);
            if (RegistrationVerification.ValidCode(Code) && await RegistrationVerification.ConfirmAsync(db, HttpContext, Code, ct))
            { RegistrationNotice = null; return RedirectToPage(new { handler = "Continue" }); }
            ErrorMessage = "Invalid or expired code. You have up to five attempts per code. Use Resend code if needed.";
            await LoadAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning("Registration code verification failed ({Type}).", ex.GetType().Name); ErrorMessage = "Unable to verify the code. Try again, or change the email if it can no longer be used."; await LoadSafelyAsync(ct); }
        Code = null; ModelState.Clear(); return Page();
    }
    public async Task<IActionResult> OnGetContinueAsync(CancellationToken ct)
    {
        Protect();
        var token = Request.Cookies[RegistrationVerification.FirstLoginCookie];
        if (!VerificationMailer.Enabled(config) || !LoginSecurity.ValidToken(token))
            return RedirectToPage("/Login");
        try
        {
            var hash = LoginSecurity.Hash(token!);
            var now = DateTime.UtcNow;
            var user = await (from proof in db.Set<RegistrationEmailCode>().AsNoTracking()
                join account in db.Users.AsNoTracking() on proof.UserId equals account.Id
                where proof.FirstLoginHash == hash && proof.FirstLoginExpiresUtc > now &&
                    proof.Email == account.Username
                select account).SingleOrDefaultAsync(ct);
            if (user is null || AccountModerationRules.IsStaff(user.Role) || user.Role == "deleted")
                return RedirectToPage("/Login");
            if (LoginSecurity.Enabled(config))
            {
                await LoginSecurity.SignInAsync(db, HttpContext, user, false, ct, registrationOnly: true);
            }
            else
            {
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                var current = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={user.Id} FOR UPDATE")
                    .AsNoTracking().ToListAsync(ct)).SingleOrDefault();
                if (current is null || AccountModerationRules.IsStaff(current.Role) || current.Role == "deleted" ||
                    current.DateOfBirth is not DateTime birth || birth.Date > now.AddHours(8).Date.AddYears(-16) ||
                    !await VerificationMailer.IsVerifiedAsync(db, current, ct) ||
                    await ModerationGuard.IsSuspendedAsync(db, current.Id, ct) ||
                    !await RegistrationVerification.ConsumeFirstLoginAsync(db, HttpContext, current, ct))
                    return RedirectToPage("/Login");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, current.Id.ToString()),
                    new Claim(ClaimTypes.Name, $"{current.FirstName} {current.LastName}".Trim()),
                    new Claim(ClaimTypes.Email, current.Username ?? ""),
                    new Claim("password_version", AccountStore.SessionFingerprint(current.PasswordHash))
                }, "Cookies");
                await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(identity));
            }
            TempData["RegistrationSuccess"] = "Account created and email verified.";
            return RedirectToPage("/Dashboard");
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning("Registration sign-in failed ({Type}).", ex.GetType().Name);
            return RedirectToPage("/Login");
        }
    }
    public Task<IActionResult> OnPostResendAsync(CancellationToken ct) => SendAsync(false, ct);
    public Task<IActionResult> OnPostChangeAsync(CancellationToken ct) => SendAsync(true, ct);
    private async Task<IActionResult> SendAsync(bool change, CancellationToken ct)
    {
        Protect();
        if (!VerificationMailer.Enabled(config)) return RedirectToPage("/Login");
        var email = change ? (NewEmail ?? "").Trim() : null;
        if (change && !RegistrationVerification.ValidEmail(email))
        { ErrorMessage = "Enter a valid email address."; await LoadSafelyAsync(ct); Password = null; ModelState.Remove(nameof(Password)); return Page(); }
        try
        {
            await RegistrationVerification.SendAsync(db, HttpContext, config, email, change ? Password : null, ct);
            RegistrationNotice = change ? "A new code was sent to the corrected email. Only the latest code will work." : "A new code was sent. Check your inbox and spam folder.";
            return RedirectToPage();
        }
        catch (RegistrationFlowException ex)
        { ErrorMessage = ex.Message; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or System.Net.Mail.SmtpException or FormatException or InvalidOperationException)
        { logger.LogWarning("Registration email delivery failed ({Type}; {Reason}).", ex.GetType().Name, SmtpMailer.Diagnostic(ex)); ErrorMessage = "Unable to send a code. Your account is still pending. Check your email or try again after the cooldown."; }
        Password = Code = null; ModelState.Remove(nameof(Password)); ModelState.Remove(nameof(Code));
        await LoadSafelyAsync(ct); return Page();
    }
    private async Task LoadSafelyAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning("Pending registration lookup failed ({Type}).", ex.GetType().Name); Pending = false; }
    }
}
