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
    public void OnGet() => Response.Headers.CacheControl = "no-store";
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
