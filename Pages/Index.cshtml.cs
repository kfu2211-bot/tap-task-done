using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using tts.Data;
using MySqlConnector;

namespace tts.Pages
{
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
                email.Length == 0 || email.Length > 45 || !new EmailAddressAttribute().IsValid(email) ||
                password.Length < 8 || password.Length > 256)
            {
                ErrorMessage = "Enter a valid name and email, and a password of at least 8 characters.";
                return Page();
            }

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
                if (await accounts.UsernameExistsAsync(email, cancellationToken))
                {
                    ErrorMessage = "That email is already registered.";
                    return Page();
                }
                var user = new AppUser { FirstName = parts[0], LastName = string.Join(' ', parts.Skip(1)), Username = email, Role = "client", DateOfBirth = birthDate };
                accounts.SetPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
                if (VerificationMailer.Enabled(HttpContext.RequestServices.GetRequiredService<IConfiguration>()))
                {
                    try { await VerificationMailer.SendAsync(db, HttpContext.RequestServices.GetRequiredService<IConfiguration>(), HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>(), user, cancellationToken); }
                    catch (Exception ex) when (ex is MySqlException or DbUpdateException or InvalidOperationException or System.Net.Mail.SmtpException or FormatException)
                    { HttpContext.RequestServices.GetRequiredService<ILogger<IndexModel>>().LogWarning("Verification email could not be sent ({ErrorType}).", ex.GetType().Name); }
                    return RedirectToPage("/VerifyEmail");
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
    }
}
