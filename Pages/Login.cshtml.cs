using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using MySqlConnector;
using tts.Data;

namespace tts.Pages
{
    [EnableRateLimiting("AdminLoginLimit")]
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

                if (user.DateOfBirth.HasValue && user.DateOfBirth.Value.Date > DateTime.UtcNow.AddHours(8).Date.AddYears(-16))
                    return new JsonResult(new { message = "You must be at least 16 years old to use this app." }) { StatusCode = 403 };

                var name = $"{user.FirstName} {user.LastName}".Trim();
                var role = (user.Role ?? "").Trim().ToLowerInvariant();
                var isStaff = role is "admin" or "moderator";
                if (isStaff && !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                    return new JsonResult(new { message = "Staff login is unavailable until production security is configured." }) { StatusCode = 403 };

                // Remove any previous identity so changing accounts cannot retain staff access.
                await HttpContext.SignOutAsync("AdminCookie");
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync("BirthdayPending");
                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                if (VerificationMailer.Enabled(config) && !await VerificationMailer.IsVerifiedAsync(db, user, cancellationToken))
                {
                    try { await VerificationMailer.SendAsync(db, config, HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>(), user, cancellationToken); }
                    catch (Exception ex) when (ex is MySqlException or DbUpdateException or InvalidOperationException or System.Net.Mail.SmtpException or FormatException)
                    { HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>().LogWarning("Verification email could not be sent ({ErrorType}).", ex.GetType().Name); }
                    return new JsonResult(new { redirect = "/VerifyEmail" });
                }
                if (!user.DateOfBirth.HasValue)
                {
                    var pending = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim("password_version", AccountStore.SessionFingerprint(user.PasswordHash)) }, "BirthdayPending");
                    await HttpContext.SignInAsync("BirthdayPending", new ClaimsPrincipal(pending));
                    return new JsonResult(new { name, email = user.Username, redirect = "/Birthday" });
                }
                if (isStaff)
                {
                    var staffIdentity = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, name),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("password_version", AccountStore.SessionFingerprint(user.PasswordHash))
                    }, "AdminCookie");
                    await HttpContext.SignInAsync("AdminCookie", new ClaimsPrincipal(staffIdentity));
                    return new JsonResult(new { name, email = user.Username, redirect = "/Admin" });
                }
                var identity = new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, name),
                        new Claim(ClaimTypes.Email, user.Username ?? ""),
                        new Claim("password_version", AccountStore.SessionFingerprint(user.PasswordHash))
                    },
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return new JsonResult(new { name, email = user.Username, redirect = "/Dashboard" });
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
