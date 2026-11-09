using Microsoft.AspNetCore.Mvc;
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
            { Completed = true; RegistrationNotice = null; return Page(); }
            ErrorMessage = "Invalid or expired code. You have up to five attempts per code. Use Resend code if needed.";
            await LoadAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning("Registration code verification failed ({Type}).", ex.GetType().Name); ErrorMessage = "Unable to verify the code. Try again, or change the email if it can no longer be used."; await LoadSafelyAsync(ct); }
        Code = null; ModelState.Clear(); return Page();
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
