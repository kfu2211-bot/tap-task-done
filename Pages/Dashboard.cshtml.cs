using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
using System.Security.Claims;

namespace tts.Pages
{
    public class DashboardModel(ApplicationDbContext db, ILogger<DashboardModel> logger) : PageModel
    {
        public record RatedProvider(string Name, string Service, double Average, int Count);
        public List<RatedProvider> RatedProviders { get; private set; } = [];
        public bool ReviewsUnavailable { get; private set; }
        public Dictionary<string, int> CategoryCounts { get; private set; } = new();
        public bool CategoriesUnavailable { get; private set; }
        public async Task OnGetAsync(CancellationToken ct)
        {
            try
            {
                var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                var services = await (from profile in db.FreelancerProfiles.AsNoTracking()
                    join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
                    where (profile.WorkRole == "freelancer" || profile.WorkRole == "both") && profile.UserId != userId
                        && profile.Service != "" && profile.Location != "" && profile.Availability == "Available"
                    select profile.Service).ToListAsync(ct);
                var terms = new Dictionary<string, string[]> {
                    ["Cleaning"] = ["clean"], ["Repairs"] = ["repair", "plumb", "electric"],
                    ["Errands"] = ["errand", "delivery"], ["Moving"] = ["moving", "mover", "hauling"],
                    ["Gardening"] = ["garden", "landscap"], ["Painting"] = ["paint"],
                    ["Tutoring"] = ["tutor", "teacher", "teaching"], ["Pet Care"] = ["pet care", "pet sit", "dog walk"] };
                CategoryCounts = terms.ToDictionary(x => x.Key,
                    x => services.Count(service => x.Value.Any(term => service.Contains(term, StringComparison.OrdinalIgnoreCase))));
                CategoryCounts["Others"] = services.Count(service =>
                    !terms.Values.Any(words => words.Any(word => service.Contains(word, StringComparison.OrdinalIgnoreCase))));
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogError(ex, "Unable to load category counts"); CategoriesUnavailable = true; }
            try
            {
                var rows = await (from review in db.MarketplaceReviews.AsNoTracking()
                    join user in db.Users.AsNoTracking() on review.FreelancerId equals user.Id
                    join profile in db.FreelancerProfiles.AsNoTracking() on user.Id equals profile.UserId
                    where profile.WorkRole == "freelancer" || profile.WorkRole == "both"
                    select new { user.Id, user.FirstName, user.LastName, profile.Service, review.Rating }).ToListAsync(ct);
                RatedProviders = rows.GroupBy(x => new { x.Id, x.FirstName, x.LastName, x.Service })
                    .Select(g => new RatedProvider(((g.Key.FirstName ?? "") + " " + (g.Key.LastName ?? "")).Trim(), g.Key.Service, g.Average(x => x.Rating), g.Count()))
                    .OrderByDescending(x => x.Average).ThenByDescending(x => x.Count).ThenBy(x => x.Name).Take(8).ToList();
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogError(ex, "Unable to load ratings"); ReviewsUnavailable = true; }
        }
    }
}
