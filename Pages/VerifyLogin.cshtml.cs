using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[EnableRateLimiting("AdminLoginLimit")]
public sealed class VerifyLoginModel(ApplicationDbContext db, IConfiguration config, ILogger<VerifyLoginModel> logger) : PageModel
{
    [BindProperty] public string Code { get; set; } = "";
    [BindProperty] public bool TrustDevice { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Notice { get; set; }
    public int ResendSeconds { get; private set; }
    public async Task OnGetAsync(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; await LoadCooldownAsync(ct); }
    private async Task LoadCooldownAsync(CancellationToken ct)
    {
        var token = Request.Cookies[LoginSecurity.ChallengeCookie];
        if (!LoginSecurity.ValidToken(token)) return;
        var hash = LoginSecurity.Hash(token!);
        var created = await db.Set<LoginChallenge>().AsNoTracking().Where(x => x.Id == hash).Select(x => (DateTime?)x.CreatedUtc).SingleOrDefaultAsync(ct);
        if (created.HasValue) ResendSeconds = LoginSecurity.ResendCooldown(created.Value, DateTime.UtcNow);
    }
    public async Task<IActionResult> OnPostResendAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var token = Request.Cookies[LoginSecurity.ChallengeCookie];
        if (!LoginSecurity.Enabled(config) || !LoginSecurity.ValidToken(token)) return RedirectToPage("/Login");
        try
        {
            var hash = LoginSecurity.Hash(token!);
            var challenge = await db.Set<LoginChallenge>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == hash, ct);
            var user = challenge is null ? null : await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == challenge.UserId, ct);
            if (user is null) return RedirectToPage("/Login");
            await LoginSecurity.BeginAsync(db, HttpContext, user, config, ct, token);
            ResendSeconds = 60; Notice = "A new code has been sent. Use the latest email; your previous code no longer works.";
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning("Sign-in code resend failed ({Type}; {Reason}).", ex.GetType().Name, SmtpMailer.Diagnostic(ex));
            ErrorMessage = ex.Message switch
            {
                "Wait 60 seconds before requesting another code." => ex.Message,
                "Too many verification requests. Wait 15 minutes before trying again." => ex.Message,
                "Your account changed. Return to login." => ex.Message,
                "Your sign-in request expired. Return to login." => ex.Message,
                _ => "Unable to send a code right now. Wait a minute and try again."
            };
            // A new challenge cookie may have been issued before a delivery failure.
            ResendSeconds = ex is System.Net.Mail.SmtpException ? 60 : 0;
            if (ex is InvalidOperationException && ex is not DbUpdateException) await LoadCooldownAsync(ct);
        }
        Code = ""; ModelState.Clear(); return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var token = Request.Cookies[LoginSecurity.ChallengeCookie];
        if (!LoginSecurity.Enabled(config) || !LoginSecurity.ValidToken(token)) return RedirectToPage("/Login");
        Code = (Code ?? "").Trim();
        if (Code.Length != 6 || !Code.All(char.IsAsciiDigit)) { ErrorMessage = "Enter the six-digit code from your email."; return Page(); }
        await LoadCooldownAsync(ct);
        try
        {
            var hash = LoginSecurity.Hash(token!);
            var userId = await db.Set<LoginChallenge>().AsNoTracking().Where(x => x.Id == hash).Select(x => (int?)x.UserId).SingleOrDefaultAsync(ct);
            var user = userId.HasValue ? await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId.Value, ct) : null;
            if (user is null) { ErrorMessage = "Invalid or expired code. Return to login to request a new code."; return Page(); }
            var destination = await LoginSecurity.SignInAsync(db, HttpContext, user, TrustDevice, ct, new LoginSecurity.ChallengeProof(token!, Code));
            return LocalRedirect(destination);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning("Sign-in verification failed ({Type}).", ex.GetType().Name); Code = ""; ModelState.Clear(); ErrorMessage = "Invalid or expired code, or sign-in is unavailable. Five attempts are allowed per code. Return to login to request a new one."; return Page(); }
    }
}
