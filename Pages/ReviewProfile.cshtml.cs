using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class ReviewProfileModel(ApplicationDbContext db) : PageModel
{
    public string DisplayName { get; private set; } = "";
    public string ProfileRole { get; private set; } = "";
    public string Availability { get; private set; } = "";
    public bool CanOpenBooking { get; private set; }
    public string Location { get; private set; } = "";
    public Dictionary<string, string[]> ServiceGroups { get; private set; } = [];
    public int? ServiceRate { get; private set; }
    public int ProfileId { get; private set; }
    public List<Item> Reviews { get; private set; } = [];
    public bool Unavailable { get; private set; }
    public record Item(string Name, int Rating, string Feedback, DateTime CreatedAt);

    public async Task<IActionResult> OnGetAsync(int id, string? role, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        ProfileId = id;
        DisplayName = $"{user.FirstName} {user.LastName}".Trim();
        ProfileRole = role == "client" ? "Client" : "Freelancer";
        if (ProfileRole == "Freelancer")
        {
            var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == id, ct);
            if (profile is null) return NotFound();
            Location = profile.Location;
            var specialties = ServiceCatalog.Read(profile.ServiceSpecialties);
            foreach (var category in profile.Service.Split(", ", StringSplitOptions.RemoveEmptyEntries))
            {
                var custom = category.StartsWith("Other/General: ", StringComparison.Ordinal);
                var label = custom ? "Other / General" : category;
                ServiceGroups[label] = custom ? [category["Other/General: ".Length..]] :
                    (specialties.GetValueOrDefault(category) ?? []).Select(ServiceCatalog.DisplayServiceName).ToArray();
            }
            ServiceRate = FreelancerProfile.GetServiceRate(profile.Rate, profile.RateMaximum);
            Availability = await FreelancerAvailability.GetAsync(db, id, profile.Availability, ct);
            CanOpenBooking = profile.WorkRole is "freelancer" or "both";
        }
        try
        {
            var byFreelancer = ProfileRole == "Client";
            Reviews = await (from review in db.MarketplaceReviews.AsNoTracking()
                join author in db.Users.AsNoTracking()
                    on (review.ByFreelancer ? review.FreelancerId : review.ClientId) equals author.Id
                where review.ByFreelancer == byFreelancer &&
                    (byFreelancer ? review.ClientId : review.FreelancerId) == id
                orderby review.CreatedAt descending, review.Id descending
                select new Item((author.FirstName + " " + author.LastName).Trim(),
                    review.Rating, review.Feedback, review.CreatedAt)).ToListAsync(ct);
        }
        catch (MySqlConnector.MySqlException) { Unavailable = true; }
        return Page();
    }
}
