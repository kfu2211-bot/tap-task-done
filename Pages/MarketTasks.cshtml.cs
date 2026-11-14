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

    public async Task<IActionResult> OnPostEditAsync(long id, string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude = null, double? longitude = null, decimal? budgetMaximum = null, int? postDurationDays = null, DateOnly? postEndDate = null)
    {
        if (UserId <= 0) return Challenge();
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
                    where t.ClientId == UserId orderby t.Id descending
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

    public async Task<IActionResult> OnPostCreateAsync(string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct, double? latitude = null, double? longitude = null, decimal? budgetMaximum = null, int? postDurationDays = null, DateOnly? postEndDate = null)
    {
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim(); location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (UserId <= 0) return Challenge();
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
        var task = new MarketplaceTask { ClientId = UserId, Title = title, Description = description, Category = category, Location = location, Budget = budget, BudgetMaximum = budgetMaximum, PostDurationDays = postDurationDays ?? 7, PostExpiresUtc = ClosingTimeUtc(postEndDate), ScheduleText = schedule, Status = "pending-confirmation", CreatedAt = DateTime.UtcNow };
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
