using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using tts.Data;
using MySqlConnector;
using Microsoft.AspNetCore.RateLimiting;

namespace tts.Pages
{
    [EnableRateLimiting("PasswordResetLimit")]
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string? Name { get; set; }

        [BindProperty]
        public string? Email { get; set; }

        [BindProperty] public int? BirthMonth { get; set; }
        [BindProperty] public int? BirthDay { get; set; }
        [BindProperty] public int? BirthYear { get; set; }

        public string? ErrorMessage { get; set; }
        public bool ShowExistingAccountLink { get; private set; }

        [TempData]
        public bool AccountCreated { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostRegisterAsync(string? password, CancellationToken cancellationToken)
        {
            var name = (Name ?? "").Trim();
            var email = (Email ?? "").Trim();
            password ??= "";
            Name = name;
            Email = email;

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].Length > 45 || string.Join(' ', parts.Skip(1)).Length > 45 ||
                email.Length == 0 || email.Length > 45 || !new EmailAddressAttribute().IsValid(email))
            {
                ErrorMessage = "Enter a valid name and email.";
                return Page();
            }

            if (!tts.Data.NewPasswordPolicy.IsValid(password))
            { ErrorMessage = tts.Data.NewPasswordPolicy.Requirements; return Page(); }

            if (!BirthYear.HasValue || !BirthMonth.HasValue || !BirthDay.HasValue ||
                BirthYear < 1900 || BirthYear > 9999 || BirthMonth < 1 || BirthMonth > 12 ||
                BirthDay < 1 || BirthDay > DateTime.DaysInMonth(BirthYear.Value, BirthMonth.Value))
            {
                ErrorMessage = "Please select a valid date of birth.";
                return Page();
            }
            var birthDate = new DateTime(BirthYear.Value, BirthMonth.Value, BirthDay.Value);
            var today = DateTime.UtcNow.AddHours(8).Date; // Philippine calendar date
            if (birthDate > today.AddYears(-16))
            {
                ErrorMessage = "You must be at least 16 years old to create an account.";
                return Page();
            }
            
            try
            {
                var db = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var accounts = HttpContext.RequestServices.GetRequiredService<AccountStore>();
                var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == email, cancellationToken);
                if (existing is not null)
                {
                    var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                    // A repeated registration may be the same pending account
                    // after a failed delivery. Authenticate it; never overwrite it.
                    if (VerificationMailer.Enabled(config) && accounts.VerifyPassword(existing, password) &&
                        !AccountModerationRules.IsStaff(existing.Role) && existing.Role != "deleted" &&
                        !await ModerationGuard.IsSuspendedAsync(db, existing.Id, cancellationToken) &&
                        !await VerificationMailer.IsVerifiedAsync(db, existing, cancellationToken))
                        return await BeginVerificationAsync(db, existing, config, cancellationToken);
                    ShowExistingAccountLink = true;
                    ErrorMessage = "An account already exists for this email. Use your original password to sign in and resume verification if needed.";
                    return Page();
                }
                var user = new AppUser { FirstName = parts[0], LastName = string.Join(' ', parts.Skip(1)), Username = email, Role = "client", DateOfBirth = birthDate };
                accounts.SetPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
                if (VerificationMailer.Enabled(HttpContext.RequestServices.GetRequiredService<IConfiguration>()))
                {
                    return await BeginVerificationAsync(db, user, HttpContext.RequestServices.GetRequiredService<IConfiguration>(), cancellationToken);
                }
                AccountCreated = true;
                return RedirectToPage("/Index");
            }
            catch (Exception ex) when (ex is MySqlException || ex is DbUpdateException || ex is InvalidOperationException)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<IndexModel>>()
                    .LogError(ex, "Registration failed while accessing the database.");
                ErrorMessage = "Unable to save the account. Check the MySQL server and database settings.";
                return Page();
            }
        }

        private async Task<IActionResult> BeginVerificationAsync(ApplicationDbContext db, AppUser user, IConfiguration config, CancellationToken ct)
        {
            try
            {
                await RegistrationVerification.StartAsync(db, HttpContext, user, config, ct);
                TempData["RegistrationNotice"] = "Continue email verification. If the code has not arrived, check Spam or use Resend code when the timer ends. Use the latest code.";
            }
            catch (RegistrationFlowException ex) { TempData["RegistrationNotice"] = ex.Message; }
            catch (Exception ex) when (ex is MySqlException or DbUpdateException or InvalidOperationException or System.Net.Mail.SmtpException or FormatException)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<IndexModel>>().LogWarning("Verification email could not be sent ({ErrorType}; {Reason}).", ex.GetType().Name, SmtpMailer.Diagnostic(ex));
                TempData["RegistrationNotice"] = SmtpMailer.VerificationFailureMessage(ex);
            }
            return RedirectToPage("/VerifyEmail");
        }
    }
}
