using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using tts.Data;

namespace tts.Pages;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LogoutModel(ApplicationDbContext db, IAntiforgery antiforgery, IConfiguration configuration, ILogger<LogoutModel> logger) : PageModel
{
    public IActionResult OnGet()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new JsonResult(new { token = tokens.RequestToken });
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        try
        {
            if (LoginSecurity.Enabled(configuration))
                await LoginSecurity.LogoutAsync(db, HttpContext, ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            logger.LogError(ex, "Could not revoke the server session during logout.");
        }

        await HttpContext.SignOutAsync("AdminCookie");
        await HttpContext.SignOutAsync("Cookies");
        await HttpContext.SignOutAsync("BirthdayPending");
        return new NoContentResult();
    }
}
