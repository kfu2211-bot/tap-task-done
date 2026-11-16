# Tap, Task, Done — Source Code Appendix

Selected implementation files from the web application. Each listing preserves the source from its indicated repository path. The listings form a documentation appendix and reference other components in the full project.

## Posting and browsing tasks

### Pages/FindPost.cshtml.cs

~~~~csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
using System.Security.Claims;

namespace tts.Pages
{
    public class FindPostModel(ApplicationDbContext db, ILogger<FindPostModel> logger) : PageModel
    {
        public record Provider(int Id, string Name, string Service, string Location, int? Rate, int? RateMaximum, string SavedAvailability)
        {
            public string Availability { get; init; } = SavedAvailability;
        }
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
        public bool IdentityApproved { get; private set; }
        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
                IdentityApproved = await IdentityWorkGate.IsApprovedAsync(db, userId, cancellationToken);
                WorkRole = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken))?.WorkRole ?? "client";
                if (userId != 0 && WorkRole is "freelancer" or "both")
                {
                    OpenTasks = await (from t in db.MarketplaceTasks.AsNoTracking()
                        join u in db.Users.AsNoTracking() on t.ClientId equals u.Id
                        where t.ClientId != userId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow) orderby t.Id descending
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
                        profile.Service, profile.Location, profile.Rate, profile.RateMaximum, profile.Availability)).ToListAsync(cancellationToken);
                var active = await FreelancerAvailability.AssignmentsAsync(db, Providers.Select(p => p.Id).ToArray(), cancellationToken);
                Providers = Providers.Select(p => p with { Availability = FreelancerAvailability.Effective(p.Availability, active.GetValueOrDefault(p.Id) ?? []) }).ToList();
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
~~~~

### Pages/MarketTasks.cshtml.cs

~~~~csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tts.Data;

namespace tts.Pages;

[Authorize]
public sealed class MarketTasksModel(ApplicationDbContext db, IWebHostEnvironment environment) : PageModel
{
    public record TaskItem(MarketplaceTask Task, string ClientName);
    public record OfferItem(MarketplaceOffer Offer, string Name, string Service);
    public record BookingItem(MarketplaceBooking Booking, string Name);
    public List<TaskItem> MyTasks { get; private set; } = [];
    public List<TaskItem> OpenTasks { get; private set; } = [];
    public List<OfferItem> Offers { get; private set; } = [];
    public Dictionary<long, string> OfferTaskTitles { get; private set; } = [];
    public List<BookingItem> Bookings { get; private set; } = [];
    public Dictionary<long, MarketplaceReview> Reviews { get; private set; } = [];
    public Dictionary<long, TaskDestination> Destinations { get; private set; } = [];
    public bool ReviewsUnavailable { get; private set; }
    public string WorkRole { get; private set; } = "client";
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public int CurrentUserId => UserId;
    public static bool CanEditTask(string status, int ownerId, int currentUserId) => currentUserId > 0 && ownerId == currentUserId && status == "pending-confirmation";
    public static bool ValidTaskDetails(string title, string description, string category, string location, decimal budget, string schedule) =>
        title.Length is >= 1 and <= 120 && description.Length is >= 1 and <= 5000 && category.Length is >= 1 and <= 60 &&
        location.Length is >= 1 and <= 120 && schedule.Length <= 120 && budget is > 0 and <= 9999999999m;

    public static DateOnly TodayInPhilippines => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

    private static DateTime? ClosingTimeUtc(DateOnly? date) =>
        date?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-8);

    public async Task<IActionResult> OnPostEditAsync(long id, string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude = null, double? longitude = null, decimal? budgetMaximum = null, int? postDurationDays = null, DateOnly? postEndDate = null, string? serviceArea = null, string? arrivalInstructions = null)
    {
        if (UserId <= 0) return Challenge();
        arrivalInstructions = arrivalInstructions?.Trim();
        if (arrivalInstructions?.Length > 500 || !await TaskLocationRules.MatchesAreaAsync(HttpContext, serviceArea, latitude, longitude, ct))
        { Notice = "Choose a verified pin in the selected Bacolor service area and keep arrival instructions under 500 characters."; return RedirectToPage(new { view = "mine" }); }
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim();
        location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (ModelState.TryGetValue(nameof(postEndDate), out var dateState) && dateState.Errors.Count > 0 ||
            postEndDate is { } endDate && (endDate < TodayInPhilippines || endDate == DateOnly.MaxValue))
        {
            Notice = "Choose a valid closing date today or later.";
            return RedirectToPage(new { view = "mine" });
        }

        if (!ModelState.IsValid || (postDurationDays is not (null or 3 or 7)) || (!ValidTaskDetails(title, description, category, location, budget, schedule) || !ValidBudgetMaximum(budget, budgetMaximum) || !TaskLocationRules.Valid(latitude, longitude)))
        { Notice = "Check the task details. The maximum budget must be at least the minimum, and the location pin must be confirmed."; return RedirectToPage(new { view = "mine" }); }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot edit tasks while suspended."; return RedirectToPage(new { view = "mine" }); }
        var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct);
        var task = tasks.SingleOrDefault();
        if (task is null || !CanEditTask(task.Status, task.ClientId, UserId)) { Notice = "This task cannot be edited. It may already be confirmed or cancelled."; return RedirectToPage(new { view = "mine" }); }
        task.Title = title; task.Description = description; task.Category = category; task.Location = location; task.Budget = budget; task.BudgetMaximum = budgetMaximum; task.ScheduleText = schedule;
        task.PostDurationDays = postDurationDays;
        task.ArrivalInstructions = arrivalInstructions;
        task.PostExpiresUtc = ClosingTimeUtc(postEndDate);
        TaskLocationRules.Save(db, id, await db.Set<TaskDestination>().SingleOrDefaultAsync(d => d.TaskId == id, ct), latitude!.Value, longitude!.Value);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        Notice = "Task and destination updated. Review the details, then confirm to publish.";
        return RedirectToPage(new { view = "mine" });
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var openedAt = DateTime.UtcNow;
        if (UserId == 0 || !await db.Users.AnyAsync(x => x.Id == UserId, ct)) return Challenge();
        WorkRole = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct))?.WorkRole ?? "client";
        MyTasks = await (from t in db.MarketplaceTasks.AsNoTracking()
                    join u in db.Users.AsNoTracking() on t.ClientId equals u.Id
                    where t.ClientId == UserId && t.Status != "inquiry" orderby t.Id descending
                    select new TaskItem(t, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(ct);
        var ownIds = MyTasks.Select(t => t.Task.Id).ToArray();
        Destinations = await db.Set<TaskDestination>().AsNoTracking().Where(d => ownIds.Contains(d.TaskId)).ToDictionaryAsync(d => d.TaskId, ct);
        if (WorkRole is "freelancer" or "both")
            OpenTasks = await (from t in db.MarketplaceTasks.AsNoTracking()
                join u in db.Users.AsNoTracking() on t.ClientId equals u.Id
                where t.ClientId != UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow) orderby t.Id descending
                select new TaskItem(t, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(ct);
        Offers = await (from o in db.MarketplaceOffers.AsNoTracking()
            join t in db.MarketplaceTasks.AsNoTracking() on o.TaskId equals t.Id
            join u in db.Users.AsNoTracking() on o.FreelancerId equals u.Id
            join p in db.FreelancerProfiles.AsNoTracking() on u.Id equals p.UserId
            where t.ClientId == UserId || o.FreelancerId == UserId
            select new OfferItem(o, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(), p.Service)).ToListAsync(ct);
        var offerTaskIds = Offers.Select(o => o.Offer.TaskId).Distinct().ToList();
        OfferTaskTitles = await db.MarketplaceTasks.AsNoTracking().Where(t => offerTaskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        Bookings = await (from b in db.MarketplaceBookings.AsNoTracking()
            join t in db.MarketplaceTasks.AsNoTracking() on b.TaskId equals t.Id
            join u in db.Users.AsNoTracking() on b.FreelancerId equals u.Id
            where t.ClientId == UserId
            select new BookingItem(b, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(ct);
        try { Reviews = await db.MarketplaceReviews.AsNoTracking().Where(r => r.ClientId == UserId).ToDictionaryAsync(r => r.TaskId, ct); }
        catch (MySqlException) { ReviewsUnavailable = true; }
        var view = Request.Query["view"].ToString();
        if (view is not ("jobs" or "post") && ownIds.Length > 0)
        {
            Response.Headers.CacheControl = "no-store";
            await db.MarketplaceNotifications
                .Where(n => n.UserId == UserId && !n.IsRead && n.CreatedAt <= openedAt
                    && MarketplaceNotification.TaskKinds.Contains(n.Kind) && ownIds.Contains(n.TaskId))
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.IsRead, true), ct);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(long id, int rating, string? feedback, bool fromTracking, CancellationToken ct)
    {
        IActionResult ReviewReturn() => fromTracking ? RedirectToPage("/Tracking", new { taskId = id }) : RedirectToPage(new { view = "mine" });
        feedback = (feedback ?? "").Trim();
        if (UserId == 0 || !ModelState.IsValid || rating is < 1 or > 5 || feedback.Length > 1000)
        { Notice = "Choose a rating from 1 to 5 and check your feedback."; return ReviewReturn(); }
        try
        {
            var freelancerId = await (from task in db.MarketplaceTasks.AsNoTracking()
                join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                where task.Id == id && task.ClientId == UserId && task.Status == "completed" && offer.Status == "accepted"
                select (int?)offer.FreelancerId).SingleOrDefaultAsync(ct);
            if (freelancerId is null || freelancerId == UserId || await db.MarketplaceReviews.AnyAsync(r => r.TaskId == id, ct))
            { Notice = "This task cannot be reviewed, or already has a review."; return ReviewReturn(); }
            db.MarketplaceReviews.Add(new MarketplaceReview { TaskId = id, ClientId = UserId, FreelancerId = freelancerId.Value, Rating = rating, Feedback = feedback, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); Notice = "Review saved. Thank you for your feedback.";
            if (fromTracking) return ReviewReturn();
            return RedirectToPage("/Reviews", new { view = "given" });
        }
        catch (DbUpdateException) { Notice = "Review could not be saved. It may already have been submitted."; }
        catch (MySqlException) { Notice = "Reviews are unavailable. Check that the review table has been created."; }
        return ReviewReturn();
    }

    public static bool ValidBudgetMaximum(decimal minimum, decimal? maximum) => maximum is null || maximum >= minimum && maximum <= 9999999999m && decimal.Round(maximum.Value, 2) == maximum;

    public async Task<IActionResult> OnPostCreateAsync(string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude = null, double? longitude = null, decimal? budgetMaximum = null, int? postDurationDays = null, DateOnly? postEndDate = null, string? serviceArea = null, string? arrivalInstructions = null)
    {
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim(); location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (UserId <= 0) return Challenge();
        arrivalInstructions = arrivalInstructions?.Trim();
        if (arrivalInstructions?.Length > 500 || !await TaskLocationRules.MatchesAreaAsync(HttpContext, serviceArea, latitude, longitude, ct))
        { Notice = "Choose a verified pin in the selected Bacolor service area and keep arrival instructions under 500 characters."; return RedirectToPage(new { view = "mine" }); }
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        if (ModelState.TryGetValue(nameof(postEndDate), out var dateState) && dateState.Errors.Count > 0 ||
            postEndDate is { } endDate && (endDate < TodayInPhilippines || endDate == DateOnly.MaxValue))
        {
            Notice = "Choose a valid closing date today or later.";
            return RedirectToPage(new { view = "mine" });
        }

        if (!ModelState.IsValid || postDurationDays is not (null or 3 or 7) || !ValidTaskDetails(title, description, category, location, budget, schedule) || !ValidBudgetMaximum(budget, budgetMaximum) || !TaskLocationRules.Valid(latitude, longitude))
        { Notice = "Complete the task details, ensure the maximum budget is at least the minimum, and confirm the destination pin."; return RedirectToPage(); }
        var role = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct))?.WorkRole ?? "client";
        if (role is not ("client" or "both")) { Notice = "Choose Client or Client & Freelancer in Settings to post tasks."; return RedirectToPage(); }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot create new tasks while restricted or pending account deletion."; return RedirectToPage(); }
        // Serialize task creation by this client so simultaneous retries see the first draft.
        await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {UserId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var duration = postDurationDays ?? 7;
        var closingTime = ClosingTimeUtc(postEndDate);
        var recent = DateTime.UtcNow.AddMinutes(-2);
        var duplicate = await (from draft in db.MarketplaceTasks.AsNoTracking()
            join pin in db.Set<TaskDestination>().AsNoTracking() on draft.Id equals pin.TaskId
            where draft.ClientId == UserId && !draft.IsPrivate && draft.Status == "pending-confirmation" &&
                draft.CreatedAt >= recent && draft.Title == title && draft.Description == description &&
                draft.Category == category && draft.Location == location && draft.ArrivalInstructions == arrivalInstructions && draft.Budget == budget &&
                draft.BudgetMaximum == budgetMaximum && draft.ScheduleText == schedule &&
                draft.PostDurationDays == duration && draft.PostExpiresUtc == closingTime &&
                pin.Latitude == latitude!.Value && pin.Longitude == longitude!.Value
            select draft.Id).AnyAsync(ct);
        if (duplicate)
        {
            Notice = "This task was already saved. Review it below before publishing.";
            return RedirectToPage();
        }
        var task = new MarketplaceTask { ClientId = UserId, Title = title, Description = description, Category = category, Location = location, ArrivalInstructions = arrivalInstructions, Budget = budget, BudgetMaximum = budgetMaximum, PostDurationDays = duration, PostExpiresUtc = closingTime, ScheduleText = schedule, Status = "pending-confirmation", CreatedAt = DateTime.UtcNow };
        db.MarketplaceTasks.Add(task); await db.SaveChangesAsync(ct);
        TaskLocationRules.Save(db, task.Id, null, latitude!.Value, longitude!.Value);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = "Task saved. Confirm it below to make it visible to freelancers."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostConfirmAsync(long id, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot confirm new tasks while restricted or pending account deletion."; return RedirectToPage(); }
        if (!await db.Set<TaskDestination>().AnyAsync(d => d.TaskId == id && d.Latitude >= 14.92 && d.Latitude <= 15.10 && d.Longitude >= 120.54 && d.Longitude <= 120.73, ct)) { Notice = "Edit this draft and confirm a work-location pin before publishing."; return RedirectToPage(new { view = "mine" }); }
        var now = DateTime.UtcNow;
        var threeDays = now.AddDays(3); var sevenDays = now.AddDays(7);
        var count = await db.MarketplaceTasks.Where(t => t.Id == id && t.ClientId == UserId && t.Status == "pending-confirmation" && (t.PostExpiresUtc == null || t.PostExpiresUtc > now)).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "open").SetProperty(t => t.PostExpiresUtc, t => t.PostExpiresUtc ?? (t.PostDurationDays == 3 ? threeDays : sevenDays)), ct);
        await tx.CommitAsync(ct);
        Notice = count == 1 ? "Task is now open for offers." : "Task could not be confirmed. If the closing date has passed, edit the draft and choose a new date."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostCancelAsync(long id, string? reason, CancellationToken ct)
    {
        reason = (reason ?? "").Trim();
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid || reason.Length is < 1 or > 500)
        { Notice = "Please provide a cancellation reason (up to 500 characters)."; return RedirectToPage(new { view = "mine" }); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.ClientId == UserId &&
            (t.Status == "pending-confirmation" || t.Status == "booking-pending" || t.Status == "open" && !t.IsPrivate || t.Status == "assigned" || t.Status == "on-the-way" || t.Status == "arrived" || t.Status == "in-progress" || t.Status == "awaiting-confirmation"), ct);
        if (task is null)
        { Notice = "This task cannot be cancelled. It may already be completed or cancelled."; return RedirectToPage(new { view = "mine" }); }
        var recipients = await db.MarketplaceOffers.Where(o => o.TaskId == id && (o.Status == "pending" || o.Status == "accepted"))
            .Select(o => o.FreelancerId).Union(db.MarketplaceBookings.Where(b => b.TaskId == id && b.Status == "pending").Select(b => b.FreelancerId)).Distinct().ToListAsync(ct);
        task.Status = "cancelled";
        task.CancellationReason = reason;
        await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "cancelled"), ct);
        await db.MarketplaceBookings.Where(b => b.TaskId == id && b.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "cancelled"), ct);
        foreach (var recipient in recipients)
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(recipient, id, "cancelled", "The client cancelled a task. Open My Jobs to see the reason."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "Task cancelled. Affected freelancers can see the reason.";
        return RedirectToPage(new { view = "mine" });
    }
    public async Task<IActionResult> OnPostConfirmCompletionAsync(long id, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        TaskProgress.Result result;
        try { result = await TaskProgress.AdvanceAsync(db, UserId, id, "confirm", null, null, null, ct); }
        catch (MySqlConnector.MySqlException) { result = new(false, "Progress storage is unavailable. Please check the database setup."); }
        Notice = result.Message;
        return RedirectToPage("/Tracking", new { taskId = id });
    }

    public async Task<IActionResult> OnPostOfferAsync(long id, decimal price, string? message, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        message = (message ?? "").Trim();
        var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct);
        if (profile is null || profile.WorkRole is not ("freelancer" or "both") || profile.Availability != "Available" || !ModelState.IsValid || price <= 0 || price > 9999999999m || message.Length > 500)
        { Notice = "Check your freelancer role, availability and offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot send offers while restricted or pending account deletion."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        if (await FreelancerAvailability.IsBusyAsync(db, UserId, ct)) { Notice = "Finish your current travel/work before sending a new offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.ClientId != UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow), ct);
        if (task is null || await db.MarketplaceOffers.AnyAsync(o => o.TaskId == id && o.FreelancerId == UserId, ct)) { Notice = "This task is unavailable or you already sent an offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, task.ClientId)) { Notice = "This task cannot receive offers while an account is pending deletion or restricted."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        await AgreementRules.ConversationAsync(db, id, UserId, ct);
        db.MarketplaceOffers.Add(new MarketplaceOffer { TaskId = id, FreelancerId = UserId, Price = price, Message = message, Status = "pending", CreatedAt = DateTime.UtcNow });
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, id, "offer", "A freelancer sent an offer for your task."));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); Notice = "Offer sent to the client."; }
        catch (DbUpdateException) { Notice = "Your offer could not be saved. Please try again."; }
        return RedirectToPage("/FindPost", new { mode = "jobs" });
    }
    public async Task<IActionResult> OnPostAcceptAsync(long id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var offer = await db.MarketplaceOffers.SingleOrDefaultAsync(o => o.Id == id && o.Status == "pending", ct);
        var task = offer is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == offer.TaskId && t.ClientId == UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow), ct);
        if (task is null || offer is null) { Notice = "This offer is no longer available."; return RedirectToPage(); }
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, offer.FreelancerId)) { Notice = "This offer cannot be accepted while either account is restricted or has a pending deletion request."; return RedirectToPage(); }
        task.Status = "assigned"; offer.Status = "accepted";
        await AgreementRules.ConversationAsync(db, task.Id, offer.FreelancerId, ct);
        await AgreementRules.InitialAsync(db, task, offer.Price, UserId, ct);
        db.Set<TaskProgressEvent>().Add(new TaskProgressEvent { TaskId = task.Id, ActorId = UserId, Status = "assigned", CreatedAtUtc = DateTime.UtcNow });
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(offer.FreelancerId, task.Id, "accepted", "The client accepted your offer."));
        await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.Id != offer.Id && o.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "declined"), ct);
        await db.MarketplaceBookings.Where(b => b.TaskId == task.Id && b.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "unavailable"), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); Notice = "Freelancer selected. Open Tracking to follow the job."; return RedirectToPage();
    }
}
~~~~

### Pages/TaskDetails.cshtml.cs

~~~~csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class TaskDetailsModel(ApplicationDbContext db) : PageModel
{
    public MarketplaceTask Work { get; private set; } = null!;
    public long ConversationId { get; private set; }
    public bool IsOwner { get; private set; }
    public bool IsAssigned { get; private set; }

    public async Task<IActionResult> OnGetAsync(long taskId, long conversationId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Challenge();

        var result = await (from conversation in ConversationAccess.VisibleTo(db, userId)
            join task in db.MarketplaceTasks.AsNoTracking() on conversation.TaskId equals task.Id
            where conversation.Id == conversationId && task.Id == taskId
            select new
            {
                Task = task,
                Assigned = db.MarketplaceOffers.Any(offer => offer.TaskId == task.Id &&
                    offer.FreelancerId == conversation.FreelancerId && offer.Status == "accepted")
            }).SingleOrDefaultAsync(ct);
        if (result is null) return NotFound();

        Work = result.Task;
        ConversationId = conversationId;
        IsOwner = Work.ClientId == userId;
        IsAssigned = result.Assigned;
        return Page();
    }
}
~~~~

### wwwroot/js/task-submit.js

~~~~javascript
const pendingForms = new Map();

document.addEventListener("submit", event => {
    if (pendingForms.has(event.target)) {
        event.preventDefault();
        event.stopImmediatePropagation();
    }
}, true);

document.addEventListener("submit", event => {
    const form = event.target;
    const button = event.submitter || form.querySelector('button[type="submit"], button:not([type]), input[type="submit"]');
    const method = button?.getAttribute("formmethod") || form.method;
    if (event.defaultPrevented || method.toLowerCase() !== "post" || button?.disabled || form.getAttribute("aria-busy") === "true") return;

    pendingForms.set(form, button);
    form.setAttribute("aria-busy", "true");
    if (button) {
        button.setAttribute("aria-disabled", "true");
        button.style.setProperty("--submit-spinner-color", getComputedStyle(button).color);
        button.dataset.pendingLabel = form.dataset.submitPending || "Loading…";
    }
});

window.addEventListener("pageshow", () => {
    for (const [form, button] of pendingForms) {
        form.removeAttribute("aria-busy");
        if (button) {
            button.removeAttribute("aria-disabled");
            delete button.dataset.pendingLabel;
            button.style.removeProperty("--submit-spinner-color");
        }
    }
    pendingForms.clear();
});
~~~~

### Data/MarketplaceTask.cs

~~~~csharp
namespace tts.Data;

public sealed class MarketplaceTask
{
    public long Id { get; set; }
    public int ClientId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string? ArrivalInstructions { get; set; }
    public string Location { get; set; } = "";
    public decimal Budget { get; set; }
    public decimal? BudgetMaximum { get; set; }
    public int? PostDurationDays { get; set; }
    public DateTime? PostExpiresUtc { get; set; }
    public string BudgetDisplay => BudgetMaximum.HasValue && BudgetMaximum.Value != Budget
        ? $"₱{Budget:N2} – ₱{BudgetMaximum.Value:N2}" : $"₱{Budget:N2}";
    public string ScheduleText { get; set; } = "";
    public string Status { get; set; } = "pending-confirmation";
    public bool IsPrivate { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MarketplaceOffer
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int FreelancerId { get; set; }
    public decimal Price { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? WithdrawalReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
~~~~

### database/mysql_tasks_offers.sql

~~~~sql
-- New tables only: this does not modify browser-local tasks or existing users.
CREATE TABLE IF NOT EXISTS marketplace_tasks (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    Title VARCHAR(120) NOT NULL,
    Description TEXT NOT NULL,
    Category VARCHAR(60) NOT NULL,
    Location VARCHAR(120) NOT NULL,
    Budget DECIMAL(12,2) NOT NULL,
    ScheduleText VARCHAR(120) NOT NULL,
    Status VARCHAR(24) NOT NULL DEFAULT 'pending-confirmation',
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_marketplace_task_client FOREIGN KEY (ClientId) REFERENCES tbl_user(Id),
    INDEX ix_marketplace_task_status (Status),
    INDEX ix_marketplace_task_client (ClientId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS marketplace_offers (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    FreelancerId INT NOT NULL,
    Price DECIMAL(12,2) NOT NULL,
    Message VARCHAR(500) NOT NULL DEFAULT '',
    Status VARCHAR(24) NOT NULL DEFAULT 'pending',
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_marketplace_offer_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_marketplace_offer_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id),
    UNIQUE KEY uq_marketplace_offer_task_freelancer (TaskId, FreelancerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
~~~~

## Booking and work agreements

### Pages/BookingRequests.cshtml.cs

~~~~csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class BookingRequestsModel(ApplicationDbContext db, ILogger<BookingRequestsModel> logger, IWebHostEnvironment environment) : PageModel
{
    public static bool CanBook(string role) => role is "client" or "both";
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [TempData] public string? Notice { get; set; }

    public IActionResult OnGet() => RedirectToPage("/FindPost");

    public async Task<IActionResult> OnPostInquiryAsync(int freelancerId, string? question, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        question = question?.Trim();
        if (!ModelState.IsValid || freelancerId <= 0 || freelancerId == UserId || question is not { Length: > 0 and <= 2000 })
            return BadRequest();

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var role = await db.FreelancerProfiles.Where(p => p.UserId == UserId).Select(p => p.WorkRole).SingleOrDefaultAsync(ct);
            var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId &&
                (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Service != "" && p.Location != "", ct);
            if (!CanBook(role ?? "client") || provider is null ||
                !await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, freelancerId))
            {
                Notice = "This service inquiry could not be sent.";
                return RedirectToPage("/FindPost", new { providerId = freelancerId });
            }

            var conversation = await (from c in db.Set<TaskConversation>()
                join t in db.MarketplaceTasks on c.TaskId equals t.Id
                where t.ClientId == UserId && t.IsPrivate && t.Status == "inquiry" && c.FreelancerId == freelancerId
                orderby c.Id descending
                select c).FirstOrDefaultAsync(ct);
            if (conversation is null)
            {
                var task = new MarketplaceTask { ClientId = UserId, Title = "Service inquiry",
                    Status = "inquiry", IsPrivate = true, CreatedAt = DateTime.UtcNow };
                db.Add(task);
                await db.SaveChangesAsync(ct);
                await AgreementRules.ConversationAsync(db, task.Id, freelancerId, ct);
                await db.SaveChangesAsync(ct);
                conversation = await db.Set<TaskConversation>().SingleAsync(c => c.TaskId == task.Id && c.FreelancerId == freelancerId, ct);
            }

            db.Add(new MarketplaceMessage { TaskId = conversation.TaskId, ConversationId = conversation.Id,
                SenderId = UserId, Body = question, CreatedAt = DateTime.UtcNow });
            var notice = MarketplaceNotification.Create(freelancerId, conversation.TaskId, "message", "You have a new service inquiry.");
            notice.ConversationId = conversation.Id;
            db.Add(notice);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return RedirectToPage("/Messages", new { conversationId = conversation.Id });
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException)
        {
            logger.LogWarning(ex, "Unable to send service inquiry");
            Notice = "Your inquiry could not be sent. Please try again.";
            return RedirectToPage("/FindPost", new { providerId = freelancerId });
        }
    }

    public async Task<IActionResult> OnPostDirectAsync(int freelancerId, string? title, string? description,
        string? location, string? schedule, decimal budget, CancellationToken ct, double? latitude = null, double? longitude = null, decimal? budgetMaximum = null, long? inquiryId = null, string? serviceArea = null, string? arrivalInstructions = null)
    {
        if (UserId <= 0) return Challenge();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        arrivalInstructions = arrivalInstructions?.Trim();
        if (arrivalInstructions?.Length > 500 || !await TaskLocationRules.MatchesAreaAsync(HttpContext, serviceArea, latitude, longitude, ct))
        { Notice = "Choose a verified pin in the selected Bacolor service area and keep arrival instructions under 500 characters."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        title = (title ?? "").Trim(); description = (description ?? "").Trim();
        location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (!ModelState.IsValid || freelancerId <= 0 || freelancerId == UserId || title.Length is < 1 or > 120 ||
            description.Length is < 1 or > 5000 || location.Length is < 1 or > 120 || schedule.Length is < 1 or > 120 ||
            budget is < 1 or > 9999999999m || decimal.Round(budget, 2) != budget || !MarketTasksModel.ValidBudgetMaximum(budget, budgetMaximum) || !TaskLocationRules.Valid(latitude, longitude))
        { Notice = "Complete the work details and confirm the location. The maximum budget must be at least the minimum."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        if (!DateTime.TryParseExact(schedule, "yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var preferredTime) ||
            new DateTimeOffset(DateTime.SpecifyKind(preferredTime, DateTimeKind.Unspecified), TimeSpan.FromHours(8)).UtcDateTime <= DateTime.UtcNow)
        { Notice = "Choose a valid future date and time for your booking (Philippine time)."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        schedule = preferredTime.ToString("MMM d, yyyy 'at' h:mm tt '(PHT)'", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, freelancerId))
            { Notice = "This request cannot be sent while either account is restricted or has a pending deletion request."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
            var requesterProfile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, ct);
            var role = requesterProfile?.WorkRole ?? "client";
            var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId &&
                (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
            if (!CanBook(role) || provider is null)
            { Notice = "Choose an available freelancer to send a booking."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
            if (preferredTime.Date <= DateTime.UtcNow.AddHours(8).Date && await FreelancerAvailability.IsBusyAsync(db, freelancerId, ct))
            { Notice = "This freelancer is working now. Choose tomorrow or a later date in Philippine time."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
            var duplicate = await db.MarketplaceBookings.AnyAsync(b => b.FreelancerId == freelancerId && b.Status == "pending" &&
                db.MarketplaceTasks.Any(t => t.Id == b.TaskId && t.ClientId == UserId && t.Status == "booking-pending" &&
                    t.Title == title && t.Description == description && t.Location == location && t.ScheduleText == schedule && t.Budget == budget && t.BudgetMaximum == budgetMaximum), ct);
            if (duplicate)
            { Notice = "This booking request has already been sent. Check My Tasks for its status."; return RedirectToPage("/MarketTasks", new { view = "mine" }); }
            MarketplaceTask? work = null;
            if (inquiryId.HasValue)
            {
                work = await (from c in db.Set<TaskConversation>()
                    join t in db.MarketplaceTasks on c.TaskId equals t.Id
                    where c.Id == inquiryId && c.FreelancerId == freelancerId &&
                        t.ClientId == UserId && t.IsPrivate && t.Status == "inquiry"
                    select t).SingleOrDefaultAsync(ct);
                if (work is null)
                {
                    Notice = "This inquiry is no longer available for booking.";
                    return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId });
                }
            }
            else
            {
                work = new MarketplaceTask { ClientId = UserId, IsPrivate = true, CreatedAt = DateTime.UtcNow };
                db.Add(work);
            }

            work.Title = title;
            work.Description = description;
            work.Location = location;
            work.ArrivalInstructions = arrivalInstructions;
            work.ScheduleText = schedule;
            work.Budget = budget;
            work.BudgetMaximum = budgetMaximum;
            work.Category = provider.Service;
            work.Status = "booking-pending";
            await db.SaveChangesAsync(ct);
            TaskLocationRules.Save(db, work.Id, null, latitude!.Value, longitude!.Value);
            await AgreementRules.ConversationAsync(db, work.Id, freelancerId, ct);
            db.MarketplaceBookings.Add(new MarketplaceBooking { TaskId = work.Id, FreelancerId = freelancerId, Status = "pending", CreatedAt = DateTime.UtcNow });
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(freelancerId, work.Id, "booking_request", "A client sent you a private work request. Review it in My Jobs → Booking requests."));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (inquiryId.HasValue) return RedirectToPage("/Messages", new { conversationId = inquiryId });
            Notice = "Booking request sent directly to the freelancer. You can follow its status in My Tasks.";
            return RedirectToPage("/MarketTasks", new { view = "mine" });
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning(ex, "Unable to create direct booking"); Notice = "Booking could not be saved. Please try again."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
    }

    public async Task<IActionResult> OnPostCreateAsync(long taskId, int freelancerId, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, freelancerId)) { Notice = "This request cannot be sent while either account is restricted or has a pending deletion request."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        var requesterProfile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, ct);
            var role = requesterProfile?.WorkRole ?? "client";
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == taskId && t.ClientId == UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow), ct);
        var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId && p.UserId != UserId && (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
        if (!CanBook(role) || task is null || provider is null || await FreelancerAvailability.IsBusyAsync(db, freelancerId, ct) || await db.MarketplaceBookings.AnyAsync(b => b.TaskId == taskId && b.FreelancerId == freelancerId, ct))
        { Notice = "Request could not be sent. Check that your task is open, the freelancer is available, and you have not sent this request already."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        await AgreementRules.ConversationAsync(db, taskId, freelancerId, ct);
        db.MarketplaceBookings.Add(new MarketplaceBooking { TaskId = taskId, FreelancerId = freelancerId, Status = "pending", CreatedAt = DateTime.UtcNow });
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(freelancerId, taskId, "booking_request", "A client sent you a booking request."));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); Notice = "Booking request sent. The freelancer can accept or decline it in My Jobs → Booking requests."; }
        catch (DbUpdateException ex) { logger.LogWarning(ex, "Unable to save booking request"); Notice = "Request could not be saved. Please try again."; }
        return RedirectToPage("/FindPost", new { providerId = freelancerId });
    }

    public async Task<IActionResult> OnPostDecideAsync(long id, string? decision, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (decision is not ("accept" or "decline")) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var booking = await db.MarketplaceBookings.SingleOrDefaultAsync(b => b.Id == id && b.FreelancerId == UserId && b.Status == "pending", ct);
        var task = booking is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == booking.TaskId && (t.Status == "booking-pending" || t.Status == "open" && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow)), ct);
        if (booking is null || task is null) { Notice = "This request is no longer available."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
        if (decision == "decline")
        {
            booking.Status = "declined";
            if (task.Status == "booking-pending") { task.Status = "cancelled"; task.CancellationReason = "The freelancer declined this direct booking request."; }
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "booking_declined", "The freelancer declined your booking request."));
        }
        else
        {
            if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, task.ClientId)) { Notice = "This request cannot be accepted while either account is restricted or has a pending deletion request."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
            booking.Status = "accepted"; task.Status = "assigned";
            await AgreementRules.ConversationAsync(db, task.Id, UserId, ct);
            await AgreementRules.InitialAsync(db, task, task.Budget, UserId, ct);
            db.Set<TaskProgressEvent>().Add(new TaskProgressEvent { TaskId = task.Id, ActorId = UserId, Status = "assigned", CreatedAtUtc = DateTime.UtcNow });
            var offer = await db.MarketplaceOffers.SingleOrDefaultAsync(o => o.TaskId == task.Id && o.FreelancerId == UserId, ct);
            if (offer == null) db.MarketplaceOffers.Add(new MarketplaceOffer { TaskId = task.Id, FreelancerId = UserId, Price = task.Budget, Message = "Direct booking: initial proposed price; confirm the final terms together.", Status = "accepted", CreatedAt = DateTime.UtcNow });
            else { offer.Price = task.Budget; offer.Message = "Direct booking: initial proposed price; confirm the final terms together."; offer.Status = "accepted"; }
            await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.FreelancerId != UserId && o.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "declined"), ct);
            await db.MarketplaceBookings.Where(b => b.TaskId == task.Id && b.Id != booking.Id && b.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "unavailable"), ct);
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "booking_accepted", "The freelancer accepted your booking request."));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = decision == "accept" ? "Booking accepted. Open Tracking to follow the job." : "Booking request declined.";
        return RedirectToPage("/MyJobs", new { view = decision == "accept" ? "active" : "requests" });
    }
}
~~~~

### Pages/Agreement.cshtml.cs

~~~~csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;
[Authorize]
public class AgreementModel(ApplicationDbContext db, IWebHostEnvironment environment, ILogger<AgreementModel> logger) : PageModel
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [TempData] public string? Notice { get; set; }
    public IActionResult OnGet(long taskId) => RedirectToPage("/Tracking", new { taskId });
    private async Task<(MarketplaceTask Task, int Freelancer)?> LockAsync(long id, CancellationToken ct)
    {
        // Called after user rows have been locked by the handlers.
        var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct);
        var task = tasks.SingleOrDefault();
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
        if (task is null || freelancer is null || UserId != task.ClientId && UserId != freelancer.Value || !AgreementRules.Editable(task.Status)) return null;
        return (task, freelancer.Value);
    }
    private async Task<int[]> ParticipantsAsync(long id, CancellationToken ct)
    {
        var pair = await (from t in db.MarketplaceTasks.AsNoTracking()
            join o in db.MarketplaceOffers.AsNoTracking() on t.Id equals o.TaskId
            where t.Id == id && o.Status == "accepted" && (t.ClientId == UserId || o.FreelancerId == UserId)
            select new { t.ClientId, o.FreelancerId }).SingleOrDefaultAsync(ct);
        return pair is null ? [] : [pair.ClientId, pair.FreelancerId];
    }
    public async Task<IActionResult> OnPostProposeAsync(long taskId, int expectedVersion, decimal price, string? scope, string? schedule, string? location, double? latitude, double? longitude, CancellationToken ct, string? serviceArea = null)
    {
        scope = (scope ?? "").Trim(); schedule = (schedule ?? "").Trim(); location = (location ?? "").Trim();
        if (UserId <= 0) return Challenge();
        if (!await TaskLocationRules.MatchesAreaAsync(HttpContext, serviceArea, latitude, longitude, ct))
        { Notice = "Choose a verified pin in the selected Bacolor service area."; return RedirectToPage("/Tracking", new { taskId }); }
        if (!ModelState.IsValid || !AgreementRules.Valid(price, scope, schedule, location, latitude, longitude)) { Notice = "Check price, scope, schedule and the confirmed Bacolor-area destination pin."; return RedirectToPage("/Tracking", new { taskId }); }
        try
        {
            var participants = await ParticipantsAsync(taskId, ct); if (participants.Length != 2) return NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await ModerationGuard.AllowedAsync(db, environment, ct, participants)) return Forbid();
            var locked = await LockAsync(taskId, ct); if (locked is null || !participants.Contains(locked.Value.Freelancer)) return NotFound();
            var latest = await AgreementRules.LatestAsync(db, taskId, ct);
            if (!AgreementRules.MatchesVersion(latest, expectedVersion)) { Notice = "The agreement changed. Review the latest version before proposing again."; return RedirectToPage("/Tracking", new { taskId }); }
            var proposal = new WorkAgreement { TaskId = taskId, Version = expectedVersion + 1, ProposedById = UserId, Price = price, Scope = scope, ScheduleText = schedule, Location = location,
                Latitude = latitude!.Value, Longitude = longitude!.Value, CreatedAtUtc = DateTime.UtcNow };
            if (latest is not null && AgreementRules.SameTerms(latest, proposal)) { Notice = "These terms are unchanged. Confirm the existing version instead."; return RedirectToPage("/Tracking", new { taskId }); }
            db.Add(proposal);
            db.Add(MarketplaceNotification.Create(UserId == locked.Value.Task.ClientId ? locked.Value.Freelancer : locked.Value.Task.ClientId, taskId, "agreement", "New work terms proposed. Both participants must confirm the same version in Tracking."));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = "New agreement saved. Both of you must confirm this version. The current destination stays unchanged until then.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning(ex, "Unable to propose work agreement"); Notice = "Could not save the agreement. Check the agreement database setup and try again."; }
        return RedirectToPage("/Tracking", new { taskId });
    }
    public async Task<IActionResult> OnPostConfirmAsync(long taskId, int version, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        try
        {
            var participants = await ParticipantsAsync(taskId, ct); if (participants.Length != 2) return NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await ModerationGuard.AllowedAsync(db, environment, ct, participants)) return Forbid();
            var locked = await LockAsync(taskId, ct); if (locked is null || !participants.Contains(locked.Value.Freelancer)) return NotFound();
            var latest = await AgreementRules.LatestAsync(db, taskId, ct);
            if (latest is null || !AgreementRules.MatchesVersion(latest, version)) { Notice = "The agreement changed. Read and confirm the latest version."; return RedirectToPage("/Tracking", new { taskId }); }
            if (UserId == locked.Value.Task.ClientId ? latest.ClientConfirmedAtUtc.HasValue : latest.FreelancerConfirmedAtUtc.HasValue)
            { Notice = "Your confirmation for this version is already saved."; return RedirectToPage("/Tracking", new { taskId }); }
            if (!AgreementRules.Valid(latest.Price, latest.Scope, latest.ScheduleText, latest.Location, latest.Latitude, latest.Longitude)) return BadRequest();
            if (UserId == locked.Value.Task.ClientId) latest.ClientConfirmedAtUtc ??= DateTime.UtcNow;
            else latest.FreelancerConfirmedAtUtc ??= DateTime.UtcNow;
            if (latest.Confirmed)
            {
                locked.Value.Task.Budget = latest.Price; locked.Value.Task.Description = latest.Scope; locked.Value.Task.ScheduleText = latest.ScheduleText; locked.Value.Task.Location = latest.Location;
                var offer = await db.MarketplaceOffers.SingleAsync(o => o.TaskId == taskId && o.Status == "accepted", ct); offer.Price = latest.Price;
                TaskLocationRules.Save(db, taskId, await db.Set<TaskDestination>().SingleOrDefaultAsync(d => d.TaskId == taskId, ct), latest.Latitude, latest.Longitude);
            }
            db.Add(MarketplaceNotification.Create(UserId == locked.Value.Task.ClientId ? locked.Value.Freelancer : locked.Value.Task.ClientId, taskId, "agreement", latest.Confirmed ? "Both participants confirmed the work agreement. Travel can proceed." : "Your counterpart confirmed the work terms. Review and confirm the same version."));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = latest.Confirmed ? "Agreement confirmed by both. The destination and agreed terms are ready." : "Your confirmation was saved. Waiting for the other participant.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning(ex, "Unable to confirm work agreement"); Notice = "Could not confirm the agreement. Please try again."; }
        return RedirectToPage("/Tracking", new { taskId });
    }
}
~~~~

### Data/MarketplaceBooking.cs

~~~~csharp
namespace tts.Data;

public sealed class MarketplaceBooking
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int FreelancerId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}
~~~~

### Data/WorkAgreement.cs

~~~~csharp
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed record TaskLocationInput(string Address = "", double? Latitude = null, double? Longitude = null, bool ShowArrivalInstructions = false, string? ArrivalInstructions = null);
public sealed record AgreementView(MarketplaceTask Task, WorkAgreement? Agreement, TaskDestination? Destination, bool IsClient);

public sealed class TaskConversation
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int FreelancerId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class WorkAgreement
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int Version { get; set; }
    public int ProposedById { get; set; }
    public decimal Price { get; set; }
    public string ScheduleText { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Location { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ClientConfirmedAtUtc { get; set; }
    public DateTime? FreelancerConfirmedAtUtc { get; set; }
    public bool Confirmed => ClientConfirmedAtUtc.HasValue && FreelancerConfirmedAtUtc.HasValue;
}

public static class TaskLocationRules
{
    // Search/service-area envelope, not a cadastral municipal boundary.
    public static bool Valid(double? latitude, double? longitude) => latitude.HasValue && longitude.HasValue &&
        double.IsFinite(latitude.Value) && double.IsFinite(longitude.Value) && latitude is >= 14.92 and <= 15.10 && longitude is >= 120.54 and <= 120.73;
    public static async Task<bool> MatchesAreaAsync(HttpContext context, string? area, double? latitude, double? longitude, CancellationToken ct)
    {
        if (!Valid(latitude, longitude) || area is null || !tts.Pages.BacolorServiceAreas.Names.Contains(area))
            return false;
        try
        {
            var result = await context.RequestServices.GetRequiredService<PlaceSearch>()
                .ReverseAsync(latitude!.Value, longitude!.Value, ct);
            return result?.ServiceArea == area && !result.OutsideBacolor;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    public static string PublicArea => "Bacolor, Pampanga";
    public static void Save(ApplicationDbContext db, long taskId, TaskDestination? existing, double latitude, double longitude)
    {
        var pin = existing ?? new TaskDestination { TaskId = taskId };
        if (existing is null) db.Add(pin);
        pin.Latitude = latitude; pin.Longitude = longitude; pin.UpdatedAtUtc = DateTime.UtcNow;
    }
}

public static class AgreementRules
{
    public static bool CanProceed(WorkAgreement? agreement) => agreement?.Confirmed == true;
    public static bool MatchesVersion(WorkAgreement? agreement, int version) => (agreement?.Version ?? 0) == version;
    public static bool Editable(string status) => status is "assigned" or "on-the-way";
    public static bool Valid(decimal price, string scope, string schedule, string location, double? latitude, double? longitude) =>
        price is > 0 and <= 9999999999m && decimal.Round(price, 2) == price && scope.Length is > 0 and <= 5000 &&
        schedule.Length is > 0 and <= 120 && location.Length is > 0 and <= 120 && TaskLocationRules.Valid(latitude, longitude);
    public static bool SameTerms(WorkAgreement a, WorkAgreement b) => a.Price == b.Price && a.Scope == b.Scope && a.ScheduleText == b.ScheduleText &&
        a.Location == b.Location && a.Latitude == b.Latitude && a.Longitude == b.Longitude;
    public static async Task<WorkAgreement?> LatestAsync(ApplicationDbContext db, long taskId, CancellationToken ct) =>
        await db.Set<WorkAgreement>().Where(a => a.TaskId == taskId).OrderByDescending(a => a.Version).FirstOrDefaultAsync(ct);
    public static async Task InitialAsync(ApplicationDbContext db, MarketplaceTask task, decimal price, int actor, CancellationToken ct)
    {
        var pin = await db.Set<TaskDestination>().SingleOrDefaultAsync(p => p.TaskId == task.Id, ct);
        if (pin is null || !TaskLocationRules.Valid(pin.Latitude, pin.Longitude)) return; // Existing jobs can propose terms in Tracking.
        var previous = await LatestAsync(db, task.Id, ct);
        db.Add(new WorkAgreement { TaskId = task.Id, Version = (previous?.Version ?? 0) + 1, ProposedById = actor, Price = price,
            Scope = task.Description, ScheduleText = string.IsNullOrWhiteSpace(task.ScheduleText) ? "To be agreed" : task.ScheduleText,
            Location = task.Location, Latitude = pin.Latitude, Longitude = pin.Longitude, CreatedAtUtc = DateTime.UtcNow });
    }
    public static async Task ConversationAsync(ApplicationDbContext db, long taskId, int freelancerId, CancellationToken ct)
    {
        if (!await db.Set<TaskConversation>().AnyAsync(c => c.TaskId == taskId && c.FreelancerId == freelancerId, ct))
            db.Add(new TaskConversation { TaskId = taskId, FreelancerId = freelancerId, CreatedAtUtc = DateTime.UtcNow });
    }
}

public static class ConversationAccess
{
    public static IQueryable<TaskConversation> VisibleTo(ApplicationDbContext db, int viewer) =>
        from conversation in db.Set<TaskConversation>()
        join task in db.MarketplaceTasks on conversation.TaskId equals task.Id
        join client in db.Users on task.ClientId equals client.Id
        join freelancer in db.Users on conversation.FreelancerId equals freelancer.Id
        where (client.Id == viewer || freelancer.Id == viewer)
            && client.Role != "deleted" && freelancer.Role != "deleted"
        select conversation;
}
~~~~

### database/mysql_bookings.sql

~~~~sql
-- Run after mysql_tasks_offers.sql and mysql_notifications.sql.
CREATE TABLE IF NOT EXISTS marketplace_bookings (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    FreelancerId INT NOT NULL,
    Status VARCHAR(24) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_booking_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_booking_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id),
    UNIQUE KEY uq_booking_task_freelancer (TaskId, FreelancerId),
    INDEX ix_booking_freelancer (FreelancerId, Status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
~~~~

### database/mysql_work_agreements.sql

~~~~sql
-- Select the intended schema first (e.g. USE defaultdb; on Aiven).
-- Run before deploying this update. Existing marketplace and task_destinations tables are required.
CREATE TABLE IF NOT EXISTS task_conversations (
 Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 TaskId BIGINT NOT NULL,
 FreelancerId INT NOT NULL,
 CreatedAtUtc DATETIME(6) NOT NULL,
 UNIQUE KEY ux_task_conversation (TaskId, FreelancerId),
 CONSTRAINT fk_conversation_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
 CONSTRAINT fk_conversation_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS work_agreements (
 Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 TaskId BIGINT NOT NULL,
 Version INT NOT NULL,
 ProposedById INT NOT NULL,
 Price DECIMAL(12,2) NOT NULL,
 ScheduleText VARCHAR(120) NOT NULL,
 Scope VARCHAR(5000) NOT NULL,
 Location VARCHAR(120) NOT NULL,
 Latitude DOUBLE NOT NULL,
 Longitude DOUBLE NOT NULL,
 CreatedAtUtc DATETIME(6) NOT NULL,
 ClientConfirmedAtUtc DATETIME(6) NULL,
 FreelancerConfirmedAtUtc DATETIME(6) NULL,
 UNIQUE KEY ux_agreement_version (TaskId, Version),
 CONSTRAINT fk_agreement_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
 CONSTRAINT fk_agreement_proposer FOREIGN KEY (ProposedById) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @ddl = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_tasks' AND COLUMN_NAME='IsPrivate')=0,
 'ALTER TABLE marketplace_tasks ADD COLUMN IsPrivate TINYINT(1) NOT NULL DEFAULT 0', 'SELECT ''IsPrivate exists''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_messages' AND COLUMN_NAME='ConversationId')=0,
 'ALTER TABLE marketplace_messages ADD COLUMN ConversationId BIGINT NULL, ADD INDEX ix_message_conversation (ConversationId,Id), ADD CONSTRAINT fk_message_conversation FOREIGN KEY (ConversationId) REFERENCES task_conversations(Id)', 'SELECT ''Message ConversationId exists''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_notifications' AND COLUMN_NAME='ConversationId')=0,
 'ALTER TABLE marketplace_notifications ADD COLUMN ConversationId BIGINT NULL, ADD INDEX ix_notification_conversation (ConversationId), ADD CONSTRAINT fk_notification_conversation FOREIGN KEY (ConversationId) REFERENCES task_conversations(Id)', 'SELECT ''Notification ConversationId exists''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Preserve privacy for recognizable existing direct bookings, including withdrawn requests.
UPDATE marketplace_tasks t SET t.IsPrivate=1
WHERE t.Status='booking-pending' OR EXISTS (
 SELECT 1 FROM marketplace_offers o WHERE o.TaskId=t.Id AND o.Message='Direct booking at the agreed request budget.');
UPDATE marketplace_tasks t SET t.Status='cancelled', t.CancellationReason='Private request closed after the selected freelancer withdrew.'
WHERE t.IsPrivate=1 AND t.Status='open';

-- Create one private thread for each existing offer/booking relationship; do not duplicate existing threads.
INSERT INTO task_conversations (TaskId,FreelancerId,CreatedAtUtc)
SELECT seed.TaskId,seed.FreelancerId,UTC_TIMESTAMP(6)
FROM (
 SELECT TaskId,FreelancerId FROM marketplace_offers WHERE Status IN ('accepted','pending','withdrawn')
 UNION SELECT TaskId,FreelancerId FROM marketplace_bookings WHERE Status IN ('pending','accepted','withdrawn')
) seed LEFT JOIN task_conversations c ON c.TaskId=seed.TaskId AND c.FreelancerId=seed.FreelancerId
WHERE c.Id IS NULL;

-- Only unambiguous legacy messages are attached to a selected pair.
-- Messages from tasks with a previous different assigned freelancer remain untouched (ConversationId NULL)
-- for manual migration review; guessing their recipient could expose private conversations.
UPDATE marketplace_messages m
JOIN marketplace_tasks t ON t.Id=m.TaskId
JOIN marketplace_offers o ON o.TaskId=t.Id AND o.Status='accepted'
JOIN task_conversations c ON c.TaskId=t.Id AND c.FreelancerId=o.FreelancerId
SET m.ConversationId=c.Id
WHERE m.ConversationId IS NULL AND m.SenderId IN (t.ClientId,o.FreelancerId)
 AND NOT EXISTS (SELECT 1 FROM marketplace_offers other WHERE other.TaskId=t.Id AND other.FreelancerId<>o.FreelancerId AND other.Status IN ('accepted','withdrawn'));

SELECT DATABASE() AS selected_database;
SELECT COUNT(*) AS unassigned_legacy_messages FROM marketplace_messages WHERE ConversationId IS NULL;
~~~~

## Tracking and task completion

### Pages/MyTasks.cshtml.cs

~~~~csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace tts.Pages
{
    public class MyTasksModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
~~~~

### Pages/MyJobs.cshtml.cs

~~~~csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class MyJobsModel(ApplicationDbContext db, ILogger<MyJobsModel> logger) : PageModel
{
    public record JobItem(MarketplaceTask Task, MarketplaceOffer Offer, string ClientName);
    public record BookingItem(MarketplaceBooking Booking, MarketplaceTask Task, string ClientName);
    public List<JobItem> Jobs { get; private set; } = [];
    public List<BookingItem> Bookings { get; private set; } = [];
    public bool Unavailable { get; private set; }
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId == 0) return Challenge();
        if (Request.Query["legacy"] == "1") return Page();
        try
        {
            Jobs = await (from offer in db.MarketplaceOffers.AsNoTracking()
                          join task in db.MarketplaceTasks.AsNoTracking() on offer.TaskId equals task.Id
                          join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                          where offer.FreelancerId == UserId
                          orderby offer.Id descending
                          select new JobItem(task, offer, ((client.FirstName ?? "") + " " + (client.LastName ?? "")).Trim())).ToListAsync(ct);
            Bookings = await (from booking in db.MarketplaceBookings.AsNoTracking()
                              join task in db.MarketplaceTasks.AsNoTracking() on booking.TaskId equals task.Id
                              join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                              where booking.FreelancerId == UserId && ((booking.Status == "pending" && (task.Status == "open" || task.Status == "booking-pending")) || (booking.Status == "cancelled" && task.Status == "cancelled"))
                              orderby booking.Id descending
                              select new BookingItem(booking, task, ((client.FirstName ?? "") + " " + (client.LastName ?? "")).Trim())).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load freelancer jobs");
            Unavailable = true;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAdvanceAsync(long id, string? action, CancellationToken ct)
    {
        if (UserId == 0) return Challenge();
        if (action == "finish") return RedirectToPage("/Tracking", new { taskId = id });
        TaskProgress.Result result;
        try { result = await TaskProgress.AdvanceAsync(db, UserId, id, action, null, null, null, ct); }
        catch (MySqlConnector.MySqlException) { result = new(false, "Progress storage is unavailable. Please check the database setup."); }
        Notice = result.Message;
        return RedirectToPage("/Tracking", new { taskId = id, shareLocation = result.Success && action == "travel" ? "1" : null });
    }

    public async Task<IActionResult> OnPostWithdrawAsync(long id, string? reason, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        reason = (reason ?? "").Trim();
        if (!ModelState.IsValid || reason.Length is < 1 or > 500)
        { Notice = "Please provide a withdrawal reason (up to 500 characters)."; return RedirectToPage(new { view = "active" }); }

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.Status == "assigned" &&
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == UserId && o.Status == "accepted"), ct);
        if (task is null)
        { Notice = "This job cannot be withdrawn. It may have already started or changed."; return RedirectToPage(new { view = "active" }); }

        var offer = await db.MarketplaceOffers.SingleAsync(o => o.TaskId == id && o.FreelancerId == UserId && o.Status == "accepted", ct);
        task.Status = task.IsPrivate ? "cancelled" : task.PostExpiresUtc <= DateTime.UtcNow ? "expired" : "open";
        if(task.IsPrivate) task.CancellationReason = "The selected freelancer withdrew before travel: " + reason;
        offer.Status = "withdrawn";
        offer.WithdrawalReason = reason;
        await db.MarketplaceBookings.Where(b => b.TaskId == id && b.FreelancerId == UserId && b.Status == "accepted")
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "withdrawn"), ct);
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "withdrawn",
            task.IsPrivate ? "The freelancer withdrew. Your private request was cancelled and has not been published." : task.Status == "expired" ? "The freelancer withdrew after your task post expired. The task is closed." : "The freelancer withdrew before starting. Your task is open for new offers."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "You withdrew from the job. The client has been notified. Private requests stay private.";
        return RedirectToPage(new { view = "offers" });
    }
}
~~~~

### Pages/Tracking.cshtml

~~~~razor
@page
@model TrackingModel
@using tts.Data
@inject IConfiguration Configuration
@inject Microsoft.AspNetCore.Hosting.IWebHostEnvironment HostEnvironment
@{
    Layout = null;
}

<!DOCTYPE html>
<html lang="en">
<head><link rel="icon" type="image/svg+xml" href="/tap-task-icon.svg" />
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />

    <title>Tracking - Tap, Task, Done</title>

    <link rel="stylesheet" href="/css/tracking.css" asp-append-version="true" />
    <link rel="stylesheet" href="/css/tracking-database.css" asp-append-version="true" />
    <link rel="stylesheet" href="/css/sidebar-stability.css" asp-append-version="true" />
    <link rel="stylesheet" href="/css/readability.css" asp-append-version="true" />
    <script defer src="/js/sidebar-account.js" asp-append-version="true"></script>
    <script defer src="/js/profile-photo.js" asp-append-version="true"></script>
<link rel="stylesheet" href="/css/light-system.css" asp-append-version="true" />
<style>
    html, html body, html body .tracking-layout, html body .tracking-layout > .main-content {
        background: #f6f7f3 !important;
        min-height: 100%;
    }
    html body .tracking-layout, html body .tracking-layout > .main-content { min-height: 100vh; }
    html body .tracking-layout > .main-content { display: flow-root; }
    @@media (min-width: 769px) { html body #mobileMenuToggle, html body #mobileMenuOverlay { display: none !important; } }
</style>
<link rel="stylesheet" href="/css/sidebar-consistency.css" asp-append-version="true" />
<link rel="stylesheet" href="/css/workspace-spacing.css" asp-append-version="true" />
<link rel="stylesheet" href="~/lib/maplibre-gl/maplibre-gl.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/job-location.css" asp-append-version="true" />
<link rel="stylesheet" href="/lib/maplibre-gl/maplibre-gl.css" asp-append-version="true" /><link rel="stylesheet" href="/css/task-location-picker.css" asp-append-version="true" /><script defer src="/lib/maplibre-gl/maplibre-gl.js" asp-append-version="true"></script><script defer src="/js/task-location-picker.js" asp-append-version="true"></script><script defer src="/js/agreement-live.js" asp-append-version="true"></script><partial name="_AccountTheme" /></head>

<body>

    <div class="tracking-layout">

        <!-- ================= SIDEBAR ================= -->

        <aside class="sidebar">

            <a class="brand" asp-page="/Landing" aria-label="Tap, Task, Done landing page" style="text-decoration:none">
                <div class="brand-icon">✓</div>

                <div class="brand-name">
                    <strong>Tap, Task, Done</strong>
                </div>
            </a>


            <nav class="sidebar-menu">

                <a href="/Dashboard">
                    <span class="menu-icon">⌂</span>
                    <span>Home</span>
                </a>


                <a href="/FindPost">
                    <span class="menu-icon">⌕</span>
                    <span>Find &amp; Post</span>
                </a>


                <a href="/MyTasks">
                    <span class="menu-icon">▤</span>
                    <span>My Tasks</span>
                </a>


                <a href="/Messages">
                    <span class="menu-icon">□</span>
                    <span>Messages</span>
                </a>


                <a href="/IdentityVerification">
                    <span class="menu-icon">✓</span>
                    <span>Identity Verification</span>
                </a>


                <a href="/Tracking"
                   class="active">
                    <span class="menu-icon">➤</span>
                    <span>Tracking</span>
                </a>


                <a href="/Reviews">
                    <span class="menu-icon">☆</span>
                    <span>Reviews</span>
                </a>

            </nav>


            <div class="sidebar-account">

                <div class="account-avatar">
                    JD
                </div>

                <div class="account-details">
                    <strong id="sidebarName">Jhon</strong>
                    <span id="sidebarEmail">jhon@gmail.com</span>
                </div>

                <div class="account-more">
                    ⋮
                </div>

            </div>

        </aside>


        <!-- ================= MAIN ================= -->

        <main class="main-content">


            <!-- TOP HEADER -->

            <header class="top-header">

                <h1>Tracking</h1>

                <div class="notification app-notification-trigger">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-2 7-2 9h16c0-2-2-2-2-9M10 20h4"/></svg>
                </div>

            </header>


            <!-- ================= TRACKING CONTENT ================= -->

            @if (Request.Query["legacy"] == "1") {
            <section class="tracking-content">
                <p class="tracking-legacy-note">Older browser-saved tracking; this is not connected to MySQL. <a href="/Tracking">Back to current tracking</a></p>


                <!-- TASK SELECTOR -->

                <div class="task-selector">

                    <button class="task-tab selected" type="button">
                        Fix leaking kitchen faucet
                    </button>

                </div>

                <section class="tracking-overview" aria-label="Task overview">
                    <div class="tracking-overview-heading">
                        <div class="tracking-task-icon">⌁</div>
                        <div>
                            <span class="overview-eyebrow">Active task</span>
                            <h2 id="taskOverviewTitle">Fix leaking kitchen faucet</h2>
                            <p id="taskOverviewMeta">Repairs · Bacolor, Pampanga</p>
                        </div>
                        <div class="status-badge" id="statusBadge">
                            <span>●</span>
                            <span id="statusText">On the way</span>
                        </div>
                    </div>

                    <div class="tracking-summary-grid">
                        <div class="tracking-summary-card">
                            <span class="summary-icon">▣</span>
                            <div><span>Budget</span><strong id="trackingBudget">₱600</strong></div>
                        </div>
                        <div class="tracking-summary-card">
                            <span class="summary-icon">◷</span>
                            <div><span>Schedule</span><strong id="trackingSchedule">To be scheduled</strong></div>
                        </div>
                        <div class="tracking-summary-card">
                            <span class="summary-icon">◉</span>
                            <div><span>Status</span><strong id="trackingStatusSummary">On the way</strong></div>
                        </div>
                    </div>
                </section>


                <div class="tracking-grid">


                    <!-- ================= LEFT TRACKING CARD ================= -->

                    <div class="tracking-card">

                        <div class="tracking-card-header">

                            <div>

                                <h2 id="taskTitle">
                                    Fix leaking kitchen faucet
                                </h2>

                                <span class="live-status">
                                    Live status
                                </span>

                            </div>


                        </div>


                        <!-- TIMELINE -->

                        <div class="timeline">


                            <!-- TASK CONFIRMED -->

                            <div class="timeline-row completed">

                                <div class="timeline-marker">
                                    ✓
                                </div>

                                <div class="timeline-info">

                                    <div class="timeline-title">
                                        <strong>Task confirmed</strong>
                                        <span>9:40 AM</span>
                                    </div>

                                    <p>
                                        Booking accepted by Ofrac
                                    </p>

                                </div>

                            </div>


                            <!-- PREPARING -->

                            <div class="timeline-row completed">

                                <div class="timeline-marker">
                                    ✓
                                </div>

                                <div class="timeline-info">

                                    <div class="timeline-title">
                                        <strong>Preparing</strong>
                                        <span>10:15 AM</span>
                                    </div>

                                    <p>
                                        Gathering tools &amp; parts
                                    </p>

                                </div>

                            </div>


                            <!-- ON THE WAY -->

                            <div class="timeline-row active" id="onTheWayStep">

                                <div class="timeline-marker">
                                    ➤
                                </div>

                                <div class="timeline-info">

                                    <div class="timeline-title">
                                        <strong>On the way</strong>
                                        <span>1:45 PM</span>
                                    </div>

                                    <p>
                                        Arriving in ~15 min
                                    </p>

                                </div>

                            </div>


                            <!-- IN PROGRESS -->

                            <div class="timeline-row" id="inProgressStep">

                                <div class="timeline-marker">
                                    ◷
                                </div>

                                <div class="timeline-info">

                                    <div class="timeline-title">
                                        <strong>In progress</strong>
                                    </div>

                                    <p>
                                        Working on your task
                                    </p>

                                </div>

                            </div>


                            <!-- COMPLETED -->

                            <div class="timeline-row" id="completedStep">

                                <div class="timeline-marker">
                                    ◷
                                </div>

                                <div class="timeline-info">

                                    <div class="timeline-title">
                                        <strong>Completed</strong>
                                    </div>

                                    <p id="completedDescription">
                                        Task finished &amp; confirmed
                                    </p>

                                </div>

                            </div>


                        </div>


                        <!-- COMPLETE BUTTON -->

                        <div class="completion-area">

                            <button id="completeButton"
                                    class="complete-button"
                                    type="button"
                                    disabled>

                                Mark as complete

                            </button>

                            <p id="completionMessage">
                                Waiting for the freelancer to finish the task.
                            </p>

                            <button id="demoFinishTaskButton"
                                    class="demo-finish-button"
                                    type="button">
                                Start work
                            </button>

                        </div>


                    </div>


                    <!-- ================= RIGHT SIDE ================= -->

                    <div class="right-column">


                        <!-- PROVIDER CARD -->

                        <div class="provider-card">

                            <div class="provider-top">

                                <div class="provider-avatar">
                                    Of
                                </div>

                                <div class="provider-info">

                                    <strong>Ofrac</strong>

                                    <span>Plumber</span>

                                    <small id="providerRating">★ New freelancer</small>

                                </div>

                            </div>


                            <div class="provider-actions">
                                <a id="messageProviderButton" class="message-button" href="/Messages">
                                    □&nbsp;&nbsp; Message
                                </a>

                                <button type="button" id="reportProviderButton" class="report-provider-button">
                                    ⚑&nbsp;&nbsp; Report
                                </button>

                            </div>

                        </div>


                        <!-- ================= MAP ================= -->

                        <div class="map-card">

                            <div class="map-grid">

                                <div class="map-road road-one"></div>
                                <div class="map-road road-two"></div>
                                <div class="map-road road-three"></div>

                                <div class="map-location">
                                    <span>⌖</span>
                                </div>

                            </div>


                            <div class="arrival-info">

                                <span class="arrival-icon">
                                    ◷
                                </span>

                                <span>
                                    Estimated arrival:
                                </span>

                                <strong>
                                    2:00 PM
                                </strong>

                            </div>

                        </div>


                    </div>

                </div>


                <!-- REVIEW NOTICE -->

                <div id="completeTaskModal"
                     class="complete-task-modal hidden"
                     role="dialog"
                     aria-modal="true"
                     aria-labelledby="completeTaskModalTitle">

                    <div class="complete-task-dialog">
                        <div class="complete-task-dialog-header">
                            <div class="complete-task-icon">✓</div>
                            <button type="button" id="closeCompletionModal" class="close-completion-modal" aria-label="Close">×</button>
                        </div>
                        <span class="complete-task-eyebrow">WORK COMPLETE</span>
                        <h2 id="completeTaskModalTitle">Review the finished work</h2>
                        <p class="complete-task-copy">
                            Check that everything is complete before you confirm and leave a rating.
                        </p>

                        <div class="complete-task-summary">
                            <div class="complete-summary-item">
                                <span>Task</span>
                                <strong id="completeModalTaskTitle">Your task</strong>
                            </div>
                            <div class="complete-summary-item">
                                <span>Provider</span>
                                <strong id="completeModalProvider">Provider</strong>
                            </div>
                            <span class="work-finished-chip">✓ Work marked finished</span>
                        </div>

                        <p class="completion-help">ⓘ Need something fixed? Message the provider before confirming.</p>

                        <div class="complete-task-actions">
                            <button type="button" id="notYetButton" class="not-yet-button">
                                Not yet
                            </button>
                            <button type="button" id="confirmCompletionButton" class="confirm-completion-button">
                                Confirm &amp; rate provider
                            </button>
                        </div>
                    </div>

                </div>

                <div id="reviewNotice"
                     class="review-notice">

                    <form class="review-form" id="reviewForm">
                        <div class="review-provider">
                            <div class="review-avatar">Of</div>
                            <div>
                                <h2>Rate your task</h2>
                                <p>★ Ofrac · 4.8 (600 reviews)</p>
                            </div>
                        </div>

                        <p class="rating-prompt">How was your experience?</p>
                        <div class="star-picker" role="radiogroup" aria-label="Task rating">
                            <button type="button" data-rating="1" aria-label="1 star">☆</button>
                            <button type="button" data-rating="2" aria-label="2 stars">☆</button>
                            <button type="button" data-rating="3" aria-label="3 stars">☆</button>
                            <button type="button" data-rating="4" aria-label="4 stars">☆</button>
                            <button type="button" data-rating="5" aria-label="5 stars">☆</button>
                        </div>

                        <p class="review-label">What stood out?</p>
                        <div class="review-tags">
                            <button type="button">Punctual</button>
                            <button type="button">Professional</button>
                            <button type="button">Great value</button>
                            <button type="button">Clean work</button>
                            <button type="button">Friendly</button>
                        </div>

                        <label class="review-label" for="reviewFeedback">Leave feedback</label>
                        <textarea id="reviewFeedback" placeholder="Share details about your experience..."></textarea>
                        <button id="submitReviewButton" class="submit-review-button" type="submit" disabled>Submit review</button>
                    </form>

                </div>


            </section>
            } else {
            <section class="tracking-content db-tracking">
                <style>
                    .db-tracking .db-tracking-header h2,.db-tracking .db-tracking-card h3 { color:#143d31; }
                    .db-tracking .db-tracking-empty h3 { color:#143d31; }
                    .db-tracking .db-tracking-empty p { color:#526e60; }
                    .db-tracking .db-tracking-empty a { color:#126345; }
                    .db-tracking .db-tracking-header p,.db-tracking .db-tracking-card p { color:#526e60; }
                    .tracking-intro { background:#fff; border:1px solid #d5e5d6; border-radius:14px; padding:28px; margin-bottom:20px; }
                    .tracking-intro .db-tracking-header { margin-bottom:0; }
                    .tracking-intro .db-tracking-eyebrow { color:#19765a; font-size:10px; letter-spacing:.14em; }
                    .tracking-intro .db-tracking-header h2 { margin:10px 0 8px; font-size:28px; letter-spacing:-.5px; }
                    .tracking-intro .db-tracking-header p { margin:0; font-size:14px; }
                    html body .db-tracking .tracking-intro { background:#fff !important; box-shadow:none; }
                    html body .db-tracking .tracking-intro .db-tracking-header { background:#e5f0e6; box-shadow:none; }
                    .tracking-picker { position:relative; margin-top:10px; }
                    .tracking-picker-label { display:block; margin-top:16px; color:#193e30; font-size:13px; font-weight:600; }
                    .tracking-picker summary { list-style:none; display:flex; justify-content:space-between; align-items:center; gap:16px; min-height:62px; padding:13px 16px; border:1px solid #bfd3c3; border-radius:8px; background:#fff; color:#193e30; cursor:pointer; }
                    .tracking-picker summary::-webkit-details-marker { display:none; }
                    .tracking-picker summary > span { min-width:0; }
                    .tracking-picker summary strong { display:block; font-size:14px; font-weight:600; overflow-wrap:anywhere; }
                    .tracking-picker summary small { display:block; margin-top:5px; color:#637c6c; font-size:12px; }
                    .tracking-picker summary svg { flex:0 0 18px; width:18px; height:18px; color:#176b50; }
                    .tracking-picker[open] summary { border-color:#19765a; }
                    .tracking-picker[open] summary svg { transform:rotate(180deg); }
                    .tracking-picker summary:focus-visible,.tracking-picker-options a:focus-visible { outline:2px solid #19765a; outline-offset:3px; }
                    .tracking-picker-options { position:absolute; top:calc(100% + 7px); left:0; right:0; z-index:30; max-height:240px; overflow:auto; padding:5px; border:1px solid #d5e5d6; border-radius:8px; background:#fff; box-shadow:0 8px 24px rgba(25,62,48,.12); }
                    .tracking-picker-options a { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:12px; border-radius:5px; color:#193e30; text-decoration:none; }
                    .tracking-picker-options a:hover,.tracking-picker-options a[aria-current="page"] { background:#edf5ed; }
                    .tracking-picker-options strong { display:block; font-size:13px; overflow-wrap:anywhere; }
                    .tracking-picker-options small { display:block; margin-top:4px; color:#637c6c; font-size:11px; }
                    .tracking-picker-options .tracking-picker-check { color:#19765a; font-weight:700; }
                    .tracking-map-link { display:inline-flex; gap:8px; align-items:center; margin-top:16px; color:#176b50; font-size:13px; font-weight:600; text-decoration:none; }
                    .tracking-selector { margin-top:24px; }
                    .tracking-selector label { display:block; color:#193e30; font-weight:700; margin-bottom:9px; }
                    .tracking-select { display:block; width:100%; max-width:100%; min-height:48px; padding:12px 16px; margin:0; border:1px solid #cde0cf; border-radius:10px; background:white; color:#143d31; }
                    .tracking-select:focus-visible { outline:3px solid #a7d8ba; outline-offset:2px; }
                    html body .db-tracking .db-tracking-card { background:white !important; border:1px solid #d5e5d6; border-radius:14px; margin:20px 0; padding:26px; }
                    .db-tracking .db-tracking-details > div { background:#f2f7f1; color:#264d3c; border:1px solid #e1eee1; }
                    .db-tracking .db-tracking-details strong { color:#143d31; }
                    .tracking-detail-layout { display:grid; grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr); gap:18px; margin-top:22px; align-items:start; }
                    .tracking-panel { border:1px solid #dce9dc; border-radius:12px; padding:22px; background:#fff; }
                    .tracking-panel h4 { color:#143d31; margin:0 0 18px; font-size:1.05rem; }
                    .tracking-timeline { list-style:none; margin:0; padding:0; }
                    .tracking-timeline li { display:flex; position:relative; gap:14px; padding:0 0 25px; color:#6b8074; }
                    .tracking-timeline li:not(:last-child)::after { content:""; position:absolute; left:13px; top:28px; bottom:0; width:2px; background:#dce9dc; }
                    .tracking-timeline li.done::after { background:#a1cfaf; }
                    .tracking-timeline li div { padding-top:3px; }
                    .tracking-timeline li small { display:block; margin-top:5px; color:#667f70; font-weight:400; line-height:1.4; }
                    .tracking-timeline li span { display:grid; place-items:center; width:28px; height:28px; flex:none; border:1px solid #adc9b1; border-radius:50%; }
                    .tracking-timeline li.current,.tracking-timeline li.done { color:#175e45; font-weight:600; }
                    .tracking-timeline li.current span,.tracking-timeline li.done span { background:#1a7657; color:white; border-color:#1a7657; }
                    .tracking-actions { display:flex; flex-wrap:wrap; gap:10px; margin-top:18px; }
                    .tracking-actions a,.tracking-actions button { display:inline-flex; align-items:center; justify-content:center; padding:10px 16px; border:1px solid #98c7a8; border-radius:8px; background:white; color:#126345; text-decoration:none; font-weight:600; cursor:pointer; }
                    .tracking-actions .tracking-primary { background:#197355; border-color:#197355; color:white; }
                    .tracking-actions form { margin:0; }
                    .tracking-person { font-size:1.1rem; font-weight:700; color:#143d31; }
                    .tracking-location { margin-top:16px; padding:18px; background:#fff; border:1px solid #dce9dc; border-radius:12px; color:#143d31; }
                    .tracking-location > strong,.tracking-location > span { display:block; }
                    .tracking-location > span { margin-top:6px; color:#526e60; overflow-wrap:anywhere; }
                    .tracking-map-preview { position:relative; overflow:hidden; height:155px; margin:14px 0 10px; border-radius:9px; background:repeating-linear-gradient(32deg, transparent 0 34px, #fff 35px 40px, transparent 41px 72px), repeating-linear-gradient(112deg, #e8f0e2 0 43px, #fff 44px 49px, #e8f0e2 50px 80px); border:1px solid #dce9dc; }
                    .tracking-map-preview::after { content:""; position:absolute; left:50%; top:48%; width:20px; height:20px; border:5px solid white; border-radius:50%; background:#187353; box-shadow:0 2px 8px #54786766; transform:translate(-50%,-50%); }
                    .tracking-map-note { font-size:12px; line-height:1.45; color:#526e60; }
                    .tracking-aside { display:grid; gap:16px; }
                    @@media(max-width:700px) { .tracking-detail-layout { grid-template-columns:1fr; } .tracking-intro { padding:20px; } .tracking-picker summary { padding:12px 14px; } }
                </style>
                @{
                    var selectedTask = long.TryParse(Request.Query["taskId"], out var requestedTask) && Model.Tasks.Any(x => x.Task.Id == requestedTask) ? requestedTask : Model.Tasks.FirstOrDefault()?.Task.Id;
                    var activeEntry = Model.Tasks.FirstOrDefault(x => x.Task.Id == selectedTask);
                }
                <div class="tracking-intro">
                    <div class="db-tracking-header"><div><span class="db-tracking-eyebrow">TASK PROGRESS</span><h2>Track your tasks</h2><p>See the progress of your assigned work in one place.</p></div></div>
                    @if (activeEntry is not null) {
                        <span class="tracking-picker-label" id="tracking-picker-label">Select a task</span>
                        <details class="tracking-picker">
                            <summary aria-labelledby="tracking-picker-label tracking-picker-current"><span><strong id="tracking-picker-current">@activeEntry.Task.Title</strong><small>@(activeEntry.IsClient ? "Posted task" : "Accepted job") · #@activeEntry.Task.Id</small></span><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></summary>
                            <nav class="tracking-picker-options" aria-label="Tasks available to track">
                                @foreach (var entry in Model.Tasks) {
                                    <a href="/Tracking?taskId=@entry.Task.Id" aria-current="@(entry.Task.Id == selectedTask ? "page" : null)"><span><strong>@entry.Task.Title</strong><small>@(entry.IsClient ? "Posted task" : "Accepted job") · #@entry.Task.Id</small></span>@if(entry.Task.Id == selectedTask) { <span class="tracking-picker-check" aria-hidden="true">✓</span> }</a>
                                }
                            </nav>
                        </details>
                    }
                </div>
                @if (Model.Unavailable) { <div class="db-tracking-empty"><h3>Tracking is unavailable</h3><p>Please check the database connection and try again.</p></div> }
                else if (Model.Tasks.Count == 0) { <div class="db-tracking-empty"><span class="tracking-empty-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7"><path d="M4 12h4l3-6 3 12 2-6h4"/></svg></span><h3>No tasks to track</h3><p>Accepted tasks and their progress appear here.</p><a class="workspace-action" href="/FindPost">Explore Find &amp; Post</a></div> }
                @foreach (var item in Model.Tasks.Where(x => x.Task.Id == selectedTask)) {
                    var task = item.Task;
                    var status = Array.IndexOf(TaskProgress.States, task.Status);
                    var label = TaskProgress.Label(task.Status);
                    var steps = TaskProgress.Labels;
                    <article class="db-tracking-card"><div class="db-tracking-card-head"><div><span class="db-tracking-eyebrow">@(item.IsClient ? "YOUR POSTED TASK" : "YOUR ACCEPTED JOB")</span><h3>@task.Title</h3><p>@task.Category · @task.Location</p></div><span class="db-tracking-status">@label</span></div>
                    <div class="db-tracking-details"><div><span>Client</span><strong>@item.Client</strong></div><div><span>Freelancer</span><strong>@item.Freelancer</strong></div><div><span>Task budget</span><strong>@task.BudgetDisplay</strong></div><div><span>Schedule</span><strong>@(string.IsNullOrWhiteSpace(task.ScheduleText) ? "To be arranged" : task.ScheduleText)</strong></div></div>
                    <div class="tracking-detail-layout"><section class="tracking-panel"><h4>Task progress</h4>
                    @if (status >= 0) { var descriptions = new[] { "The booking or offer has been accepted.", "The freelancer is travelling to the work location.", "The freelancer has confirmed arrival. Travel tracking stops.", "The freelancer is performing the agreed work.", "The client checks the completion note and finished work.", "The client has confirmed completion. Reviews are available." }; <ol class="tracking-timeline">@for (var i = 0; i < steps.Length; i++) { <li class="@(i == 0 ? "done" : status == i ? "current" : status > i ? "done" : "")"><span>@(i == 0 || status > i ? "✓" : (i + 1).ToString())</span><div>@steps[i]<small>@descriptions[i]</small>
@{ var recorded = Model.Progress.LastOrDefault(x => x.TaskId == task.Id && x.Status == TaskProgress.States[i]); }
@if (recorded is not null) { <small>@recorded.CreatedAtUtc.AddHours(8).ToString("MMM d, yyyy h:mm tt") PHT</small> }</div></li> }</ol> }

@if (!string.IsNullOrEmpty(Model.Notice)) { <p role="status">@Model.Notice</p> }
@if (Model.ProgressUnavailable) { <p role="alert">Progress updates need the task_progress_events database table.</p> }
@{ var finished = Model.Progress.LastOrDefault(x => x.TaskId == task.Id && x.Status == "awaiting-confirmation"); }
@if (finished is not null) { <div class="tracking-completion"><strong>Completion note</strong><p style="white-space:pre-wrap">@finished.Note</p>@if (finished.PhotoType is not null) { <a href="/Tracking?handler=CompletionPhoto&amp;eventId=@finished.Id" target="_blank" rel="noopener"><img src="/Tracking?handler=CompletionPhoto&amp;eventId=@finished.Id" alt="Completion photo" loading="lazy" style="max-width:100%;max-height:240px;border-radius:8px" /></a> }</div> }
<div class="tracking-actions">
@if (!Model.ProgressUnavailable && task.Status == "on-the-way")
{
    var requested = Model.Progress.Any(x => x.TaskId == task.Id && x.Status == TaskProgress.ArrivalRequest);
    <section class="tracking-arrival" data-arrival-panel data-task-id="@task.Id" data-arrival-requested="@(requested ? "true" : "false")" aria-labelledby="arrivalTitle-@task.Id">
        <h3 id="arrivalTitle-@task.Id">Confirm arrival</h3>
        @if (item.IsClient)
        {
            <p>@(requested ? "The freelancer asked you to confirm their arrival. Confirm once they are at the agreed location." : "The freelancer is travelling to the agreed task location.")</p>
            @if (requested)
            {
                <form method="post" asp-page-handler="Advance">
                    <input type="hidden" name="id" value="@task.Id" />
                    <button name="action" value="confirm-arrival" type="submit" class="tracking-primary">Confirm freelancer arrival</button>
                </form>
            }
        }
        else
        {
            <p data-arrival-status role="status">I’ve arrived becomes available once a recent, accurate location is within 100 m of the agreed pin.</p>
            <div class="tracking-arrival-buttons">
                <form method="post" asp-page-handler="Advance" data-arrival-form hidden>
                    <input type="hidden" name="id" value="@task.Id" />
                    <button name="action" value="arrive" type="submit" class="tracking-primary">I’ve arrived</button>
                </form>
                <button type="button" class="db-tracking-link" data-arrival-retry>Retry location</button>
            </div>
            @if (requested)
            {
                <p>Waiting for the client to confirm your arrival.</p>
            }
            else
            {
                <form method="post" asp-page-handler="Advance">
                    <input type="hidden" name="id" value="@task.Id" />
                    <button name="action" value="request-arrival" type="submit" class="db-tracking-link">Ask client to confirm arrival</button>
                </form>
            }
        }
    </section>
}
@if (!Model.ProgressUnavailable && !item.IsClient && task.Status is "assigned" or "arrived" or "in-progress") {
var action = task.Status switch { "assigned" => "travel", "on-the-way" => "arrive", "arrived" => "start", _ => "finish" };
var button = action switch { "travel" => "Start travel & share location", "arrive" => "I’ve arrived", "start" => "Start work", _ => "Mark work finished" };
<form method="post" asp-page-handler="Advance" enctype="multipart/form-data" class="tracking-progress-form"><input type="hidden" name="id" value="@task.Id" /><input type="hidden" name="action" value="@action" />
@if (action == "finish") { <label>Completion note<textarea name="note" maxlength="1000" rows="3" required placeholder="Describe the work completed"></textarea></label><div class="completion-upload" data-completion-upload><div class="completion-upload-heading"><strong>Completion photo</strong><span>Optional</span></div><p>Show the client your finished work.</p><label class="completion-upload-picker"><input type="file" name="photo" accept="image/jpeg,image/png,image/webp" aria-label="Choose completion photo" /><svg viewBox="0 0 24 24" width="26" height="26" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="3"/><circle cx="8" cy="9" r="1.5"/><path d="m4 17 5-5 4 4 3-3 5 5"/></svg><strong data-upload-label>Choose a photo</strong><small>JPG, PNG or WEBP · Up to 5 MB</small></label><div class="completion-upload-preview" hidden><img alt="Selected completion photo preview" /><div><strong data-upload-name></strong><small data-upload-size></small><button type="button" data-upload-remove>Remove photo</button></div></div><p class="completion-upload-feedback" role="status" aria-live="polite" hidden></p></div> }
@if (action == "travel") { <p>Allow location access and keep Tracking open while travelling. Sharing stops when you mark your arrival.</p> }
<button type="submit" class="tracking-primary">@button</button></form>
}
@if (!Model.ProgressUnavailable && item.IsClient && task.Status == "awaiting-confirmation") { <form method="post" asp-page-handler="Advance"><input type="hidden" name="id" value="@task.Id" /><input type="hidden" name="action" value="confirm" /><button type="submit" class="tracking-primary">Confirm completion</button></form> }
@if (task.Status == "disputed") { <p>This older task is paused. Contact support through Safety &amp; Reports.</p> }
@if (task.Status == "cancelled") { <p>This task was cancelled.</p> }

<a href="@(item.IsClient ? "/MarketTasks?view=mine" : "/MyJobs?view=active")">@(item.IsClient ? "Open My Tasks" : "Open My Jobs")</a>
</div>
@if (task.Status == "completed")
{
    var canReviewFreelancer = item.IsClient && item.FreelancerId > 0 && !await Model.HasReviewAsync(task.Id);
    var canReviewCommunity = await Model.CanWriteCommunityReviewAsync();
    if (canReviewFreelancer || canReviewCommunity)
    {
    <section id="completed-reviews" class="tracking-completed-reviews">
        <h4>Leave a review</h4>
@if (canReviewFreelancer) {
    <details class="tracking-review tracking-actions"><summary>Review your freelancer</summary><form method="post" asp-page="/MarketTasks" asp-page-handler="Review"><input type="hidden" name="id" value="@task.Id" /><input type="hidden" name="fromTracking" value="true" /><label>Rating<select name="rating" required><option value="">Choose a rating</option>@for (var rating = 5; rating >= 1; rating--) { <option value="@rating">@rating / 5</option> }</select></label><label>Feedback (optional)<textarea name="feedback" maxlength="1000" rows="3"></textarea></label><button type="submit" class="tracking-primary">Submit review</button></form></details>
}
        @if (canReviewCommunity)
        {
        <details class="tracking-review tracking-actions">
            <summary>Write a community review</summary>
            <form method="post" asp-page-handler="Feedback" data-submit-pending="Saving…">
                <input type="hidden" name="taskId" value="@task.Id" />
                <p>Share your experience using Tap, Task, Done.</p>
                <label>Rating
                    <select name="rating" required>
                        <option value="">Choose a rating</option>
                        @for (var rating = 5; rating >= 1; rating--) { <option value="@rating">@rating / 5</option> }
                    </select>
                </label>
                <label>Your feedback<textarea name="feedback" maxlength="500" rows="3" required></textarea></label>
                <p>Your display name, rating, and feedback will appear on the landing page.</p>
                <button type="submit" class="tracking-primary">Publish community review</button>
            </form>
        </details>
        }
    </section>
    }
}
</section>
                    <aside class="tracking-aside"><section class="tracking-panel"><h4>@(item.IsClient ? "Freelancer" : "Client")</h4><div class="tracking-person" data-account-id="@(item.IsClient ? item.FreelancerId : task.ClientId)">@(item.IsClient ? item.Freelancer : item.Client)</div><p>Assigned to this task</p>@if (Model.SharedPhones.TryGetValue(task.Id, out var sharedPhone)) { <p class="tracking-shared-phone"><strong>Mobile number</strong> <bdi dir="ltr" style="user-select: all">@sharedPhone</bdi></p> }<div class="tracking-actions"><a href="/Messages?taskId=@task.Id">Message @(item.IsClient ? "freelancer" : "client")</a></div></section><section class="tracking-location"><strong>Task location</strong><span>@(string.IsNullOrWhiteSpace(task.Location) ? "Location not provided" : task.Location)</span>
@if (!string.IsNullOrWhiteSpace(task.ArrivalInstructions))
{
    <div class="tracking-arrival location-arrival-note">
        <strong>Arrival instructions</strong>
        <p>@task.ArrivalInstructions</p>
    </div>
}
<div class="job-location" data-job-location data-task-id="@task.Id" data-viewer-id="@User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value" data-freelancer="@(!item.IsClient ? "true" : "false")" data-progress-status="@task.Status" data-active="@(task.Status == "on-the-way" ? "true" : "false")" data-address="@task.Location" data-route-url="@(Configuration["Routing:BaseUrl"] ?? (HostEnvironment.EnvironmentName == "Development" ? "https://router.project-osrm.org" : ""))">
<div class="map-place-search" data-map-search><label>Search a place on the map<input maxlength="120" placeholder="Bacolor address or landmark" /></label><button type="button">Search map</button><div class="location-search-results"></div><p role="status">Search only changes your map view. To change the work destination, request changes in Confirm work details.</p></div><div class="job-location-map" data-map aria-label="Task and freelancer location map"></div>
<p class="job-location-status" data-map-status hidden role="status">Loading map…</p>
<p class="job-location-status" data-route-status hidden role="status">Waiting for a destination pin.</p>

<p class="job-location-status" data-location-status hidden role="status">Waiting for a location update.</p>
@if (!item.IsClient && task.Status == "on-the-way") {
<div class="tracking-actions"><button type="button" data-share>Enable location / Try again</button><button type="button" data-stop hidden>Stop sharing</button></div>
<p class="job-location-status" data-sharing-status hidden role="status"></p>
}
<form hidden data-location-token>@Html.AntiForgeryToken()</form>
</div>
</section></aside></div></article>
                }
            </section>
            }

        </main>

    </div>


    @if (Request.Query["legacy"] == "1") { <script src="~/js/tracking.js" asp-append-version="true"></script> }


<script src="~/js/completion-photo.js" asp-append-version="true" defer></script>
<script src="~/js/job-location.js" asp-append-version="true" defer></script>

    <partial name="_TaskHelp" />
</body>
</html>
~~~~

### Pages/Tracking.cshtml.cs

~~~~csharp
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
[RequestSizeLimit(6 * 1024 * 1024)]
public class TrackingModel(ApplicationDbContext db, ILogger<TrackingModel> logger) : PageModel
{
    public async Task<IActionResult> OnPostFeedbackAsync(long taskId, int rating, string? feedback, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        feedback = feedback?.Trim();
        if (!ModelState.IsValid || rating is < 1 or > 5 || feedback is not { Length: > 0 and <= 500 } ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            Notice = "Choose a rating, write up to 500 characters, before publishing.";
            return RedirectToPage("/Tracking", new { taskId });
        }

        if (!await db.MarketplaceTasks.AnyAsync(task => task.Id == taskId && task.Status == "completed" &&
            db.MarketplaceOffers.Any(offer => offer.TaskId == task.Id && offer.Status == "accepted" &&
                (task.ClientId == userId || offer.FreelancerId == userId)), ct))
            return NotFound();

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO community_feedback (UserId, Rating, Message, ShowDisplayName)
                VALUES ({userId}, {rating}, {feedback}, TRUE)
                ON DUPLICATE KEY UPDATE UserId = UserId
                """, ct);
            Notice = "Your feedback is now visible on the landing page.";
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community feedback could not be saved.");
            Notice = "Feedback could not be saved. Please try again later.";
        }
        return RedirectToPage("/Tracking", new { taskId });
    }

    public async Task<IActionResult> OnPostAdvanceAsync(long id, string? action, string? note, IFormFile? photo, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        byte[]? encrypted = null; string? type = null;
        if (photo is { Length: > 0 })
        {
            if (action != "finish" || photo.Length > 5 * 1024 * 1024) { Notice = "Choose a completion photo up to 5 MB."; return RedirectToPage(new { taskId = id }); }
            using var memory = new MemoryStream();
            await using var stream = photo.OpenReadStream();
            await stream.CopyToAsync(memory, ct);
            type = ProfilePhotoModel.ImageType(memory.ToArray());
            if (type is null) { Notice = "Choose a JPG, PNG or WEBP completion photo."; return RedirectToPage(new { taskId = id }); }
            var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            if (!environment.IsDevelopment() && !HttpContext.RequestServices.GetRequiredService<IdentityVerificationAvailability>().Enabled)
            { Notice = "Secure photo storage is not configured. Submit a completion note without a photo."; return RedirectToPage(new { taskId = id }); }
            encrypted = PhotoProtector(id).Protect(memory.ToArray());
        }
        try
        {
            var result = await TaskProgress.AdvanceAsync(db, UserId, id, action, note, encrypted, type, ct);
            Notice = result.Message;
            return RedirectToPage(new { taskId = id, shareLocation = result.Success && action == "travel" ? "1" : null });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Progress update failed"); Notice = "Progress storage is unavailable. Please check the database setup."; return RedirectToPage(new { taskId = id }); }
    }

    private Microsoft.AspNetCore.DataProtection.IDataProtector PhotoProtector(long taskId) =>
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("TaskProgress.Photo.v1", taskId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public async Task<IActionResult> OnGetCompletionPhotoAsync(long eventId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (UserId <= 0) return Challenge();
        var item = await db.Set<TaskProgressEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId &&
            db.MarketplaceTasks.Any(t => t.Id == x.TaskId && (t.ClientId == UserId || db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.Status == "accepted" && o.FreelancerId == UserId))), ct);
        if (item?.Photo is null) return NotFound();
        try
        {
            var bytes = PhotoProtector(item.TaskId).Unprotect(item.Photo);
            var type = ProfilePhotoModel.ImageType(bytes);
            return type is null ? NotFound() : File(bytes, type);
        }
        catch (System.Security.Cryptography.CryptographicException) { return StatusCode(503); }
    }
    [TempData] public string? Notice { get; set; }
    public List<TaskProgressEvent> Progress { get; private set; } = [];
    public Dictionary<long, WorkAgreement> Agreements { get; private set; } = [];
    public Dictionary<long, TaskDestination> Destinations { get; private set; } = [];
    public bool ProgressUnavailable { get; private set; }
    public record Coordinates(long TaskId, double Latitude, double Longitude, double? AccuracyMeters);
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetDestinationAsync(long taskId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        if (!await db.MarketplaceTasks.AnyAsync(t => t.Id == taskId && (t.ClientId == UserId ||
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.Status == "accepted" && o.FreelancerId == UserId)), ct)) return NotFound();
        try
        {
            return new JsonResult(await db.Set<TaskDestination>().AsNoTracking().Where(x => x.TaskId == taskId)
                .Select(x => new { x.Latitude, x.Longitude }).SingleOrDefaultAsync(ct));
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Destination storage unavailable"); return StatusCode(503, new { message = "Destination storage is not ready." }); }
    }

    public IActionResult OnPostDestination([FromBody] Coordinates input)
    {
        if (UserId <= 0) return Challenge();
        return StatusCode(409, new { message = "Change the destination through the final work agreement. Both participants must confirm it." });
    }
    public async Task<IActionResult> OnGetLocationAsync(long taskId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        var assignment = await (from task in db.MarketplaceTasks.AsNoTracking()
            join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
            where task.Id == taskId && offer.Status == "accepted" && (task.ClientId == UserId || offer.FreelancerId == UserId)
            select new { task.Status, offer.FreelancerId }).FirstOrDefaultAsync(ct);
        if (assignment is null) return NotFound();
        try
        {
            var latest = await db.Set<FreelancerLocation>().AsNoTracking()
                .Where(x => x.TaskId == taskId && x.UserId == assignment.FreelancerId)
                .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id)
                .Select(x => new { x.Latitude, x.Longitude, x.AccuracyMeters, x.RecordedAtUtc }).FirstOrDefaultAsync(ct);
            var active = assignment.Status == "on-the-way";
            var canArrive = active && UserId == assignment.FreelancerId &&
                await TaskProgress.NearDestinationAsync(db, taskId, UserId, ct);
            var arrivalRequested = active && await db.Set<TaskProgressEvent>().AnyAsync(x =>
                x.TaskId == taskId && x.ActorId == assignment.FreelancerId && x.Status == TaskProgress.ArrivalRequest, ct);
            return new JsonResult(new { active, status = assignment.Status, location = latest, canArrive, arrivalRequested });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Location storage unavailable"); return StatusCode(503, new { message = "Location storage is not ready. Please contact support." }); }
    }

    public async Task<IActionResult> OnPostLocationAsync([FromBody] Coordinates input, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid || input is null || !double.IsFinite(input.Latitude) || !double.IsFinite(input.Longitude)
            || input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180
            || (input.AccuracyMeters is double accuracy && (!double.IsFinite(accuracy) || accuracy is < 0 or > 100000))) return BadRequest();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var task = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {input.TaskId} FOR UPDATE").ToListAsync(ct);
            if (task.Count != 1 || task[0].Status != "on-the-way" ||
                !await db.MarketplaceOffers.AnyAsync(o => o.TaskId == input.TaskId && o.FreelancerId == UserId && o.Status == "accepted", ct)) return NotFound();
            var now = DateTime.UtcNow;
            if (await db.Set<FreelancerLocation>().AnyAsync(x => x.TaskId == input.TaskId && x.UserId == UserId && x.RecordedAtUtc > now.AddSeconds(-10), ct))
                return StatusCode(429);
            db.Set<FreelancerLocation>().Add(new FreelancerLocation { TaskId = input.TaskId, UserId = UserId,
                Latitude = input.Latitude, Longitude = input.Longitude, AccuracyMeters = input.AccuracyMeters, RecordedAtUtc = now });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new JsonResult(new { saved = true, canArrive = await TaskProgress.NearDestinationAsync(db, input.TaskId, UserId, ct) });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Unable to save freelancer location"); return StatusCode(503, new { message = "Location storage is not ready. Please contact support." }); }
    }
    public Task<bool> HasReviewAsync(long taskId) => db.MarketplaceReviews.AnyAsync(r => r.TaskId == taskId, HttpContext.RequestAborted);
    public async Task<bool> CanWriteCommunityReviewAsync()
    {
        try
        {
            return !await db.CommunityFeedback.AnyAsync(review => review.UserId == UserId, HttpContext.RequestAborted);
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community review status could not be loaded.");
            return false;
        }
    }

    public record TrackingItem(MarketplaceTask Task, string Client, string Freelancer, bool IsClient) { public int FreelancerId { get; init; } }
    public List<TrackingItem> Tasks { get; private set; } = [];
    public bool Unavailable { get; private set; }
    public Dictionary<long, string> SharedPhones { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0) return Challenge();
        if (Request.Query["legacy"] == "1") return Page();
        try
        {
            Tasks = await (from task in db.MarketplaceTasks.AsNoTracking()
                join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                join freelancer in db.Users.AsNoTracking() on offer.FreelancerId equals freelancer.Id
                where offer.Status == "accepted" && (task.ClientId == userId || offer.FreelancerId == userId)
                orderby task.Id descending
                select new TrackingItem(task, ((client.FirstName ?? "") + " " + (client.LastName ?? "")).Trim(),
                    ((freelancer.FirstName ?? "") + " " + (freelancer.LastName ?? "")).Trim(), task.ClientId == userId) { FreelancerId = offer.FreelancerId }).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load task tracking"); Unavailable = true;
        }
        if (!Unavailable) {
            try {
                var activeIds = Tasks.Where(x => TaskPhoneShare.IsActive(x.Task.Status)).Select(x => x.Task.Id).ToArray();
                SharedPhones = await (from c in ConversationAccess.VisibleTo(db, userId)
                    join t in db.MarketplaceTasks on c.TaskId equals t.Id
                    join o in db.MarketplaceOffers on new { c.TaskId, c.FreelancerId } equals new { o.TaskId, o.FreelancerId }
                    join s in db.Set<TaskPhoneShare>() on c.Id equals s.ConversationId
                    join contact in db.Set<UserContact>() on s.UserId equals contact.UserId
                    where activeIds.Contains(t.Id) && o.Status == "accepted" && contact.PhoneNumber != ""
                        && s.UserId == (t.ClientId == userId ? c.FreelancerId : t.ClientId)
                    select new { t.Id, contact.PhoneNumber }).ToDictionaryAsync(x => x.Id, x => x.PhoneNumber, ct);
            } catch (MySqlConnector.MySqlException ex) { logger.LogWarning(ex, "Shared contact details unavailable"); }
            try {
                var ids = Tasks.Select(x => x.Task.Id).ToList();
                var terms = await db.Set<WorkAgreement>().AsNoTracking().Where(a => ids.Contains(a.TaskId)).OrderByDescending(a => a.Version).ToListAsync(ct);
                Agreements = terms.GroupBy(a => a.TaskId).ToDictionary(g => g.Key, g => g.First());
                Destinations = await db.Set<TaskDestination>().AsNoTracking().Where(d => ids.Contains(d.TaskId)).ToDictionaryAsync(d => d.TaskId, ct);
                Progress = await db.Set<TaskProgressEvent>().AsNoTracking().Where(x => ids.Contains(x.TaskId))
                    .OrderBy(x => x.Id).Select(x => new TaskProgressEvent { Id = x.Id, TaskId = x.TaskId, Status = x.Status, Note = x.Note, PhotoType = x.PhotoType, CreatedAtUtc = x.CreatedAtUtc }).ToListAsync(ct);
            } catch (MySqlConnector.MySqlException) { ProgressUnavailable = true; }
        }
        return Page();
    }
}
~~~~

### Data/TaskProgress.cs

~~~~csharp
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class TaskProgressEvent
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int ActorId { get; set; }
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public byte[]? Photo { get; set; }
    public string? PhotoType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public static class TaskProgress
{
    public static readonly string[] States = ["assigned", "on-the-way", "arrived", "in-progress", "awaiting-confirmation", "completed"];
    public static readonly string[] Labels = ["Booking accepted", "On the way", "Arrived", "Work in progress", "Awaiting client confirmation", "Completed"];
    public static string Label(string status) => Array.IndexOf(States, status) is var i && i >= 0 ? Labels[i] : status == "disputed" ? "Paused" : status == "cancelled" ? "Cancelled" : status == "expired" ? "Expired" : status;
    public static (string From, string To) Transition(string? action) => action switch
    {
        "travel" => ("assigned", "on-the-way"), "arrive" or "confirm-arrival" => ("on-the-way", "arrived"),
        "start" => ("arrived", "in-progress"), "finish" => ("in-progress", "awaiting-confirmation"),
        "confirm" => ("awaiting-confirmation", "completed"), _ => ("", "")
    };
    public record Result(bool Success, string Message);
    public static bool CanAdvance(string status, string? action, bool isClient, bool isAssignedFreelancer, bool hasDestination)
    {
        var (from, _) = Transition(action);
        return from != "" && status == from && (action is "confirm" or "confirm-arrival" ? isClient : isAssignedFreelancer)
            && (action != "travel" || hasDestination);
    }

    public const string ArrivalRequest = "arrival-requested";

    public static async Task<bool> NearDestinationAsync(ApplicationDbContext db, long taskId, int freelancerId, CancellationToken ct)
    {
        var pin = await db.Set<TaskDestination>().AsNoTracking().SingleOrDefaultAsync(x => x.TaskId == taskId, ct);
        if (pin is null) return false;
        var fix = await db.Set<FreelancerLocation>().AsNoTracking()
            .Where(x => x.TaskId == taskId && x.UserId == freelancerId)
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (fix is null || fix.RecordedAtUtc < DateTime.UtcNow.AddSeconds(-45) || fix.AccuracyMeters is not (>= 0 and <= 50))
            return false;
        const double radians = Math.PI / 180;
        var lat = Math.Sin((pin.Latitude - fix.Latitude) * radians / 2);
        var lng = Math.Sin((pin.Longitude - fix.Longitude) * radians / 2);
        var a = lat * lat + Math.Cos(pin.Latitude * radians) * Math.Cos(fix.Latitude * radians) * lng * lng;
        return 12_742_000 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1))) <= 100;
    }

    public static async Task<Result> AdvanceAsync(ApplicationDbContext db, int actor, long id, string? action,
        string? note, byte[]? photo, string? photoType, CancellationToken ct)
    {
        var requestArrival = action == "request-arrival";
        var (from, to) = Transition(requestArrival ? "arrive" : action);
        if (actor <= 0 || from == "") return new(false, "This action is unavailable.");
        var participants = await (from t in db.MarketplaceTasks.AsNoTracking()
            join o in db.MarketplaceOffers.AsNoTracking() on t.Id equals o.TaskId
            where t.Id == id && o.Status == "accepted" select new { t.ClientId, o.FreelancerId }).SingleOrDefaultAsync(ct);
        if (participants is null || (actor != participants.ClientId && actor != participants.FreelancerId)) return new(false, "This action is unavailable.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var userId in new[] { participants.ClientId, participants.FreelancerId }.Distinct().Order())
            await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {userId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (await ModerationGuard.IsSuspendedAsync(db, actor, ct)) return new(false, "This account is suspended. Check your suspension notice.");
        var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct);
        var task = tasks.SingleOrDefault();
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
        if (task is null || freelancer is null || !CanAdvance(task.Status, requestArrival ? "arrive" : action, task.ClientId == actor, freelancer == actor, true))
            return new(false, "This action is unavailable or the task has already changed. Refresh and check its progress.");
        if (requestArrival || action == "confirm-arrival")
        {
            var requested = await db.Set<TaskProgressEvent>().AnyAsync(x => x.TaskId == id &&
                x.ActorId == freelancer.Value && x.Status == ArrivalRequest, ct);
            if (requestArrival)
            {
                if (requested) return new(true, "The client has already been asked to confirm your arrival.");
                db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = ArrivalRequest, CreatedAtUtc = DateTime.UtcNow });
                db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, id, "on-the-way",
                    "The freelancer asked you to confirm arrival. Open Tracking to confirm when they are there."));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(true, "Arrival confirmation requested.");
            }
            if (!requested) return new(false, "There is no arrival request to confirm.");
        }
        if (action == "arrive" && !await NearDestinationAsync(db, id, freelancer.Value, ct))
            return new(false, "A recent, accurate location near the task pin is needed. Retry location or ask the client to confirm.");
        if (action == "travel" && !await db.Set<TaskDestination>().AnyAsync(x => x.TaskId == id, ct))
            return new(false, "Ask the client to save the exact destination pin before starting travel.");
        if (action is "travel" or "start" && await FreelancerAvailability.ActiveTasks(db, freelancer.Value).AnyAsync(t => t.Id != id, ct))
            return new(false, "Finish your current travel/work before starting another job.");
        note = note?.Trim();
        if (action == "finish" && (string.IsNullOrWhiteSpace(note) || note.Length > 1000))
            return new(false, "Add a completion note (up to 1,000 characters).");
        task.Status = to;
        db.Set<TaskProgressEvent>().Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = to,
            Note = action == "finish" ? note : null, Photo = action == "finish" ? photo : null,
            PhotoType = action == "finish" ? photoType : null, CreatedAtUtc = DateTime.UtcNow });
        var text = action switch
        {
            "travel" => "The freelancer is on the way. Open Tracking to view their shared location.",
            "arrive" => "The freelancer has marked their arrival. Travel location sharing has stopped.",
            "confirm-arrival" => "The client confirmed your arrival. You can now start work.",
            "start" => "The freelancer started working on your task.",
            "finish" => "The freelancer marked the work finished. Open Tracking to review the completion note and confirm.",
            _ => "The client confirmed that the task is complete. You can now leave a review."
        };
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(action is "confirm" or "confirm-arrival" ? freelancer.Value : task.ClientId,
            id, action == "confirm" ? "completed" : action == "start" ? "started" : action == "finish" ? "finished" : to, text));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, Label(to) + ".");
    }
}
~~~~

### wwwroot/js/tracking.js

~~~~javascript
/* =========================================================
   TAP, TASK, DONE - TRACKING
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    // Use the same signed-in account label as every other sidebar.
    try {
        const account = JSON.parse(
            localStorage.getItem("tapTaskDoneAccount") || "{}"
        );
        const name = account.nickname || account.name || "User";
        const email = account.email || "user@email.com";
        const initials = name.split(/\s+/).filter(Boolean).slice(0, 2)
            .map(function (part) { return part.charAt(0); }).join("").toUpperCase() || "U";

        const sidebarName = document.getElementById("sidebarName");
        const sidebarEmail = document.getElementById("sidebarEmail");
        const sidebarAvatar = document.querySelector(".sidebar-account .account-avatar");

        if (sidebarName) sidebarName.textContent = name;
        if (sidebarEmail) sidebarEmail.textContent = email;
        if (sidebarAvatar) sidebarAvatar.textContent = initials;
    } catch (error) {
        console.error("Unable to load sidebar account.", error);
    }

    // =====================================================
    // ELEMENTS
    // =====================================================

    const completeButton =
        document.getElementById("completeButton");

    const completionMessage =
        document.getElementById("completionMessage");

    const reviewNotice =
        document.getElementById("reviewNotice");

    const reviewButton =
        document.getElementById("reviewButton");

    const demoFinishTaskButton =
        document.getElementById("demoFinishTaskButton");

    const reviewForm =
        document.getElementById("reviewForm");

    const submitReviewButton =
        document.getElementById("submitReviewButton");

    const statusText =
        document.getElementById("statusText");

    const onTheWayStep =
        document.getElementById("onTheWayStep");

    const inProgressStep =
        document.getElementById("inProgressStep");

    const completedStep =
        document.getElementById("completedStep");

    const completeTaskModal = document.getElementById("completeTaskModal");
    const confirmCompletionButton = document.getElementById("confirmCompletionButton");
    const notYetButton = document.getElementById("notYetButton");
    const completeModalTaskTitle = document.getElementById("completeModalTaskTitle");
    const completeModalProvider = document.getElementById("completeModalProvider");
    const closeCompletionModal = document.getElementById("closeCompletionModal");
    const reportProviderButton = document.getElementById("reportProviderButton");

    let currentTask = null;

    try {
        currentTask = JSON.parse(
            localStorage.getItem("tapTaskDoneCurrentTask") || "null"
        );
    } catch (error) {
        currentTask = null;
    }

    // The globally selected task may belong to another signed-in demo account.
    try {
        const account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}");
        const email = String(account.email || "").toLowerCase();
        const tasks = JSON.parse(localStorage.getItem("tapTaskDoneTasks") || "[]");
        const eligible = Array.isArray(tasks) ? tasks.filter(function (task) {
            return email && [task.clientEmail, task.providerEmail].some(function (value) { return value && String(value).toLowerCase() === email; }) && ["assigned", "completed", "client-confirmed"].includes(task.status);
        }) : [];
        currentTask = eligible.find(function (task) { return currentTask && task.id === currentTask.id; }) || eligible[eligible.length - 1] || null;
    } catch (error) { currentTask = null; }

    if (!currentTask) {
        const trackingContent = document.querySelector(".tracking-content");

        if (trackingContent) {
            trackingContent.innerHTML = "";
            const emptyState = document.createElement("div");
            emptyState.className = "tracking-empty-state";
            const icon = document.createElement("div");
            icon.className = "tracking-empty-icon";
            icon.textContent = "○";
            const heading = document.createElement("h2");
            heading.textContent = "No active task";
            const message = document.createElement("p");
            message.textContent = "Post a new task to start tracking its progress.";
            const button = document.createElement("button");
            button.type = "button";
            button.textContent = "Post a task";
            button.addEventListener("click", function () {
                window.location.href = "/FindPost?mode=post";
            });
            emptyState.append(icon, heading, message, button);
            trackingContent.append(emptyState);
        }

        return;
    }

    const taskTitle = document.getElementById("taskTitle");
    const taskTab = document.querySelector(".task-tab");
    const taskOverviewTitle = document.getElementById("taskOverviewTitle");
    const taskOverviewMeta = document.getElementById("taskOverviewMeta");
    const trackingBudget = document.getElementById("trackingBudget");
    const trackingSchedule = document.getElementById("trackingSchedule");
    const trackingStatusSummary = document.getElementById("trackingStatusSummary");
    if (taskTitle) taskTitle.textContent = currentTask.title || "New task";
    if (taskTab) taskTab.textContent = currentTask.title || "New task";
    if (taskOverviewTitle) taskOverviewTitle.textContent = currentTask.title || "New task";
    if (taskOverviewMeta) {
        taskOverviewMeta.textContent = (currentTask.providerService || currentTask.category || "Service") + " · " + (currentTask.location || "Location to be confirmed");
    }
    if (trackingBudget) trackingBudget.textContent = currentTask.budget || "Budget to be confirmed";
    if (trackingSchedule) trackingSchedule.textContent = currentTask.schedule || "To be scheduled";

    const activeProviderName = currentTask.providerName || "Provider";
    const activeProviderService = currentTask.providerService || "Service provider";
    const providerNameElement = document.querySelector(".provider-info strong");
    const providerServiceElement = document.querySelector(".provider-info span");
    const providerAvatarElement = document.querySelector(".provider-card .provider-avatar");
    if (providerNameElement) providerNameElement.textContent = activeProviderName;
    if (providerServiceElement) providerServiceElement.textContent = activeProviderService;
    if (providerAvatarElement) providerAvatarElement.textContent = activeProviderName.slice(0, 2).toUpperCase();
    const providerRating = document.getElementById("providerRating");
    const messageProviderButton = document.getElementById("messageProviderButton");
    if (providerRating) providerRating.textContent = "★ " + (currentTask.providerReviews || "New freelancer");

    const reviewAvatar = document.querySelector(".review-avatar");
    const reviewProviderInfo = document.querySelector(".review-provider p");
    if (reviewAvatar) reviewAvatar.textContent = activeProviderName.slice(0, 2).toUpperCase();
    if (reviewProviderInfo) reviewProviderInfo.textContent = "★ " + activeProviderName + " · " + (currentTask.providerReviews || "New freelancer");


    // =====================================================
    // INITIAL STATE
    // =====================================================

    let taskStatus = currentTask.status === "client-confirmed" ? "client-confirmed" : currentTask.status === "completed" ? "completed" : (currentTask.freelancerProgress || "on-the-way");

    // The rating modal is shown only immediately after the client confirms completion,
    // never merely because the Tracking page was opened.
    let shouldShowRating =
        sessionStorage.getItem("tapTaskDoneShowRating") === "true";

    function showRatingForm() {
        if (!reviewNotice) return;
        reviewNotice.classList.add("is-open");
        reviewNotice.style.setProperty("display", "flex", "important");
    }

    if (!taskStatus) {
        taskStatus = "on-the-way";

        localStorage.setItem(
            "tapTaskDoneTrackingStatus",
            taskStatus
        );
    }

    // Keep the task list, the active tracking card, and notifications in sync.
    // It uses local storage only while the database is not connected.
    function syncTaskStatus(status, notification, href) {
        try {
            const tasks = JSON.parse(localStorage.getItem("tapTaskDoneTasks") || "[]");
            if (Array.isArray(tasks) && currentTask.id) {
                const updated = tasks.map(function (task) {
                    if (task.id !== currentTask.id) return task;
                    return Object.assign({}, task, { status: status, freelancerProgress: taskStatus, updatedAt: "Just now" });
                });
                const latest = updated.find(function (task) { return task.id === currentTask.id; });
                if (latest) currentTask = latest;
                localStorage.setItem("tapTaskDoneTasks", JSON.stringify(updated));
                localStorage.setItem("tapTaskDoneCurrentTask", JSON.stringify(currentTask));
            }
        } catch (error) { }

        if (!notification) return;
        let notifications = [];
        try { notifications = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { notifications = []; }
        if (!Array.isArray(notifications)) notifications = [];
        notifications.unshift({ id: "notice-" + Date.now(), message: notification, href: href || "/Tracking", read: false, createdAt: "Just now" });
        localStorage.setItem("tapTaskDoneNotifications", JSON.stringify(notifications.slice(0, 30)));
    }


    // =====================================================
    // UPDATE TRACKING DISPLAY
    // =====================================================

    function updateTracking() {

        function setStatusLabel(label) {
            statusText.textContent = label;
            if (trackingStatusSummary) trackingStatusSummary.textContent = label;
        }

        // Reset states
        onTheWayStep.classList.remove("completed", "active");
        inProgressStep.classList.remove("completed", "active");
        completedStep.classList.remove("completed", "active");

        if (demoFinishTaskButton) {
            demoFinishTaskButton.style.display =
                (taskStatus === "completed" || taskStatus === "client-confirmed")
                    ? "none"
                    : "block";
            demoFinishTaskButton.textContent =
                taskStatus === "on-the-way" ? "Start work" : "Mark work finished";
        }


        // -------------------------------------------------
        // ON THE WAY
        // -------------------------------------------------

        if (taskStatus === "on-the-way") {

            onTheWayStep.classList.add("active");

            setStatusLabel("On the way");

            completeButton.disabled = true;

            completionMessage.textContent =
                "Waiting for the freelancer to finish the task.";

            reviewNotice.style.display = "none";
        }


        // -------------------------------------------------
        // IN PROGRESS
        // -------------------------------------------------

        else if (taskStatus === "in-progress") {

            onTheWayStep.classList.add("completed");

            inProgressStep.classList.add("active");

            setStatusLabel("In progress");

            completeButton.disabled = true;

            completionMessage.textContent =
                "The freelancer is currently working on your task.";

            reviewNotice.style.display = "none";
        }


        // -------------------------------------------------
        // COMPLETED
        // -------------------------------------------------

        else if (taskStatus === "completed") {

            onTheWayStep.classList.add("completed");

            inProgressStep.classList.add("completed");

            completedStep.classList.add("active");

            setStatusLabel("Completed");

            completeButton.disabled = false;

            completionMessage.textContent =
                "The freelancer has finished the task. You can now confirm completion.";

            reviewNotice.style.display = "none";
        }


        // -------------------------------------------------
        // CLIENT CONFIRMED
        // -------------------------------------------------

        else if (taskStatus === "client-confirmed") {

            onTheWayStep.classList.add("completed");

            inProgressStep.classList.add("completed");

            completedStep.classList.add("completed");

            setStatusLabel("Task completed");

            completeButton.disabled = true;

            completeButton.textContent =
                "Task completed ✓";

            completionMessage.textContent =
                "You confirmed that the task has been completed.";

            // A confirmed task must stay ready for rating until the review is submitted.
            // This also recovers a rating modal that was interrupted by a refresh.
            const hasPendingRating =
                shouldShowRating ||
                localStorage.getItem("tapTaskDonePendingRating") === "true" ||
                taskStatus === "client-confirmed";

            if (hasPendingRating) showRatingForm();
            else reviewNotice.style.display = "none";
            shouldShowRating = false;
            sessionStorage.removeItem("tapTaskDoneShowRating");
        }
    }


    // =====================================================
    // COMPLETE BUTTON
    // =====================================================

    completeButton.addEventListener("click", function () {

        // Safety check
        if (taskStatus !== "completed") {
            return;
        }


        if (completeModalTaskTitle) {
            completeModalTaskTitle.textContent = currentTask.title || "Your task";
        }
        if (completeModalProvider) {
            completeModalProvider.textContent = currentTask.providerName || "Provider";
        }

        if (completeTaskModal) {
            completeTaskModal.classList.remove("hidden");
        }

    });

    function confirmTaskCompletion() {
        if (taskStatus !== "completed") return;

        if (completeTaskModal) completeTaskModal.classList.add("hidden");


        // Save confirmed state
        taskStatus = "client-confirmed";

        // Show the rating modal immediately in this same page session.
        shouldShowRating = true;
        sessionStorage.setItem("tapTaskDoneShowRating", "true");
        localStorage.setItem("tapTaskDonePendingRating", "true");

        localStorage.setItem(
            "tapTaskDoneTrackingStatus",
            taskStatus
        );
        syncTaskStatus("client-confirmed", "Task completion confirmed. You can now leave a review.", "/Tracking");


        // Update screen
        updateTracking();

        // Keep this explicit so the form opens in the same click, not only on refresh.
        showRatingForm();
    }

    if (confirmCompletionButton) {
        confirmCompletionButton.addEventListener("click", confirmTaskCompletion);
    }

    if (notYetButton) {
        notYetButton.addEventListener("click", function () {
            if (completeTaskModal) completeTaskModal.classList.add("hidden");
            window.location.href = "/Messages";
        });
    }

    if (closeCompletionModal) {
        closeCompletionModal.addEventListener("click", function () {
            if (completeTaskModal) completeTaskModal.classList.add("hidden");
        });
    }

    if (completeTaskModal) {
        completeTaskModal.addEventListener("click", function (event) {
            if (event.target === completeTaskModal) completeTaskModal.classList.add("hidden");
        });
    }

    if (reportProviderButton) {
        reportProviderButton.addEventListener("click", function () {
            const params = new URLSearchParams({ provider: activeProviderName, task: currentTask.title || "" });
            window.location.href = "/Reports?" + params.toString();
        });
    }

    if (messageProviderButton) {
        // The normal href works without JavaScript; this adds the active task id when available.
        if (currentTask.id) messageProviderButton.href = "/Messages?task=" + encodeURIComponent(currentTask.id);
    }


    // Provider progress action: start work, then mark it finished for client confirmation.
    if (demoFinishTaskButton) {
        demoFinishTaskButton.addEventListener("click", function () {
            taskStatus = taskStatus === "on-the-way" ? "in-progress" : "completed";

            localStorage.setItem(
                "tapTaskDoneTrackingStatus",
                taskStatus
            );
            const isCompleted = taskStatus === "completed";
            syncTaskStatus(
                isCompleted ? "completed" : "assigned",
                isCompleted
                    ? "Your provider marked " + (currentTask.title || "the task") + " as finished. Please review it."
                    : "Work has started for " + (currentTask.title || "your task") + ".",
                "/Tracking"
            );

            updateTracking();
        });
    }


    // =====================================================
    // RATING FORM
    // =====================================================

    if (reviewForm && submitReviewButton) {
        let selectedRating = 0;

        const starButtons =
            reviewForm.querySelectorAll(".star-picker button");

        const reviewTags =
            reviewForm.querySelectorAll(".review-tags button");

        starButtons.forEach(function (starButton) {
            starButton.addEventListener("click", function () {
                selectedRating = Number(starButton.dataset.rating);

                starButtons.forEach(function (item) {
                    const selected = Number(item.dataset.rating) <= selectedRating;
                    item.classList.toggle("selected", selected);
                    item.textContent = selected ? "★" : "☆";
                });

                submitReviewButton.disabled = false;
            });
        });

        reviewTags.forEach(function (tag) {
            tag.addEventListener("click", function () {
                tag.classList.toggle("selected");
            });
        });

        reviewForm.addEventListener("submit", function (event) {
            event.preventDefault();

            if (!selectedRating) {
                return;
            }

            const selectedTags = Array.from(reviewTags)
                .filter(function (tag) { return tag.classList.contains("selected"); })
                .map(function (tag) { return tag.textContent.trim(); });

            const review = {
                provider: currentTask.providerName || "Ofrac",
                service: currentTask.providerService || "Service provider",
                task: currentTask.title || "Task",
                rating: selectedRating,
                tags: selectedTags,
                feedback: document.getElementById("reviewFeedback").value.trim(),
                createdAt: new Date().toLocaleDateString("en-US", {
                    month: "short",
                    day: "numeric",
                    year: "numeric"
                })
            };

            let reviewHistory = [];

            try {
                reviewHistory = JSON.parse(
                    localStorage.getItem("tapTaskDoneReviews") || "[]"
                );
            } catch (error) {
                reviewHistory = [];
            }

            reviewHistory.push(review);
            localStorage.setItem("tapTaskDoneReviews", JSON.stringify(reviewHistory));
            localStorage.setItem("tapTaskDoneReview", JSON.stringify(review));

            reviewNotice.classList.remove("is-open");
            reviewNotice.style.display = "none";
            try {
                const tasks = JSON.parse(localStorage.getItem("tapTaskDoneTasks") || "[]");
                if (Array.isArray(tasks) && currentTask.id) {
                    localStorage.setItem("tapTaskDoneTasks", JSON.stringify(tasks.map(function (task) {
                        return task.id === currentTask.id
                            ? Object.assign({}, task, { status: "reviewed", reviewedAt: "Just now" })
                            : task;
                    })));
                }
            } catch (error) { }
            syncTaskStatus("reviewed", "Your review for " + (currentTask.title || "the task") + " was submitted.", "/Reviews");
            localStorage.removeItem("tapTaskDoneCurrentTask");
            localStorage.removeItem("tapTaskDoneTrackingStatus");
            localStorage.removeItem("tapTaskDonePendingRating");
            sessionStorage.removeItem("tapTaskDoneShowRating");
            window.location.href = "/Reviews";
        });
    }


    // =====================================================
    // TASK TABS
    // =====================================================

    const taskTabs =
        document.querySelectorAll(".task-tab");

    taskTabs.forEach(function (tab) {

        tab.addEventListener("click", function () {

            taskTabs.forEach(function (item) {
                item.classList.remove("selected");
            });

            tab.classList.add("selected");

        });

    });


    // =====================================================
    // DEMO STATUS CONTROL
    // =====================================================
    // Temporary controls for testing the prototype.
    //
    // Browser console:
    //
    // localStorage.setItem(
    //     "tapTaskDoneTrackingStatus",
    //     "in-progress"
    // );
    //
    // OR
    //
    // localStorage.setItem(
    //     "tapTaskDoneTrackingStatus",
    //     "completed"
    // );
    //
    // Then refresh the page.
    // =====================================================


    // =====================================================
    // LOAD
    // =====================================================

    updateTracking();

});
~~~~

### database/mysql_task_progress.sql

~~~~sql
-- Select the intended schema before running. Existing task statuses are preserved.
CREATE TABLE IF NOT EXISTS task_progress_events (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  TaskId BIGINT NOT NULL,
  ActorId INT NOT NULL,
  Status VARCHAR(24) NOT NULL,
  Note VARCHAR(1000) NULL,
  Photo MEDIUMBLOB NULL,
  PhotoType VARCHAR(32) NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  INDEX IX_progress_task (TaskId, Id),
  CONSTRAINT FK_progress_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
  CONSTRAINT FK_progress_actor FOREIGN KEY (ActorId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
~~~~

## Reviews

### Pages/Reviews.cshtml.cs

~~~~csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class ReviewsModel(ApplicationDbContext db) : PageModel
{
    public record ReviewItem(MarketplaceReview Review, string TaskTitle, string OtherName);
    public List<ReviewItem> Items { get; private set; } = [];
    public string View { get; private set; } = "received";
    public bool Legacy { get; private set; }
    public bool Unavailable { get; private set; }
    [TempData] public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(string? view, int? legacy, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == 0 ||
            !await db.Users.AnyAsync(u => u.Id == userId, ct)) return Challenge();
        Legacy = legacy == 1;
        View = view == "given" ? "given" : "received";
        if (Legacy) return Page();
        try
        {
            var query = from r in db.MarketplaceReviews.AsNoTracking()
                        join task in db.MarketplaceTasks.AsNoTracking() on r.TaskId equals task.Id
                        join person in db.Users.AsNoTracking() on (View == "given" ? r.FreelancerId : r.ClientId) equals person.Id
                        where View == "given" ? r.ClientId == userId : r.FreelancerId == userId
                        orderby r.CreatedAt descending, r.Id descending
                        select new { Review = r, task.Title, person.FirstName, person.LastName };
            var rows = await query.ToListAsync(ct);
            Items = rows.Select(x => new ReviewItem(x.Review, x.Title, ($"{x.FirstName} {x.LastName}").Trim())).ToList();
        }
        catch (MySqlException) { Unavailable = true; }
        return Page();
    }
}
~~~~

### Data/MarketplaceReview.cs

~~~~csharp
namespace tts.Data;

public sealed class MarketplaceReview
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int ClientId { get; set; }
    public int FreelancerId { get; set; }
    public int Rating { get; set; }
    public string Feedback { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
~~~~

### database/mysql_reviews.sql

~~~~sql
-- Run after mysql_tasks_offers.sql on the intended MySQL database.
-- Creates a new table only; no existing task or user data is changed.
CREATE TABLE IF NOT EXISTS marketplace_reviews (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    ClientId INT NOT NULL,
    FreelancerId INT NOT NULL,
    Rating INT NOT NULL,
    Feedback VARCHAR(1000) NOT NULL DEFAULT '',
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_marketplace_review_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_marketplace_review_client FOREIGN KEY (ClientId) REFERENCES tbl_user(Id),
    CONSTRAINT fk_marketplace_review_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id),
    CONSTRAINT ck_marketplace_review_rating CHECK (Rating BETWEEN 1 AND 5),
    UNIQUE KEY uq_marketplace_review_task (TaskId),
    INDEX ix_marketplace_review_freelancer (FreelancerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
~~~~
