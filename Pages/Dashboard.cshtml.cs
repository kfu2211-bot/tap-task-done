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
        public bool HasMatchingServices { get; private set; }
        public bool FreelancerOnly { get; private set; }
        public bool CanSwitchView { get; private set; }
        public bool SummaryUnavailable { get; private set; }
        public int[] Summary { get; private set; } = new int[3];
        public string Availability { get; private set; } = "Unavailable";
        public record TaskPreview(MarketplaceTask Task, int Offers);
        public List<TaskPreview> RecentTasks { get; private set; } = [];
        public string TaskFilter { get; private set; } = "all";
        public int[] TaskCounts { get; private set; } = new int[5];
        public bool AcceptingJobs { get; private set; }
        public bool CanEditAvailability { get; private set; }
        public string PreferredHours { get; private set; } = "Not set";
        [TempData] public string? AvailabilityNotice { get; set; }
        public string AvailableDays { get; private set; } = "";
        public string ServiceArea { get; private set; } = "";
        public record ScheduledWork(long Id, string Title, string ScheduleText)
        {
            public string Status { get; init; } = "Scheduled";
            public bool ClientConfirmed { get; init; }
            public bool FreelancerConfirmed { get; init; }
            public bool HasAgreement { get; init; }
            public string Confirmation => !HasAgreement ? "Work details needed" : !FreelancerConfirmed ? "Your confirmation needed" : !ClientConfirmed ? "Waiting for client confirmation" : "Work details confirmed";
            public string NextStep => !HasAgreement ? "Set the final work details together in Tracking."
                : !FreelancerConfirmed ? "Review the work details and confirm your side in Tracking."
                : !ClientConfirmed ? "Your confirmation is saved. Ask the client to confirm before travel."
                : "Both confirmed. Travel opens 1 hour before arrival.";
        }
        public ScheduledWork? ScheduledJob { get; private set; }
        public bool TasksUnavailable { get; private set; }
        public AccountSetup? Setup { get; private set; }
        public string SetupReminderKey { get; private set; } = "";

        private async Task LoadAccountSetupAsync(int userId, CancellationToken ct)
        {
            string? role = null, identity = null;
            List<string> accepted = [];
            var hasPhone = false;
            var roleAvailable = true; var identityAvailable = true; var phoneAvailable = true;
            try
            {
                role = await db.FreelancerProfiles.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.WorkRole).SingleOrDefaultAsync(ct);
                accepted = await db.Set<RoleAgreementAcceptance>().AsNoTracking()
                    .Where(a => a.UserId == userId && a.Version == RoleAgreementAcceptance.CurrentVersion).Select(a => a.Role).ToListAsync(ct);
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogWarning(ex, "Unable to load account setup role"); roleAvailable = false; }
            try
            {
                identity = await db.IdentityRequests.AsNoTracking().Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.Id).Select(r => r.Status).FirstOrDefaultAsync(ct);
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogWarning(ex, "Unable to load account setup verification"); identityAvailable = false; }
            try
            {
                var phone = await db.Set<UserContact>().AsNoTracking().Where(c => c.UserId == userId).Select(c => c.PhoneNumber).SingleOrDefaultAsync(ct);
                hasPhone = !string.IsNullOrWhiteSpace(phone);
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogWarning(ex, "Unable to load account setup contact status"); phoneAvailable = false; }
            Setup = AccountSetup.Create(role, accepted, identity, hasPhone, roleAvailable, identityAvailable, phoneAvailable);
            // A UI-only digest distinguishes sign-ins without exposing the login token or contact data.
            var issued = HttpContext.Features.Get<Microsoft.AspNetCore.Authentication.IAuthenticateResultFeature>()?.AuthenticateResult?.Properties?.IssuedUtc?.ToString("O");
            var signIn = User.FindFirst("login_session")?.Value ?? issued ?? "browser-session";
            SetupReminderKey = "tts-setup-v1-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{userId}:{signIn}")))[..24];
        }
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
                        ScheduleText = TaskSchedule.Display(scheduled.ScheduleText),
                        scheduled.Status,
                        scheduled.Confirmation,
                        scheduled.NextStep,
                        AgreementConfirmed = scheduled.ClientConfirmed && scheduled.FreelancerConfirmed,
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
            var jobs = await (from offer in db.MarketplaceOffers.AsNoTracking()
                              join task in db.MarketplaceTasks.AsNoTracking() on offer.TaskId equals task.Id
                              where offer.FreelancerId == userId &&
                                  (offer.Status == "pending" || offer.Status == "accepted")
                              select new { Task = task, offer.Status }).ToListAsync(ct);
            return [
                jobs.Count(j => j.Status == "pending" && j.Task.Status == "open" &&
                    (j.Task.PostExpiresUtc == null || j.Task.PostExpiresUtc > now)),
                jobs.Count(j => j.Status == "accepted" && j.Task.Status is not ("completed" or "cancelled" or "expired") &&
                    !TaskSchedule.IsUpcoming(j.Task, now)),
                jobs.Count(j => j.Status == "accepted" && j.Task.Status == "completed")
            ];
        }

        private async Task<ScheduledWork?> LoadScheduledWorkAsync(int userId, CancellationToken ct)
        {
            var candidates = await (from task in db.MarketplaceTasks.AsNoTracking()
                                    join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                                    where task.Status == "assigned" && offer.Status == "accepted" && offer.FreelancerId == userId
                                    select new ScheduledWork(task.Id, task.Title, task.ScheduleText)).ToListAsync(ct);
            var ids = candidates.Select(job => job.Id).ToArray();
            var agreements = (await db.Set<WorkAgreement>().AsNoTracking().Where(a => ids.Contains(a.TaskId))
                .OrderByDescending(a => a.Version).ToListAsync(ct)).GroupBy(a => a.TaskId).ToDictionary(g => g.Key, g => g.First());
            candidates = candidates.Select(job => agreements.TryGetValue(job.Id, out var terms)
                ? job with { HasAgreement = true, ClientConfirmed = terms.ClientConfirmedAtUtc.HasValue, FreelancerConfirmed = terms.FreelancerConfirmedAtUtc.HasValue } : job).ToList();
            var now = DateTime.UtcNow;
            return SelectScheduledWork(candidates, now);
        }

        public static ScheduledWork? SelectScheduledWork(IEnumerable<ScheduledWork> candidates, DateTime now) =>
            candidates.Select(job => (Job: job, Start: TaskSchedule.StartUtc(job.ScheduleText)))
                .OrderBy(entry => entry.Start is null ? 2 : entry.Start > now ? 1 : 0)
                .ThenBy(entry => entry.Start).ThenBy(entry => entry.Job.Id)
                .Select(entry => entry.Job with { Status = entry.Start.HasValue ? TaskSchedule.ScheduleLabel(entry.Start.Value, now) : "Schedule needed" }).FirstOrDefault();

        public async Task OnGetAsync(CancellationToken ct, string? view = null, string? filter = null)
        {
            if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var setupUserId) && setupUserId > 0)
            {
                Response.Headers.CacheControl = "no-store";
                await LoadAccountSetupAsync(setupUserId, ct);
            }
            try
            {
                var viewerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                var matchingProfile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == viewerId, ct);
                var role = matchingProfile?.WorkRole;
                CanSwitchView = role == "both";
                if (CanSwitchView && view is "client" or "freelancer")
                    Response.Cookies.Append("tts-home-view-" + viewerId, view,
                        new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(90) });
                FreelancerOnly = role == "freelancer" || (CanSwitchView &&
                    (view is "client" or "freelancer" ? view : Request.Cookies["tts-home-view-" + viewerId]) == "freelancer");
                ShowAvailableTasks = FreelancerOnly;
                if (ShowAvailableTasks)
                {
                    HasMatchingServices = !string.IsNullOrWhiteSpace(matchingProfile?.Service);
                    if (HasMatchingServices)
                    {
                        // Match before limiting, so unrelated recent posts cannot hide older matching work.
                        var candidates = db.MarketplaceTasks.AsNoTracking()
                        .Where(t => t.ClientId != viewerId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow))
                        .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id);
                        await foreach (var task in candidates.AsAsyncEnumerable().WithCancellation(ct))
                        {
                            if (!ServiceCatalog.Matches(matchingProfile!.Service, matchingProfile.ServiceSpecialties, task)) continue;
                            AvailableTasks.Add(task);
                            if (AvailableTasks.Count == 4) break;
                        }
                    }
                }
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
                    TaskFilter = filter is "confirm" or "active" or "completed" or "scheduled" ? filter : "all";
                    var now = DateTime.UtcNow;
                    var assigned = await tasks.Where(t => t.Status == "assigned").ToListAsync(ct);
                    var scheduledIds = assigned.Where(t => TaskSchedule.IsUpcoming(t, now)).Select(t => t.Id).ToArray();
                    var scheduledTasks = tasks.Where(t => scheduledIds.Contains(t.Id));
                    var activeTasks = tasks.Where(t => (t.Status == "assigned" && !scheduledIds.Contains(t.Id)) || t.Status == "on-the-way" ||
                        t.Status == "arrived" || t.Status == "in-progress" || t.Status == "awaiting-confirmation");
                    var confirmationTasks = tasks.Where(t => t.Status == "pending-confirmation");
                    var completedTasks = tasks.Where(t => t.Status == "completed");
                    var recent = TaskFilter switch
                    {
                        "confirm" => confirmationTasks,
                        "active" => activeTasks,
                        "scheduled" => scheduledTasks,
                        "completed" => completedTasks,
                        _ => tasks
                    };
                    TaskCounts = [await tasks.CountAsync(ct), await confirmationTasks.CountAsync(ct),
                        await activeTasks.CountAsync(ct), await completedTasks.CountAsync(ct), scheduledIds.Length];
                    RecentTasks = await recent.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(4)
                        .Select(t => new TaskPreview(t, db.MarketplaceOffers.Count(o => o.TaskId == t.Id && o.Status == "pending")))
                        .ToListAsync(ct);
                    Summary[0] = TaskCounts[1];
                    Summary[1] = await db.MarketplaceOffers.CountAsync(o => o.Status == "pending" &&
                        tasks.Any(t => t.Id == o.TaskId && t.Status == "open"), ct);
                    Summary[2] = await activeTasks.CountAsync(t => t.IsPrivate);
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
            var rows = await (from review in db.MarketplaceReviews.AsNoTracking().Where(r => !r.ByFreelancer)
                join user in db.Users.AsNoTracking() on review.FreelancerId equals user.Id
                join profile in db.FreelancerProfiles.AsNoTracking() on user.Id equals profile.UserId into profiles
                from profile in profiles.DefaultIfEmpty()
                where user.Role != "deleted"
                select new { user.Id, user.FirstName, user.LastName, profile.Service, profile.Location,
                    profile.WorkRole, profile.Availability, review.Rating }).ToListAsync(ct);
            var assignments = await FreelancerAvailability.AssignmentsAsync(db, rows.Select(x => x.Id).Distinct().ToArray(), ct);
            return rows.GroupBy(x => x.Id).Select(group =>
            {
                var provider = group.First();
                var canRequest = (provider.WorkRole is "freelancer" or "both")
                    && !string.IsNullOrWhiteSpace(provider.Service) && !string.IsNullOrWhiteSpace(provider.Location);
                return new RatedProvider(provider.Id, ($"{provider.FirstName} {provider.LastName}").Trim(),
                    provider.Service ?? "", group.Average(x => x.Rating), group.Count())
                {
                    CanRequest = canRequest,
                    Availability = canRequest ? FreelancerAvailability.Effective(provider.Availability ?? "Unavailable",
                        assignments.GetValueOrDefault(provider.Id) ?? []) : "Unavailable"
                };
            }).OrderByDescending(x => x.Average).ThenByDescending(x => x.Count).ThenBy(x => x.Name).Take(limit).ToList();
        }
    }
}
