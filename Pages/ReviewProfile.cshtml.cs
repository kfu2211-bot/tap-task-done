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
    public bool HasFreelancerProfile { get; private set; }
    public bool CanSwitchRatingRole { get; private set; }
    public string Location { get; private set; } = "";
    public Dictionary<string, string[]> ServiceGroups { get; private set; } = [];
    public int? ServiceRate { get; private set; }
    public int ProfileId { get; private set; }
    public List<Item> Reviews { get; private set; } = [];
    public bool Unavailable { get; private set; }
    public List<ServiceRatingItem> RatingItems { get; private set; } = [];
    public record Item(string Name, int Rating, string Feedback, DateTime CreatedAt);

    public async Task<IActionResult> OnGetAsync(int id, string? role, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || user.Role == "deleted") return NotFound();
        ProfileId = id;
        DisplayName = $"{user.FirstName} {user.LastName}".Trim();
        var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == id, ct);
        CanSwitchRatingRole = ServiceRatingRules.CanSwitchRoles(profile?.WorkRole);
        HasFreelancerProfile = profile?.WorkRole is "freelancer" or "both";
        ProfileRole = ServiceRatingRules.ResolveRole(profile?.WorkRole,role,HasFreelancerProfile ? "freelancer" : "client") == "client" ? "Client" : "Freelancer";
        if (ProfileRole == "Freelancer")
        {
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
            var rows = await (from review in db.MarketplaceReviews.AsNoTracking()
                join author in db.Users.AsNoTracking() on (review.ByFreelancer ? review.FreelancerId : review.ClientId) equals author.Id
                join task in db.MarketplaceTasks.AsNoTracking() on review.TaskId equals task.Id
                where review.ByFreelancer == byFreelancer && (byFreelancer ? review.ClientId : review.FreelancerId) == id
                orderby review.CreatedAt descending, review.Id descending
                select new { review.Id, review.Rating, review.Feedback, review.CreatedAt, task.Category, Name=(author.FirstName+" "+author.LastName).Trim() }).ToListAsync(ct);
            Reviews = rows.Select(r=>new Item(r.Name,r.Rating,r.Feedback,r.CreatedAt)).ToList();
            RatingItems = rows.Select(r=>new ServiceRatingItem(r.Id,0,"",r.Category,r.Name,Math.Clamp(r.Rating,1,5),r.Feedback,r.CreatedAt,byFreelancer ? "Freelancer → Client" : "Client → Freelancer",byFreelancer ? "client" : "freelancer")).ToList();
        }
        catch (MySqlConnector.MySqlException) { Unavailable = true; }
        return Page();
    }
}
