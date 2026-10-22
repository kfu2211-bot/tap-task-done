using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tts.Data;

namespace tts.Pages;

public class BirthdayModel(ApplicationDbContext db, ILogger<BirthdayModel> logger) : PageModel
{
    [BindProperty] public DateTime? DateOfBirth { get; set; }
    public string? ErrorMessage { get; set; }

    private async Task<int?> PendingUserId()
    {
        var pending = await HttpContext.AuthenticateAsync("BirthdayPending");
        if (!pending.Succeeded || !int.TryParse(pending.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id <= 0) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        return user is not null && pending.Principal?.FindFirstValue("password_version") == AccountStore.SessionFingerprint(user.PasswordHash) ? id : null;
    }

    public async Task<IActionResult> OnGetAsync() => await PendingUserId() is null ? RedirectToPage("/Login") : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var id = await PendingUserId();
        if (id is null) return RedirectToPage("/Login");
        var today = DateTime.UtcNow.AddHours(8).Date;
        if (!DateOfBirth.HasValue || DateOfBirth.Value.Year < 1900 || DateOfBirth.Value.Date > today.AddYears(-16))
        {
            ErrorMessage = "Enter a valid birthday. You must be at least 16 years old to use the app.";
            return Page();
        }
        try
        {
            var updated = await db.Users.Where(u => u.Id == id.Value && u.DateOfBirth == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.DateOfBirth, DateOfBirth.Value.Date), cancellationToken);
            await HttpContext.SignOutAsync("BirthdayPending");
            TempData["BirthdayMessage"] = updated == 1 ? "Birthday saved. Please log in again to continue." : "Please log in again to continue.";
            return RedirectToPage("/Login");
        }
        catch (Exception ex) when (ex is MySqlException or DbUpdateException or InvalidOperationException)
        {
            logger.LogError(ex, "Could not save birthday for existing account.");
            ErrorMessage = "Unable to save your birthday. Please try again later.";
            return Page();
        }
    }
}
