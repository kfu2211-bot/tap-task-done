using Microsoft.AspNetCore.Mvc;
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
        public bool CanSwitchView { get; private set; }
        public bool SummaryUnavailable { get; private set; }
        public int[] Summary { get; private set; } = new int[3];
        public string Availability { get; private set; } = "Unavailable";
        public record TaskPreview(MarketplaceTask Task, int Offers);
        public List<TaskPreview> RecentTasks { get; private set; } = [];
        public string TaskFilter { get; private set; } = "all";
        public int[] TaskCounts { get; private set; } = new int[4];
        public bool AcceptingJobs { get; private set; }
        public bool CanEditAvailability { get; private set; }
        public string PreferredHours { get; private set; } = "Not set";
        [TempData] public string? AvailabilityNotice { get; set; }
        public string AvailableDays { get; private set; } = "";
        public string ServiceArea { get; private set; } = "";
        public record ScheduledWork(long Id, string Title, string ScheduleText);
        public ScheduledWork? ScheduledJob { get; private set; }
        public bool TasksUnavailable { get; private set; }
        public async Task<IActionResult> OnPostAvailabilityAsync(bool? acceptingJobs, CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
                return Challenge();

            if (!acceptingJobs.HasValue) return BadRequest();

            try
            {
                var availability = acceptingJobs.Value ? "Available" : "Day off";
                var changed = await db.FreelancerProfiles
                    .Where(p => p.UserId == userId && (p.WorkRole == "freelancer" || p.WorkRole == "both"))
                    .ExecuteUpdateAsync(update => update.SetProperty(p => p.Availability, availability), ct);
                if (changed == 0) AvailabilityNotice = "Availability could not be changed for this account.";
            }
            catch (MySqlConnector.MySqlException ex)
            {
                logger.LogWarning(ex, "Unable to update freelancer availability");
                AvailabilityNotice = "Availability could not be saved. Please try again.";
            }

            return RedirectToPage(new { view = "freelancer" });
        }

        public async Task<IActionResult> OnGetFreelancerSummaryAsync(CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
                return Challenge();

            Response.Headers.CacheControl = "no-store";
            try
            {
                var canView = await db.FreelancerProfiles.AnyAsync(p => p.UserId == userId &&
                    (p.WorkRole == "freelancer" || p.WorkRole == "both"), ct);
                if (!canView) return Forbid();
                var counts = await LoadFreelancerSummaryAsync(userId, ct);
                var scheduled = await LoadScheduledWorkAsync(userId, ct);
                return new JsonResult(new
                {
                    counts,
                    scheduled = scheduled is null ? null : new
                    {
                        scheduled.Title,
                        scheduled.ScheduleText,
                        url = Url.Page("/Tracking", new { taskId = scheduled.Id })
                    }
                });
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Unable to refresh freelancer Home summary");
                return StatusCode(503);
            }
        }

        private async Task<int[]> LoadFreelancerSummaryAsync(int userId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var buckets = from offer in db.MarketplaceOffers.AsNoTracking()
                          join task in db.MarketplaceTasks.AsNoTracking() on offer.TaskId equals task.Id
                          where offer.FreelancerId == userId
                          select offer.Status == "pending" && task.Status == "open" &&
                              (task.PostExpiresUtc == null || task.PostExpiresUtc > now) ? 0
                              : offer.Status == "accepted" && task.Status != "cancelled" && task.Status != "expired"
                                  ? (task.Status == "completed" ? 2 : 1) : -1;
            var counts = await buckets.Where(bucket => bucket >= 0).GroupBy(bucket => bucket)
                .Select(group => new { Index = group.Key, Count = group.Count() }).ToListAsync(ct);
            var summary = new int[3];
            foreach (var count in counts) summary[count.Index] = count.Count;
            return summary;
        }

        private async Task<ScheduledWork?> LoadScheduledWorkAsync(int userId, CancellationToken ct)
        {
            var candidates = await (
                from agreement in db.Set<WorkAgreement>().AsNoTracking()
                join task in db.MarketplaceTasks.AsNoTracking() on agreement.TaskId equals task.Id
                where task.Status == "assigned" && agreement.ClientConfirmedAtUtc != null &&
                    agreement.FreelancerConfirmedAtUtc != null && agreement.ScheduleText != "" &&
                    !db.Set<WorkAgreement>().Any(next => next.TaskId == task.Id && next.Version > agreement.Version) &&
                    db.MarketplaceOffers.Any(o => o.TaskId == task.Id && o.FreelancerId == userId && o.Status == "accepted")
                orderby agreement.ClientConfirmedAtUtc descending, agreement.Id descending
                select new ScheduledWork(task.Id, task.Title, agreement.ScheduleText)).ToListAsync(ct);
            var now = DateTime.UtcNow.AddHours(8);
            var schedules = candidates.Where(job => !string.IsNullOrWhiteSpace(job.ScheduleText) &&
                !string.Equals(job.ScheduleText.Trim(), "To be agreed", StringComparison.OrdinalIgnoreCase))
                .Select(job => (Job: job, Start: ParseSchedule(job.ScheduleText))).ToList();
            return schedules.Where(entry => entry.Start >= now).OrderBy(entry => entry.Start)
                .Select(entry => entry.Job).FirstOrDefault()
                ?? schedules.FirstOrDefault(entry => entry.Start is null).Job;
        }

        private static DateTime? ParseSchedule(string text) =>
            DateTime.TryParseExact(text.Trim(),
                ["MMM d, yyyy 'at' h:mm tt '(PHT)'", "MMM d, yyyy, h:mm tt", "yyyy-MM-dd'T'HH:mm"],
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
                ? date : null;

        public async Task OnGetAsync(CancellationToken ct, string? view = null, string? filter = null)
        {
            try
            {
                var viewerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                var role = await db.FreelancerProfiles.AsNoTracking().Where(p => p.UserId == viewerId).Select(p => p.WorkRole).SingleOrDefaultAsync(ct);
                CanSwitchView = role == "both";
                if (CanSwitchView && view is "client" or "freelancer")
                    Response.Cookies.Append("tts-home-view-" + viewerId, view,
                        new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(90) });
                FreelancerOnly = role == "freelancer" || (CanSwitchView &&
                    (view is "client" or "freelancer" ? view : Request.Cookies["tts-home-view-" + viewerId]) == "freelancer");
                ShowAvailableTasks = FreelancerOnly;
                if (ShowAvailableTasks)
                    AvailableTasks = await db.MarketplaceTasks.AsNoTracking()
                        .Where(t => t.ClientId != viewerId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow))
                        .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(4).ToListAsync(ct);
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogError(ex, "Unable to load available tasks on the dashboard"); TasksUnavailable = true; }
            try
            {
                var viewer = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                if (FreelancerOnly)
                {
                    Summary = await LoadFreelancerSummaryAsync(viewer, ct);
                    var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == viewer, ct);
                    Availability = await FreelancerAvailability.GetAsync(db, viewer, profile?.Availability ?? "Unavailable", ct);
                    CanEditAvailability = profile is not null;
                    AcceptingJobs = profile?.Availability == "Available";
                    if (profile?.PreferredStart is { } start && profile.PreferredEnd is { } end)
                        PreferredHours = FormattableString.Invariant($"{start:h:mm tt} – {end:h:mm tt}");
                    AvailableDays = profile?.AvailableDays ?? "";
                    ServiceArea = profile?.Location ?? "";
                    ScheduledJob = await LoadScheduledWorkAsync(viewer, ct);
                }
                else
                {
                    var tasks = db.MarketplaceTasks.AsNoTracking().Where(t => t.ClientId == viewer && t.Status != "inquiry");
                    TaskFilter = filter is "confirm" or "active" or "completed" ? filter : "all";
                    var activeTasks = tasks.Where(t => t.Status == "assigned" || t.Status == "on-the-way" ||
                        t.Status == "arrived" || t.Status == "in-progress" || t.Status == "awaiting-confirmation");
                    var confirmationTasks = tasks.Where(t => t.Status == "pending-confirmation");
                    var completedTasks = tasks.Where(t => t.Status == "completed");
                    var recent = TaskFilter switch
                    {
                        "confirm" => confirmationTasks,
                        "active" => activeTasks,
                        "completed" => completedTasks,
                        _ => tasks
                    };
                    TaskCounts = [await tasks.CountAsync(ct), await confirmationTasks.CountAsync(ct),
                        await activeTasks.CountAsync(ct), await completedTasks.CountAsync(ct)];
                    RecentTasks = await recent.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(4)
                        .Select(t => new TaskPreview(t, db.MarketplaceOffers.Count(o => o.TaskId == t.Id && o.Status == "pending")))
                        .ToListAsync(ct);
                    Summary[0] = TaskCounts[1];
                    Summary[1] = await db.MarketplaceOffers.CountAsync(o => o.Status == "pending" &&
                        tasks.Any(t => t.Id == o.TaskId && t.Status == "open"), ct);
                    Summary[2] = await tasks.CountAsync(t => t.IsPrivate && (
                        t.Status == "assigned" || t.Status == "on-the-way" || t.Status == "arrived" ||
                        t.Status == "in-progress" || t.Status == "awaiting-confirmation"), ct);
                }
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogWarning(ex, "Unable to load Home summary"); SummaryUnavailable = true; }
            if (FreelancerOnly) return;

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
