using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MySqlConnector;
using tts.Data;

namespace tts.Pages
{
    public class LoginModel : PageModel
    {
        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostLoginAsync([FromForm] string? email, [FromForm] string? password, CancellationToken cancellationToken)
        {
            email = (email ?? "").Trim();
            password ??= "";
            if (email.Length == 0 || email.Length > 45 || password.Length == 0 || password.Length > 256)
                return new JsonResult(new { message = "Incorrect email or password." }) { StatusCode = 401 };

            try
            {
                var db = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var accounts = HttpContext.RequestServices.GetRequiredService<AccountStore>();
                var user = await db.Users.SingleOrDefaultAsync(u => u.Username == email, cancellationToken);
                if (user is null || !accounts.VerifyPassword(user, password))
                    return new JsonResult(new { message = "Incorrect email or password." }) { StatusCode = 401 };

                var name = $"{user.FirstName} {user.LastName}".Trim();
                var identity = new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, name),
                        new Claim(ClaimTypes.Email, user.Username ?? "")
                    },
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return new JsonResult(new { name, email = user.Username });
            }
            catch (Exception ex) when (ex is MySqlException || ex is DbUpdateException || ex is InvalidOperationException)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>()
                    .LogError(ex, "Login failed while accessing the database.");
                return new JsonResult(new { message = "Unable to log in. Check the MySQL server and database settings." }) { StatusCode = 503 };
            }
        }
    }
}
