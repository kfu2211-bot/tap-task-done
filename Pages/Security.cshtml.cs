using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(AuthenticationSchemes = "Cookies,AdminCookie")]
[EnableRateLimiting("PasswordResetLimit")]
public sealed class SecurityModel(ApplicationDbContext db, AccountStore accounts, IConfiguration config, ILogger<SecurityModel> logger) : PageModel
{
    public List<LoginSession> Sessions { get; private set; } = [];
    public List<LoginAlert> Alerts { get; private set; } = [];
    public List<TrustedLoginDevice> Devices { get; private set; } = [];
    public string CurrentId => LoginSecurity.ValidToken(User.FindFirstValue("login_session")) ? LoginSecurity.Hash(User.FindFirstValue("login_session")!) : "";
    [TempData] public string? Notice { get; set; }
    [BindProperty] public string CurrentPassword { get; set; } = "";
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!LoginSecurity.Enabled(config)) { Notice = "Login security is not enabled yet."; return Page(); }
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == UserId, ct);
        var version = AccountStore.SessionFingerprint(user.PasswordHash); var now = DateTime.UtcNow;
        Sessions = await db.Set<LoginSession>().AsNoTracking().Where(x => x.UserId == UserId && x.PasswordVersion == version && x.RevokedUtc == null && x.ExpiresUtc > now).OrderByDescending(x => x.LastSeenUtc).Take(50).ToListAsync(ct);
        Devices = await db.Set<TrustedLoginDevice>().AsNoTracking().Where(x => x.UserId == UserId && x.PasswordVersion == version && x.ExpiresUtc > now).OrderByDescending(x => x.ExpiresUtc).Take(50).ToListAsync(ct);
        Alerts = await db.Set<LoginAlert>().AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.Id).Take(30).ToListAsync(ct);
        return Page();
    }
    public async Task<IActionResult> OnPostReadAsync(long id, CancellationToken ct)
    {
        if (!LoginSecurity.Enabled(config)) return NotFound();
        await db.Set<LoginAlert>().Where(x => x.UserId == UserId && x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadUtc, DateTime.UtcNow), ct);
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostManageAsync(string action, string? id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!LoginSecurity.Enabled(config)) return NotFound();
        if (CurrentPassword is null || CurrentPassword.Length is 0 or > 256 || action is not ("revoke" or "others" or "forget" or "secure"))
        { Notice = "Enter your current password and choose an action."; return RedirectToPage(); }
        if (action == "secure" && (NewPassword is null || NewPassword.Length is < 8 or > 256 || NewPassword != ConfirmPassword || NewPassword == CurrentPassword))
        { Notice = "Choose a different password with at least 8 characters and confirm it."; return RedirectToPage(); }
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={UserId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (user is null || !accounts.VerifyPassword(user, CurrentPassword))
            { Notice = "Current password is incorrect."; return RedirectToPage(); }
            var now = DateTime.UtcNow;
            if (action == "secure")
            {
                accounts.SetPassword(user, NewPassword); await db.SaveChangesAsync(ct);
                await LoginSecurity.RevokeAllAsync(db, UserId, ct);
            }
            else if (action == "forget") await db.Set<TrustedLoginDevice>().Where(x => x.UserId == UserId && x.Id == id).ExecuteDeleteAsync(ct);
            else
            {
                var current = CurrentId;
                await db.Set<LoginSession>().Where(x => x.UserId == UserId && x.RevokedUtc == null && (action == "others" ? x.Id != current : x.Id == id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, now), ct);
                // A revoked session must not immediately return using its saved trusted-browser token.
                await db.Set<TrustedLoginDevice>().Where(x => x.UserId == UserId).ExecuteDeleteAsync(ct);
            }
            await tx.CommitAsync(ct);
            if (action == "secure")
            {
                Response.Cookies.Delete(LoginSecurity.TrustCookie(UserId));
                await HttpContext.SignOutAsync("Cookies"); await HttpContext.SignOutAsync("AdminCookie");
                try { await SmtpMailer.SendAsync(config, user.Username!, "Your Tap, Task, Done account was secured", "Your password was changed, all sessions were signed out, and trusted browsers were removed. If this was not you, use Forgot password on the website immediately.", ct); }
                catch (Exception ex) when (ex is System.Net.Mail.SmtpException or FormatException or InvalidOperationException) { logger.LogWarning("Account security email failed ({Type}).", ex.GetType().Name); }
                return RedirectToPage("/Login");
            }
            Notice = action == "forget" ? "Trusted browser removed. Its next sign-in will require a code." : "Session access revoked. Other browsers are signed out on their next request; trusted browsers have been cleared.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning("Session management failed ({Type}).", ex.GetType().Name); Notice = "Unable to update account security. Please try again."; return RedirectToPage(); }
    }
}
