using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

public static class BacolorServiceAreas
{
    public const string AllAreas = "All 5 supported areas";
    public static IReadOnlyList<string> Names { get; } =
        Array.AsReadOnly(new[] { "Solana Casa Real", "Cabalantian", "Parulog", "Cabetican", "Magliman" });

    public static string? Match(string? name) =>
        string.Equals(AllAreas, name?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? AllAreas
            : Names.FirstOrDefault(area => string.Equals(area, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> InLocation(string location)
    {
        if (Match(location) == AllAreas) return Names;
        var normalized = " " + System.Text.RegularExpressions.Regex.Replace(location.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim() + " ";
        return Names.Where(area => normalized.Contains(" " + area.ToLowerInvariant() + " ", StringComparison.Ordinal));
    }
}

[Authorize]
public class SettingsModel(ApplicationDbContext db, ILogger<SettingsModel> logger) : PageModel
{
    public string DisplayName { get; private set; } = "";
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string EffectiveAvailability { get; private set; } = "Available";
    public string Email { get; private set; } = "";
    public DateTime? DateOfBirth { get; private set; }
    public string PhoneNumber { get; private set; } = "";
    public bool SharePhone { get; private set; }
    public bool ContactUnavailable { get; private set; }
    [BindProperty] public string? Service { get; set; }
    [BindProperty] public string? Location { get; set; }
    public static readonly string[] ServiceOptions = [.. ServiceCatalog.Options.Keys, ServiceCatalog.OtherCategory];
    [BindProperty] public string[] Services { get; set; } = [];
    [BindProperty] public string[] SpecialtyChoices { get; set; } = [];
    [BindProperty] public Dictionary<string, string?> CustomSpecialties { get; set; } = [];
    public Dictionary<string, string[]> SavedSpecialties { get; private set; } = [];
    [BindProperty] public string[] Areas { get; set; } = [];
    [BindProperty] public string? OtherService { get; set; }
    [BindProperty] public int? ServiceRate { get; set; }
    public int? SavedServiceRate { get; private set; }
    public bool HasRateRange { get; private set; }
    public List<string> AcceptedRoles { get; private set; } = [];
    [BindProperty] public string[] AcceptedAgreements { get; set; } = [];
    [BindProperty] public int AgreementVersion { get; set; }

    private Task<List<string>> LoadAcceptedRolesAsync() => db.Set<RoleAgreementAcceptance>()
        .Where(x => x.UserId == UserId && x.Version == RoleAgreementAcceptance.CurrentVersion)
        .Select(x => x.Role).ToListAsync(HttpContext.RequestAborted);

    [BindProperty] public string WorkRole { get; set; } = "client";
    [BindProperty] public TimeOnly? PreferredStart { get; set; }
    [BindProperty] public TimeOnly? PreferredEnd { get; set; }
    [BindProperty] public string Availability { get; set; } = "Available";
    [BindProperty] public string[] Days { get; set; } = [];
    [TempData] public string? Message { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == UserId);
        if (user is null) { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
        DisplayName = $"{user.FirstName} {user.LastName}".Trim(); Email = user.Username ?? ""; DateOfBirth = user.DateOfBirth;
        FirstName = user.FirstName;
        LastName = user.LastName;
        try {
            var contact = await db.Set<UserContact>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId);
            PhoneNumber = contact?.PhoneNumber ?? ""; SharePhone = contact?.ShareWithAssignedUser ?? false;
        } catch (MySqlConnector.MySqlException ex) { logger.LogWarning(ex, "Contact storage unavailable"); ContactUnavailable = true; }
        var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId);
        if (profile != null) { Service = profile.Service; Location = profile.Location; SavedSpecialties = ServiceCatalog.Read(profile.ServiceSpecialties);
            var savedServices = Service.Split(", ", StringSplitOptions.RemoveEmptyEntries);
            Services = savedServices.Select(value => value.StartsWith("Other/General: ", StringComparison.Ordinal) || !ServiceOptions.Contains(value) ? "Other/General" : value).Distinct().ToArray();
            OtherService = string.Join(", ", savedServices.Where(value => !ServiceOptions.Contains(value)).Select(value => value.StartsWith("Other/General: ", StringComparison.Ordinal) ? value["Other/General: ".Length..] : value));
            Areas = BacolorServiceAreas.InLocation(Location).ToArray(); HasRateRange = profile.Rate.HasValue && profile.RateMaximum > profile.Rate; SavedServiceRate = FreelancerProfile.GetServiceRate(profile.Rate, profile.RateMaximum); ServiceRate = SavedServiceRate; WorkRole = profile.WorkRole; Availability = profile.Availability; PreferredStart = profile.PreferredStart; PreferredEnd = profile.PreferredEnd; Days = profile.AvailableDays.Split(',', StringSplitOptions.RemoveEmptyEntries); }
        AcceptedRoles = await LoadAcceptedRolesAsync();
        EffectiveAvailability = await FreelancerAvailability.GetAsync(db, UserId, Availability, HttpContext.RequestAborted);
        return Page();
    }
    public async Task<IActionResult> OnPostContactAsync(string? phoneNumber, bool sharePhone, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        phoneNumber = (phoneNumber ?? "").Trim();
        if (phoneNumber.Length > 25 || (phoneNumber.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^\+?[0-9 ()-]{7,24}$")))
        { Message = "Enter a valid phone number, or leave it blank to remove it."; return RedirectToPage("/Settings", null, new { view = "profile", editPhone = true }, "contact-details"); }
        try {
            var contact = await db.Set<UserContact>().SingleOrDefaultAsync(x => x.UserId == UserId, ct);
            if (contact is null) { contact = new UserContact { UserId = UserId }; db.Set<UserContact>().Add(contact); }
            contact.PhoneNumber = phoneNumber; contact.ShareWithAssignedUser = phoneNumber.Length > 0 && sharePhone;
            await db.SaveChangesAsync(ct); Message = "Contact details saved.";
        } catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException) {
            logger.LogError(ex, "Could not save contact details"); Message = "Contact details could not be saved. Please try again.";
        }
        return RedirectToPage("/Settings", null, new { view = "profile", editPhone = true }, "contact-details");
    }
    public async Task<IActionResult> OnPostDisplayNameAsync(string? firstName, string? lastName, CancellationToken ct)
    {
        firstName = firstName?.Trim();
        lastName = lastName?.Trim();
        if (string.IsNullOrEmpty(firstName) || firstName.Length > 45 || lastName?.Length > 45)
        {
            Message = "Enter a first name and use up to 45 characters per name.";
            return RedirectToPage(new { view = "profile", editName = true });
        }

        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var ticket = await HttpContext.AuthenticateAsync(scheme);
        if (ticket.Principal?.Identity is not ClaimsIdentity identity || UserId == 0)
            return Challenge(scheme);

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == UserId, ct);
        if (user is null)
        {
            await HttpContext.SignOutAsync(scheme);
            return RedirectToPage("/Login");
        }

        user.FirstName = firstName;
        user.LastName = lastName ?? "";
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Could not save display name for user {UserId}.", UserId);
            Message = "Your display name could not be saved. Please try again.";
            return RedirectToPage(new { view = "profile", editName = true });
        }

        foreach (var claim in identity.FindAll(identity.NameClaimType).ToArray())
            identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(identity.NameClaimType, $"{user.FirstName} {user.LastName}".Trim()));
        await HttpContext.SignInAsync(scheme, ticket.Principal, ticket.Properties);
        Message = "Display name updated.";
        return RedirectToPage(new { view = "profile" });
    }

    public async Task<IActionResult> OnPostAvailabilityAsync()
    {
        if (UserId <= 0) return Challenge();
        var validDays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        Days ??= [];
        var fields = new[] { nameof(Days), nameof(Availability), nameof(PreferredStart), nameof(PreferredEnd) };
        if (ModelState.Any(entry => fields.Any(field => entry.Key == field || entry.Key.StartsWith(field + "[", StringComparison.Ordinal)) && entry.Value!.Errors.Count > 0) ||
            Days.Any(day => !validDays.Contains(day)) || !new[] { "Available", "Busy", "Day off" }.Contains(Availability) ||
            PreferredStart.HasValue != PreferredEnd.HasValue || PreferredStart >= PreferredEnd)
            return new JsonResult(new { success = false, message = "Check your days and enter both times, with the end after the start, or leave both blank." }) { StatusCode = 422 };
        try
        {
            var profile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, HttpContext.RequestAborted);
            if (profile is null || profile.WorkRole is not ("freelancer" or "both"))
                return new JsonResult(new { success = false, message = "Save your freelancer work role first, then update availability." }) { StatusCode = 422 };
            profile.AvailableDays = string.Join(',', validDays.Where(Days.Contains));
            profile.Availability = Availability;
            profile.PreferredStart = PreferredStart;
            profile.PreferredEnd = PreferredEnd;
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            return new JsonResult(new { success = true, message = "Availability saved." });
        }
        catch (Exception ex) when (ex is DbUpdateException or MySqlConnector.MySqlException)
        {
            logger.LogError(ex, "Could not save availability for user {UserId}.", UserId);
            return new JsonResult(new { success = false, message = "Availability could not be saved. Your selections are still here; please try again." }) { StatusCode = 503 };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var validDays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        OtherService = OtherService?.Trim();
        Days ??= [];
        if (!ModelState.IsValid || UserId == 0 || ServiceRate is <= 0 ||
            !new[] { "client", "freelancer", "both" }.Contains(WorkRole) ||
            !new[] { "Available", "Busy", "Day off" }.Contains(Availability) || Days.Any(x => !validDays.Contains(x)))
        { Message = "Please check your work preferences and try again."; return RedirectToPage(); }
        if ((WorkRole is "freelancer" or "both") &&
            (Services.Length == 0 || Services.Any(service => !ServiceOptions.Contains(service)) ||
             Areas.Length == 0 || Areas.Any(area => !BacolorServiceAreas.Names.Contains(area)) ||
             (Services.Contains("Other/General") && (string.IsNullOrWhiteSpace(OtherService) || OtherService.Length > 60 || OtherService.Contains(',')))))
        { Message = "Choose at least one service and area. For Other / General, enter a service name of up to 60 characters without commas."; return RedirectToPage(); }
        string? specialties = null;
        if ((WorkRole is "freelancer" or "both") && !ServiceCatalog.TryBuild(Services, SpecialtyChoices, CustomSpecialties, out specialties))
        { Message = "Check the selected specific services and enter any custom service names (up to 60 characters)."; return RedirectToPage(); }
        if ((WorkRole is "freelancer" or "both") &&
            (PreferredStart.HasValue != PreferredEnd.HasValue || PreferredStart >= PreferredEnd))
        {
            Message = "Enter both preferred times, with an end time after the start time, or leave both blank.";
            return RedirectToPage();
        }
        if (!await db.Users.AnyAsync(x => x.Id == UserId)) { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
        var accepted = await LoadAcceptedRolesAsync();
        string[] required = WorkRole == "both" ? ["client", "freelancer"] : [WorkRole];
        var missing = required.Except(accepted).ToArray();
        if (missing.Length > 0 && (AgreementVersion != RoleAgreementAcceptance.CurrentVersion ||
            missing.Any(role => !AcceptedAgreements.Contains(role))))
        {
            Message = "Review and accept the required role agreements before saving.";
            return RedirectToPage();
        }

        foreach (var role in missing)
        {
            db.Add(new RoleAgreementAcceptance { UserId = UserId, Role = role,
                Version = AgreementVersion, AcceptedAtUtc = DateTime.UtcNow });
        }
        var profile = await db.FreelancerProfiles.FindAsync(UserId);
        if (profile is null) { profile = new FreelancerProfile { UserId = UserId }; db.FreelancerProfiles.Add(profile); }
        profile.WorkRole = WorkRole;
        if (WorkRole is "freelancer" or "both")
        {
            profile.Service = string.Join(", ", ServiceOptions.Where(Services.Contains).Select(service => service == "Other/General" ? $"Other/General: {OtherService}" : service));
            profile.ServiceSpecialties = specialties;
            profile.Location = Areas.Distinct().Count() == BacolorServiceAreas.Names.Count ? BacolorServiceAreas.AllAreas : string.Join(", ", BacolorServiceAreas.Names.Where(Areas.Contains));
            var hasExistingRange = profile.Rate.HasValue && profile.RateMaximum > profile.Rate;
            if (ServiceRate.HasValue || !hasExistingRange)
            {
                profile.Rate = ServiceRate;
                profile.RateMaximum = ServiceRate;
            }
            profile.Availability = Availability;
            profile.PreferredStart = PreferredStart;
            profile.PreferredEnd = PreferredEnd;
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
        Message = "Work preferences saved.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostLogoutAsync() { if (LoginSecurity.Enabled(HttpContext.RequestServices.GetRequiredService<IConfiguration>())) await LoginSecurity.LogoutAsync(db, HttpContext, HttpContext.RequestAborted); await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
}
