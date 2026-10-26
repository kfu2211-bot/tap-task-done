using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
using System.Security.Claims;

namespace tts.Pages
{
    public class FindPostModel(ApplicationDbContext db, ILogger<FindPostModel> logger) : PageModel
    {
        public bool DemoMode { get; private set; }
        public record Provider(int Id, string Name, string Service, string Location, int? Rate, string Availability);
        public record RatingSummary(double Average, int Count);
        public List<Provider> Providers { get; private set; } = [];
        public Dictionary<int, RatingSummary> Ratings { get; private set; } = [];
        public bool RatingsUnavailable { get; private set; }
        public List<MarketTasksModel.TaskItem> OpenTasks { get; private set; } = [];
        public List<MarketTasksModel.OfferItem> Offers { get; private set; } = [];
        public Dictionary<long, string> OfferTaskTitles { get; private set; } = [];
        public string WorkRole { get; private set; } = "client";
        [Microsoft.AspNetCore.Mvc.TempData] public string? Notice { get; set; }
        public bool DirectoryUnavailable { get; private set; }
        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            if (Request.Query["demo"] == "1")
            {
                DemoMode = true;
                WorkRole = "both"; // Read-only previews of both search directions, not an account role change.
                Providers = DemoSearchCatalog.Providers.Select(p => new Provider(p.Id, p.Name, p.Service, p.Location, p.Rate, p.Availability)).ToList();
                OpenTasks = DemoSearchCatalog.Tasks().Select(t => new MarketTasksModel.TaskItem(t, "[DEMO] Sample client")).ToList();
                return;
            }
            try
            {
                var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                WorkRole = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken))?.WorkRole ?? "client";
                if (userId != 0 && WorkRole is "freelancer" or "both")
                {
                    OpenTasks = await (from t in db.MarketplaceTasks.AsNoTracking()
                        join u in db.Users.AsNoTracking() on t.ClientId equals u.Id
                        where t.ClientId != userId && t.Status == "open" orderby t.Id descending
                        select new MarketTasksModel.TaskItem(t, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(cancellationToken);
                    Offers = await (from o in db.MarketplaceOffers.AsNoTracking()
                        join u in db.Users.AsNoTracking() on o.FreelancerId equals u.Id
                        join p in db.FreelancerProfiles.AsNoTracking() on u.Id equals p.UserId
                        where o.FreelancerId == userId
                        select new MarketTasksModel.OfferItem(o, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(), p.Service)).ToListAsync(cancellationToken);
                    var offerTaskIds = Offers.Select(o => o.Offer.TaskId).Distinct().ToList();
                    OfferTaskTitles = await db.MarketplaceTasks.AsNoTracking().Where(t => offerTaskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, cancellationToken);
                }
                Providers = await (from profile in db.FreelancerProfiles.AsNoTracking()
                    join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
                    where (profile.WorkRole == "freelancer" || profile.WorkRole == "both") && profile.UserId != userId
                        && profile.Service != "" && profile.Location != ""
                    orderby user.FirstName, user.Id
                    select new Provider(user.Id, ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(),
                        profile.Service, profile.Location, profile.Rate, profile.Availability)).ToListAsync(cancellationToken);
                try
                {
                    var reviews = await db.MarketplaceReviews.AsNoTracking().Select(r => new { r.FreelancerId, r.Rating }).ToListAsync(cancellationToken);
                    Ratings = reviews.GroupBy(r => r.FreelancerId).ToDictionary(g => g.Key, g => new RatingSummary(g.Average(r => r.Rating), g.Count()));
                }
                catch (MySqlConnector.MySqlException) { RatingsUnavailable = true; logger.LogWarning("Review ratings are unavailable"); }
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException || ex is InvalidOperationException)
            {
                logger.LogError(ex, "Unable to load freelancer directory");
                DirectoryUnavailable = true;
            }
        }
    }
}
