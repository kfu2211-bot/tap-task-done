using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
using System.Security.Claims;

namespace tts.Pages
{
    public class DashboardModel(ApplicationDbContext db, ILogger<DashboardModel> logger) : PageModel
    {
        public record RatedProvider(int Id, string Name, string Service, double Average, int Count) { public string Availability { get; init; } = "Available"; public bool CanRequest { get; init; } }
        public List<RatedProvider> RatedProviders { get; private set; } = [];
        public bool ReviewsUnavailable { get; private set; }
        public Dictionary<string, int> CategoryCounts { get; private set; } = new();
        public bool CategoriesUnavailable { get; private set; }
        public List<MarketplaceTask> AvailableTasks { get; private set; } = [];
        public bool ShowAvailableTasks { get; private set; }
        public bool FreelancerOnly { get; private set; }
        public bool TasksUnavailable { get; private set; }
        public async Task OnGetAsync(CancellationToken ct)
        {
            try
            {
                var viewerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                var role = await db.FreelancerProfiles.AsNoTracking().Where(p => p.UserId == viewerId).Select(p => p.WorkRole).SingleOrDefaultAsync(ct);
                ShowAvailableTasks = role is "freelancer" or "both";
                FreelancerOnly = role == "freelancer";
                if (ShowAvailableTasks)
                    AvailableTasks = await db.MarketplaceTasks.AsNoTracking()
                        .Where(t => t.ClientId != viewerId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow))
                        .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(4).ToListAsync(ct);
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogError(ex, "Unable to load available tasks on the dashboard"); TasksUnavailable = true; }
            try
            {
                var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                var profiles = await (from profile in db.FreelancerProfiles.AsNoTracking()
                    join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
                    where (profile.WorkRole == "freelancer" || profile.WorkRole == "both") && profile.UserId != userId
                        && profile.Service != "" && profile.Location != "" && profile.Availability == "Available"
                    select new { profile.Service, profile.Location, profile.Availability }).ToListAsync(ct);
                var services = profiles
                    .Where(p => BacolorServiceAreas.InLocation(p.Location).Any() &&
                        string.Equals(p.Availability, "Available", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Service).ToList();
                var terms = new Dictionary<string, string[]> {
                    ["Cleaning"] = ["clean"], ["Repairs"] = ["repair", "plumb", "electric"],
                    ["Errands"] = ["errand", "delivery"], ["Moving"] = ["moving", "mover", "hauling"],
                    ["Gardening"] = ["garden", "landscap"], ["Painting"] = ["paint"],
                    ["Tutoring"] = ["tutor", "teacher", "teaching"], ["Pet Care"] = ["pet care", "pet sit", "dog walk"] };
                CategoryCounts = terms.ToDictionary(x => x.Key,
                    x => services.Count(service => x.Value.Any(term => service.Contains(term, StringComparison.OrdinalIgnoreCase))));
                CategoryCounts["Others"] = services.Count(service =>
                    service.Contains("Other/General", StringComparison.OrdinalIgnoreCase) ||
                    !terms.Values.Any(words => words.Any(word => service.Contains(word, StringComparison.OrdinalIgnoreCase))));
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogError(ex, "Unable to load category counts"); CategoriesUnavailable = true; }
            try
            {
                RatedProviders = await LoadRatedProvidersAsync(db, 8, ct);
                var active = await FreelancerAvailability.AssignmentsAsync(db, RatedProviders.Select(p => p.Id).ToArray(), ct);
                RatedProviders = RatedProviders.Select(p => p.CanRequest ? p with { Availability = FreelancerAvailability.Effective(p.Availability, active.GetValueOrDefault(p.Id) ?? []) } : p).ToList();
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogError(ex, "Unable to load ratings"); ReviewsUnavailable = true; }
        }
        public static async Task<List<RatedProvider>> LoadRatedProvidersAsync(ApplicationDbContext db, int limit, CancellationToken ct)
        {
            var rows = await (from review in db.MarketplaceReviews.AsNoTracking()
                join user in db.Users.AsNoTracking() on review.FreelancerId equals user.Id
                join profile in db.FreelancerProfiles.AsNoTracking() on user.Id equals profile.UserId into profiles
                from profile in profiles.DefaultIfEmpty()
                where user.Role != "deleted"
                select new { user.Id, user.FirstName, user.LastName, profile.Service, profile.Location,
                    profile.WorkRole, profile.Availability, review.Rating }).ToListAsync(ct);
            return rows.GroupBy(x => x.Id).Select(group =>
            {
                var provider = group.First();
                var canRequest = (provider.WorkRole is "freelancer" or "both")
                    && !string.IsNullOrWhiteSpace(provider.Service) && !string.IsNullOrWhiteSpace(provider.Location);
                return new RatedProvider(provider.Id, ($"{provider.FirstName} {provider.LastName}").Trim(),
                    provider.Service ?? "", group.Average(x => x.Rating), group.Count())
                {
                    CanRequest = canRequest,
                    Availability = canRequest ? provider.Availability ?? "Unavailable" : "Unavailable"
                };
            }).OrderByDescending(x => x.Average).ThenByDescending(x => x.Count).ThenBy(x => x.Name).Take(limit).ToList();
        }
    }
}
