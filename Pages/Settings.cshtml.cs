using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

public static class BacolorServiceAreas
{
    public static IReadOnlyList<string> Names { get; } =
        Array.AsReadOnly(new[] { "Solana Casa Real", "Cabalantian", "Parulog", "Cabetican", "Magliman" });

    public static string? Match(string? name) =>
        Names.FirstOrDefault(area => string.Equals(area, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> InLocation(string location)
    {
        var normalized = " " + System.Text.RegularExpressions.Regex.Replace(location.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim() + " ";
        return Names.Where(area => normalized.Contains(" " + area.ToLowerInvariant() + " ", StringComparison.Ordinal));
    }
}

[Authorize]
public class SettingsModel(ApplicationDbContext db, ILogger<SettingsModel> logger) : PageModel
{
    public string DisplayName { get; private set; } = "";
    public string EffectiveAvailability { get; private set; } = "Available";
    public string Email { get; private set; } = "";
    public DateTime? DateOfBirth { get; private set; }
    [BindProperty] public string? Service { get; set; }
    [BindProperty] public string? Location { get; set; }
    public string? SelectedServiceArea => BacolorServiceAreas.Match(Location?.Split(',')[0]);
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
        DisplayName = $"{user.FirstName} {user.LastName}".Trim(); Email = user.Username ?? ""; DateOfBirth = user.DateOfBirth;
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
        var serviceArea = SelectedServiceArea;
        if ((WorkRole is "freelancer" or "both") && serviceArea is null)
        { Message = "Choose one of the five supported barangays in Bacolor before saving."; return RedirectToPage(); }
        if (!await db.Users.AnyAsync(x => x.Id == UserId)) { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
        var profile = await db.FreelancerProfiles.FindAsync(UserId);
        if (profile is null) { profile = new FreelancerProfile { UserId = UserId }; db.FreelancerProfiles.Add(profile); }
        profile.WorkRole = WorkRole;
        if (WorkRole is "freelancer" or "both")
        {
            profile.Service = Service;
            profile.Location = serviceArea!;
            profile.Rate = Rate;
            profile.Availability = Availability;
            profile.AvailableDays = string.Join(',', validDays.Where(Days.Contains));
        }
        try
        {
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Could not save work preferences for user {UserId}.", UserId);
            Message = "Your changes could not be saved. Your previous work role is still active.";
            return RedirectToPage();
        }
        Message = "Work preferences saved to the database.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostLogoutAsync() { if (LoginSecurity.Enabled(HttpContext.RequestServices.GetRequiredService<IConfiguration>())) await LoginSecurity.LogoutAsync(db, HttpContext, HttpContext.RequestAborted); await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
}
