using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tts.Data;

namespace tts.Pages;

[Authorize]
public sealed class MarketTasksModel(ApplicationDbContext db, IWebHostEnvironment environment, ILogger<MarketTasksModel>? logger = null) : PageModel
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
    public bool WorkLocked { get; private set; }
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

    public async Task<IActionResult> OnPostEditAsync(long id, string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude = null, double? longitude = null, int? postDurationDays = null, DateOnly? postEndDate = null, string? serviceArea = null, string? arrivalInstructions = null, int? estimatedMinutes = null, string? jobType = null, DateTime? workEnd = null)
    {
        if (UserId <= 0) return Challenge();
        arrivalInstructions = arrivalInstructions?.Trim();
        if (arrivalInstructions?.Length > 500 || !TaskLocationRules.ValidServiceLocation(serviceArea, latitude, longitude))
        { Notice = "Confirm a pin inside Bacolor and keep arrival instructions under 500 characters."; return RedirectToPage(new { view = "mine" }); }
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim();
        location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (ModelState.TryGetValue(nameof(postEndDate), out var dateState) && dateState.Errors.Count > 0 ||
            postEndDate is { } endDate && (endDate < TodayInPhilippines || endDate == DateOnly.MaxValue))
        {
            Notice = "Choose a valid closing date today or later.";
            return RedirectToPage(new { view = "mine" });
        }

        if (!JobTypes.ValidSchedule(jobType, schedule, JobTypes.EndUtc(workEnd))) { Notice = JobTypes.ScheduleMessage; return RedirectToPage(new { view = "mine" }); }
        if (!ModelState.IsValid || !JobTypes.ValidSchedule(jobType, schedule, JobTypes.EndUtc(workEnd)) || !JobDuration.Valid(estimatedMinutes) || (postDurationDays is not (null or 3 or 7)) || (!ValidTaskDetails(title, description, category, location, budget, schedule) || !TaskLocationRules.Valid(latitude, longitude)))
        { Notice = "Check the task details. Enter a valid task budget and confirm the location pin."; return RedirectToPage(new { view = "mine" }); }
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot edit tasks while suspended."; return RedirectToPage(new { view = "mine" }); }
        var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct);
        var task = tasks.SingleOrDefault();
        if (task is null || !CanEditTask(task.Status, task.ClientId, UserId)) { Notice = "This task cannot be edited. It may already be confirmed or cancelled."; return RedirectToPage(new { view = "mine" }); }
        if (!string.Equals(task.Category, category, StringComparison.OrdinalIgnoreCase)) task.SpecificService = null;
        task.JobType = jobType; task.WorkEndUtc = JobTypes.EndUtc(workEnd); task.Title = title; task.Description = description; task.Category = category; task.Location = location; task.Budget = budget; task.BudgetMaximum = null; task.ScheduleText = schedule;
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
        WorkLocked = await JobAcceptanceRules.IsLockedAsync(db, UserId, ct);
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
        try { Reviews = await db.MarketplaceReviews.AsNoTracking().Where(r => r.ClientId == UserId && !r.ByFreelancer).ToDictionaryAsync(r => r.TaskId, ct); }
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
            var assignment = await (from task in db.MarketplaceTasks.AsNoTracking()
                join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                where task.Id == id && task.Status == "completed" && offer.Status == "accepted" &&
                    (task.ClientId == UserId || offer.FreelancerId == UserId)
                select new { task.ClientId, offer.FreelancerId }).SingleOrDefaultAsync(ct);
            if (assignment is null || assignment.ClientId == assignment.FreelancerId)
            { Notice = "This task cannot be reviewed."; return ReviewReturn(); }
            var byFreelancer = assignment.FreelancerId == UserId;
            if (await db.MarketplaceReviews.AnyAsync(r => r.TaskId == id && r.ByFreelancer == byFreelancer, ct))
            { Notice = "You already reviewed this task."; return ReviewReturn(); }
            db.MarketplaceReviews.Add(new MarketplaceReview {
                TaskId = id, ClientId = assignment.ClientId, FreelancerId = assignment.FreelancerId,
                ByFreelancer = byFreelancer, Rating = rating, Feedback = feedback, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct); Notice = "Review saved. Thank you for your feedback.";
            if (fromTracking) return ReviewReturn();
            return RedirectToPage("/Reviews", new { view = "given" });
        }
        catch (DbUpdateException) { Notice = "Review could not be saved. It may already have been submitted."; }
        catch (MySqlException) { Notice = "Reviews are unavailable. Check that the review table has been created."; }
        return ReviewReturn();
    }

    public static bool ValidBudgetMaximum(decimal minimum, decimal? maximum) => maximum is null || maximum >= minimum && maximum <= 9999999999m && decimal.Round(maximum.Value, 2) == maximum;

    private bool JsonRequested => Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    private IActionResult DraftFailure(string message, int status = 422)
    {
        if (JsonRequested) return new JsonResult(new { success = false, message }) { StatusCode = status };
        Notice = message;
        return RedirectToPage("/FindPost", new { mode = "post" });
    }
    private IActionResult DraftSaved(long id, string message)
    {
        Notice = message;
        return JsonRequested ? new JsonResult(new { success = true, redirectUrl = $"/MarketTasks?view=mine&reviewTask={id}#task-{id}" })
            : RedirectToPage("/MarketTasks", null, new { view = "mine", reviewTask = id }, $"task-{id}");
    }

    public async Task<IActionResult> OnPostCreateAsync(string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude = null, double? longitude = null, int? postDurationDays = null, DateOnly? postEndDate = null, string? serviceArea = null, string? arrivalInstructions = null, string? specificService = null, int? estimatedMinutes = null, string? jobType = null, DateTime? workEnd = null)
    {
        try { return await CreateDraftAsync(title, description, category, location, budget, schedule, ct, latitude, longitude, postDurationDays, postEndDate, serviceArea, arrivalInstructions, specificService, estimatedMinutes, jobType, workEnd); }
        catch (Exception ex) when (ex is MySqlException or DbUpdateException or InvalidOperationException)
        {
            logger?.LogError(ex, "Unable to save task draft");
            return DraftFailure("Could not save your task. Your details are still here; please try again.", 503);
        }
    }
    private async Task<IActionResult> CreateDraftAsync(string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude, double? longitude, int? postDurationDays, DateOnly? postEndDate, string? serviceArea, string? arrivalInstructions, string? specificService, int? estimatedMinutes, string? jobType, DateTime? workEnd)
    {
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim(); location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (UserId <= 0) return Challenge();
        arrivalInstructions = arrivalInstructions?.Trim();
        if (arrivalInstructions?.Length > 500 || !TaskLocationRules.ValidServiceLocation(serviceArea, latitude, longitude))
        { return DraftFailure("Confirm a pin inside Bacolor and keep arrival instructions under 500 characters."); }
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { if (JsonRequested) return DraftFailure(IdentityWorkGate.Message); TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        if (ModelState.TryGetValue(nameof(postEndDate), out var dateState) && dateState.Errors.Count > 0 ||
            postEndDate is { } endDate && (endDate < TodayInPhilippines || endDate == DateOnly.MaxValue))
        {
            return DraftFailure("Choose a valid closing date today or later.");
        }

        if (!JobTypes.ValidSchedule(jobType, schedule, JobTypes.EndUtc(workEnd))) return DraftFailure(JobTypes.ScheduleMessage);
        if (!ModelState.IsValid || !JobTypes.ValidSchedule(jobType, schedule, JobTypes.EndUtc(workEnd)) || !JobDuration.Valid(estimatedMinutes) || postDurationDays is not (null or 3 or 7) || !ValidTaskDetails(title, description, category, location, budget, schedule) || !TaskLocationRules.Valid(latitude, longitude))
        { return DraftFailure("Complete the task details, enter a valid task budget, and confirm the destination pin."); }
        if (string.IsNullOrWhiteSpace(specificService)) specificService = null;
        else if (!ServiceCatalog.Options.TryGetValue(category, out var services) ||
            (specificService != ServiceCatalog.CustomOption && !services.Contains(specificService)))
        {
            return DraftFailure("Choose a specific service that belongs to the selected category.");
        }

        var role = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct))?.WorkRole ?? "client";
        if (role is not ("client" or "both")) { return DraftFailure("Choose Client or Client & Freelancer in Settings to post tasks."); }
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId)) { return DraftFailure("This account cannot create new tasks while restricted or pending account deletion."); }
        // Serialize task creation by this client so simultaneous retries see the first draft.
        await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {UserId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var duration = postDurationDays ?? 7;
        var closingTime = ClosingTimeUtc(postEndDate);
        var recent = DateTime.UtcNow.AddMinutes(-2);
        var duplicate = await (from draft in db.MarketplaceTasks.AsNoTracking()
            join pin in db.Set<TaskDestination>().AsNoTracking() on draft.Id equals pin.TaskId
            where draft.ClientId == UserId && !draft.IsPrivate && draft.Status == "pending-confirmation" &&
                draft.CreatedAt >= recent && draft.Title == title && draft.Description == description &&
                draft.JobType == jobType && draft.WorkEndUtc == JobTypes.EndUtc(workEnd) && draft.Category == category && draft.SpecificService == specificService && draft.Location == location && draft.ArrivalInstructions == arrivalInstructions && draft.Budget == budget &&
                draft.BudgetMaximum == null && draft.ScheduleText == schedule &&
                draft.PostDurationDays == duration && draft.PostExpiresUtc == closingTime &&
                pin.Latitude == latitude!.Value && pin.Longitude == longitude!.Value
            select (long?)draft.Id).FirstOrDefaultAsync(ct);
        if (duplicate.HasValue)
        {
            return DraftSaved(duplicate.Value, "This task was already saved. Confirm and open the task to publish it.");
        }
        var task = new MarketplaceTask { ClientId = UserId, Title = title, Description = description, JobType = jobType, WorkEndUtc = JobTypes.EndUtc(workEnd), EstimatedMinutes = estimatedMinutes, Category = category, SpecificService = specificService, Location = location, ArrivalInstructions = arrivalInstructions, Budget = budget, PostDurationDays = duration, PostExpiresUtc = closingTime, ScheduleText = schedule, Status = "pending-confirmation", CreatedAt = DateTime.UtcNow };
        db.MarketplaceTasks.Add(task); await db.SaveChangesAsync(ct);
        TaskLocationRules.Save(db, task.Id, null, latitude!.Value, longitude!.Value);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return DraftSaved(task.Id, "Task saved for review. Select Confirm and open task to publish it for freelancers.");
    }
    public async Task<IActionResult> OnPostConfirmAsync(long id, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot confirm new tasks while restricted or pending account deletion."; return RedirectToPage(); }
        var destination = await db.Set<TaskDestination>().AsNoTracking().SingleOrDefaultAsync(d => d.TaskId == id, ct);
        if (!TaskLocationRules.Valid(destination?.Latitude, destination?.Longitude)) { Notice = "Edit this draft and confirm a work-location pin before publishing."; return RedirectToPage(new { view = "mine" }); }
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
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
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

    public async Task<IActionResult> OnPostOfferAsync(long id, decimal price, string? message, CancellationToken ct, bool applyAtPostedPay = false)
    {
        if (UserId <= 0) return Challenge();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        message = (message ?? "").Trim();
        var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct);
        if (profile is null || profile.WorkRole is not ("freelancer" or "both") || profile.Availability != "Available" || !ModelState.IsValid || price <= 0 || price > 9999999999m || message.Length > 500)
        { Notice = "Check your freelancer role, availability and offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        var owner = await db.MarketplaceTasks.Where(t => t.Id == id).Select(t => t.ClientId).SingleOrDefaultAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, owner)) { Notice = "This account cannot send offers while restricted or pending account deletion."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        if (await JobAcceptanceRules.IsLockedAsync(db, UserId, ct)) { Notice = JobAcceptanceRules.Message; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.ClientId != UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow), ct);
        if (task is null || await db.MarketplaceOffers.AnyAsync(o => o.TaskId == id && o.FreelancerId == UserId, ct)) { Notice = "This task is unavailable or you already sent an offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        if (applyAtPostedPay && ((task.BudgetMaximum.HasValue && task.BudgetMaximum.Value != task.Budget) || price != task.Budget))
        {
            Notice = "The posted pay has changed. Review the latest budget before applying.";
            return RedirectToPage("/FindPost", new { mode = "jobs" });
        }
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, task.ClientId)) { Notice = "This task cannot receive offers while an account is pending deletion or restricted."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        await AgreementRules.ConversationAsync(db, id, UserId, ct);
        db.MarketplaceOffers.Add(new MarketplaceOffer { TaskId = id, FreelancerId = UserId, Price = price, Message = message, Status = "pending", CreatedAt = DateTime.UtcNow });
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, id, "offer", "A freelancer sent an offer for your task."));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); Notice = applyAtPostedPay ? "Application sent to the client at the posted pay." : "Offer sent to the client."; return RedirectToPage("/MyJobs", new { view = "offers" }); }
        catch (DbUpdateException) { Notice = "Your offer could not be saved. Please try again."; }
        return RedirectToPage("/FindPost", new { mode = "jobs" });
    }
    public async Task<IActionResult> OnPostAcceptAsync(long id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        var hint = await db.MarketplaceOffers.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id && o.Status == "pending", ct);
        if (hint is null || !await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, hint.FreelancerId)) { Notice = "This offer is no longer available."; return RedirectToPage(); }
        await AppTransactions.LockTaskAsync(db, hint.TaskId, ct);
        var offer = await db.MarketplaceOffers.SingleOrDefaultAsync(o => o.Id == id && o.Status == "pending", ct);
        var task = offer is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == offer.TaskId && t.ClientId == UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow), ct);
        if (task is null || offer is null) { Notice = "This offer is no longer available."; return RedirectToPage(); }
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, offer.FreelancerId)) { Notice = "This offer cannot be accepted while either account is restricted or has a pending deletion request."; return RedirectToPage(); }
        if (await JobAcceptanceRules.IsLockedAsync(db, offer.FreelancerId, ct, task.Id))
        { Notice = "This freelancer must finish their ongoing Long Job or Multi-Day Job before another offer can be accepted."; return RedirectToPage(); }
        if (await JobTypes.ConflictsAsync(db, offer.FreelancerId, task.Id, task.ScheduleText, task.WorkEndUtc, ct))
        { Notice = "This work schedule overlaps another accepted job for the freelancer. Agree on a different schedule first."; return RedirectToPage(); }
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
