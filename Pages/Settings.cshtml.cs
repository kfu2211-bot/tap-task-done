using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class SettingsModel(ApplicationDbContext db) : PageModel
{
    public string DisplayName { get; private set; } = "";
    public string EffectiveAvailability { get; private set; } = "Available";
    public string Email { get; private set; } = "";
    [BindProperty] public string? Service { get; set; }
    [BindProperty] public string? Location { get; set; }
    [BindProperty] public int? Rate { get; set; }
    [BindProperty] public string WorkRole { get; set; } = "client";
    [BindProperty] public string Availability { get; set; } = "Available";
    [BindProperty] public string[] Days { get; set; } = [];
    [TempData] public string? Message { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == UserId);
        if (user is null) { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
        DisplayName = $"{user.FirstName} {user.LastName}".Trim(); Email = user.Username ?? "";
        var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId);
        if (profile != null) { Service = profile.Service; Location = profile.Location; Rate = profile.Rate; WorkRole = profile.WorkRole; Availability = profile.Availability; Days = profile.AvailableDays.Split(',', StringSplitOptions.RemoveEmptyEntries); }
        EffectiveAvailability = await FreelancerAvailability.GetAsync(db, UserId, Availability, HttpContext.RequestAborted);
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var validDays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        Service = (Service ?? "").Trim(); Location = (Location ?? "").Trim();
        Days ??= [];
        if (!ModelState.IsValid || UserId == 0 || Service.Length > 60 || Location.Length > 80 || Rate is <= 0 ||
            !new[] { "client", "freelancer", "both" }.Contains(WorkRole) ||
            !new[] { "Available", "Busy", "Day off" }.Contains(Availability) || Days.Any(x => !validDays.Contains(x)))
        { Message = "Please check your work preferences and try again."; return RedirectToPage(); }
        if (!await db.Users.AnyAsync(x => x.Id == UserId)) { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
        var profile = await db.FreelancerProfiles.FindAsync(UserId);
        if (profile is null) { profile = new FreelancerProfile { UserId = UserId }; db.FreelancerProfiles.Add(profile); }
        profile.Service = Service; profile.Location = Location; profile.Rate = Rate; profile.WorkRole = WorkRole;
        profile.Availability = Availability; profile.AvailableDays = string.Join(',', validDays.Where(Days.Contains));
        await db.SaveChangesAsync(); Message = "Work preferences saved to the database.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostLogoutAsync() { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
}
