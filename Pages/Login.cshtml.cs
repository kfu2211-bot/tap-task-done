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
                if (!isStaff)
                {
                    var penalty = await OffensePolicy.GetAsync(db, user.Id, cancellationToken);
                    if (penalty.Suspended)
                    {
                        var reason = await db.Set<AccountOffense>().AsNoTracking().Where(o => o.UserId == user.Id).OrderByDescending(o => o.Id).Select(o => o.Reason).FirstOrDefaultAsync(cancellationToken)
                            ?? await db.AccountModerationActions.AsNoTracking().Where(a => a.UserId == user.Id && a.Action == "suspend").OrderByDescending(a => a.Id).Select(a => a.Reason).FirstOrDefaultAsync(cancellationToken);
                        var message = penalty.Permanent ? "Your account is permanently suspended. Contact the administrator for review." : penalty.Until.HasValue ? $"Your account is suspended until {DisplayTime.Philippine(penalty.Until.Value)} (Philippine time). Access returns automatically after this time." : "Your account has a previous suspension. Contact the administrator for review.";
                        return new JsonResult(new { message = message + (string.IsNullOrWhiteSpace(reason) ? "" : " Reason: " + reason), suspendedUntilUtc = penalty.Until?.ToString("yyyy-MM-ddTHH:mm:ssZ") }) { StatusCode = 403 };
                    }
                }
                // Remove any previous identity so changing accounts cannot retain staff access.
                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                if (LoginSecurity.Enabled(config)) await LoginSecurity.LogoutAsync(db, HttpContext, cancellationToken);
                await HttpContext.SignOutAsync("AdminCookie");
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync("BirthdayPending");
                if (VerificationMailer.Enabled(config) && !await VerificationMailer.IsVerifiedAsync(db, user, cancellationToken))
                {
                    try { await RegistrationVerification.StartAsync(db, HttpContext, user, config, cancellationToken); }
                    catch (Exception ex) when (ex is MySqlException or DbUpdateException or InvalidOperationException or System.Net.Mail.SmtpException or FormatException)
                    { HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>().LogWarning("Verification email could not be sent ({ErrorType}; {Reason}).", ex.GetType().Name, SmtpMailer.Diagnostic(ex)); TempData["RegistrationNotice"] = "Email verification is still pending. If a code could not be sent, wait a minute and try again."; }
                    return new JsonResult(new { redirect = "/VerifyEmail" });
                }
                if (!user.DateOfBirth.HasValue)
                {
                    var pending = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim("password_version", AccountStore.SessionFingerprint(user.PasswordHash)) }, "BirthdayPending");
                    await HttpContext.SignInAsync("BirthdayPending", new ClaimsPrincipal(pending));
                    return new JsonResult(new { name, email = user.Username, redirect = "/Birthday" });
                }
                if (LoginSecurity.Enabled(config))
                {
                    if (await LoginSecurity.TrustedAsync(db, HttpContext, user, cancellationToken) || (VerificationMailer.Enabled(config) && await RegistrationVerification.FirstLoginAvailableAsync(db, HttpContext, user, cancellationToken)))
                    {
                        var destination = await LoginSecurity.SignInAsync(db, HttpContext, user, false, cancellationToken);
                        return new JsonResult(new { name, email = user.Username, redirect = destination });
                    }
                    try { await LoginSecurity.BeginAsync(db, HttpContext, user, config, cancellationToken); }
                    catch (Exception ex) when (ex is System.Net.Mail.SmtpException or FormatException or InvalidOperationException)
                    {
                        HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>().LogWarning("New-device code delivery failed ({Type}; {Reason}).", ex.GetType().Name, SmtpMailer.Diagnostic(ex));
                        return new JsonResult(new { message = "Unable to send a sign-in code. Try again later. No new session was created." }) { StatusCode = 503 };
                    }
                    return new JsonResult(new { name, email = user.Username, redirect = "/VerifyLogin" });
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
            catch (InvalidOperationException ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>()
                    .LogError(ex, "Login could not complete because configuration or account state was invalid.");
                return new JsonResult(new { message = "Unable to complete sign-in. Please try again or contact support." }) { StatusCode = 503 };
            }
            catch (Exception ex) when (ex is MySqlException || ex is DbUpdateException)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<LoginModel>>()
                    .LogError(ex, "Login failed while accessing the database.");
                return new JsonResult(new { message = "Unable to log in. Check the MySQL server and database settings." }) { StatusCode = 503 };
            }
        }
    }
}
