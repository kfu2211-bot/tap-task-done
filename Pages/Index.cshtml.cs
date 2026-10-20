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
            if (parts.Length < 2 || parts[0].Length > 45 || string.Join(' ', parts.Skip(1)).Length > 45)
            {
                ErrorMessage = "Please enter your first and last name (maximum 45 characters each).";
                return Page();
            }
            if (email.Length > 45 || !new EmailAddressAttribute().IsValid(email))
            {
                ErrorMessage = "Please enter a valid email of at most 45 characters.";
                return Page();
            }
            if (password.Length < 8 || password.Length > 256 || !Regex.IsMatch(password, "[A-Z]") || !Regex.IsMatch(password, "[a-z]") || !Regex.IsMatch(password, "[0-9]") || !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
            {
                ErrorMessage = "Please choose a strong password of at least 8 characters.";
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
                var user = new AppUser { FirstName = parts[0], LastName = string.Join(' ', parts.Skip(1)), Username = email, Role = "client" };
                accounts.SetPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
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
