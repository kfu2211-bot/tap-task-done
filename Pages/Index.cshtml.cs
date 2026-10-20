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
