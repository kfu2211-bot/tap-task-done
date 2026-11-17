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
    [TempData(Key = "VerifyLogin.ResendError")] public string? ResendError { get; set; }
    public string? ErrorMessage { get; set; }
    [TempData(Key = "VerifyLogin.ResendNotice")] public string? Notice { get; set; }
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
            Notice = "A new code has been sent. Use the latest email; your previous code no longer works.";
            return RedirectToPage("/VerifyLogin");
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning("Sign-in code resend failed ({Type}; {Reason}).", ex.GetType().Name, SmtpMailer.Diagnostic(ex));
            ResendError = ex switch
            {
                System.Net.Mail.SmtpException => "The email could not be sent. Wait for the resend timer, then request another code.",
                _ => ex.Message switch
                {
                    "Wait 60 seconds before requesting another code." => "Please wait until the resend timer ends.",
                    "Too many verification requests. Wait 15 minutes before trying again." => ex.Message,
                    "Your account changed. Return to login." => ex.Message,
                    "Your sign-in request expired. Return to login." => ex.Message,
                    _ => "Unable to send a code right now. Try again shortly."
                }
            };
            return RedirectToPage("/VerifyLogin");
        }
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var token = Request.Cookies[LoginSecurity.ChallengeCookie];
        if (!LoginSecurity.Enabled(config) || !LoginSecurity.ValidToken(token)) return RedirectToPage("/Login");
        Code = (Code ?? "").Trim();
        if (Code.Length != 6 || !Code.All(char.IsAsciiDigit)) { ErrorMessage = "Enter the six-digit code from your email."; return Page(); }
        try
        {
            var hash = LoginSecurity.Hash(token!);
            var challenge = await db.Set<LoginChallenge>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == hash, ct);
            if (challenge is null || challenge.Consumed || challenge.ExpiresUtc <= DateTime.UtcNow)
            {
                ErrorMessage = "This sign-in request has expired or was replaced. Return to login to get a new code.";
                return Page();
            }
            ResendSeconds = LoginSecurity.ResendCooldown(challenge.CreatedUtc, DateTime.UtcNow);
            if (challenge.Attempts >= 5)
            {
                ErrorMessage = "Too many incorrect attempts for this code. Request a new code when the resend timer ends.";
                return Page();
            }
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == challenge.UserId, ct);
            if (user is null) { ErrorMessage = "Invalid or expired code. Return to login to request a new code."; return Page(); }
            var destination = await LoginSecurity.SignInAsync(db, HttpContext, user, TrustDevice, ct, new LoginSecurity.ChallengeProof(token!, Code));
            return LocalRedirect(destination);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
                {
            logger.LogWarning("Sign-in verification failed ({Type}).", ex.GetType().Name);
            Code = "";
            ModelState.Remove(nameof(Code));
            ErrorMessage = ex.Message switch
            {
                "Invalid or expired sign-in code." => "That code did not verify. Enter the latest code from your email, or request a new one.",
                "Your account changed. Please log in again." => ex.Message,
                "Your account cannot sign in. Please return to login." => ex.Message,
                _ => "Sign-in could not be completed. Try again shortly."
            };
            return Page();
        }
    }
}
