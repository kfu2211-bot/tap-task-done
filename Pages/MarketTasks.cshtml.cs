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
    public bool ReviewsUnavailable { get; private set; }
    public string WorkRole { get; private set; } = "client";
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public int CurrentUserId => UserId;
    public static bool CanEditTask(string status, int ownerId, int currentUserId) => currentUserId > 0 && ownerId == currentUserId && status == "pending-confirmation";
    public static bool ValidTaskDetails(string title, string description, string category, string location, decimal budget, string schedule) =>
        title.Length is >= 1 and <= 120 && description.Length is >= 1 and <= 5000 && category.Length is >= 1 and <= 60 &&
        location.Length is >= 1 and <= 120 && schedule.Length <= 120 && budget is > 0 and <= 9999999999m;

    public async Task<IActionResult> OnPostEditAsync(long id, string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim();
        location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (!ModelState.IsValid || !ValidTaskDetails(title, description, category, location, budget, schedule))
        { Notice = "Check the task details. Title, description, category, location and a valid budget are required."; return RedirectToPage(new { view = "mine" }); }
        // The condition also protects against a concurrent confirmation or cancellation.
        var updated = await db.MarketplaceTasks.Where(t => t.Id == id && t.ClientId == UserId && t.Status == "pending-confirmation")
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.Title, title).SetProperty(t => t.Description, description)
                .SetProperty(t => t.Category, category).SetProperty(t => t.Location, location).SetProperty(t => t.Budget, budget)
                .SetProperty(t => t.ScheduleText, schedule), ct);
        Notice = updated == 1 ? "Task updated. Review the details, then confirm to publish." : "This task cannot be edited. It may already be confirmed or cancelled.";
        return RedirectToPage(new { view = "mine" });
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId == 0 || !await db.Users.AnyAsync(x => x.Id == UserId, ct)) return Challenge();
        WorkRole = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct))?.WorkRole ?? "client";
        MyTasks = await (from t in db.MarketplaceTasks.AsNoTracking()
                    join u in db.Users.AsNoTracking() on t.ClientId equals u.Id
                    where t.ClientId == UserId orderby t.Id descending
                    select new TaskItem(t, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(ct);
        if (WorkRole is "freelancer" or "both")
            OpenTasks = await (from t in db.MarketplaceTasks.AsNoTracking()
                join u in db.Users.AsNoTracking() on t.ClientId equals u.Id
                where t.ClientId != UserId && t.Status == "open" orderby t.Id descending
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
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(long id, int rating, string? feedback, CancellationToken ct)
    {
        feedback = (feedback ?? "").Trim();
        if (UserId == 0 || !ModelState.IsValid || rating is < 1 or > 5 || feedback.Length > 1000)
        { Notice = "Choose a rating from 1 to 5 and check your feedback."; return RedirectToPage(); }
        try
        {
            var freelancerId = await (from task in db.MarketplaceTasks.AsNoTracking()
                join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                where task.Id == id && task.ClientId == UserId && task.Status == "completed" && offer.Status == "accepted"
                select (int?)offer.FreelancerId).SingleOrDefaultAsync(ct);
            if (freelancerId is null || freelancerId == UserId || await db.MarketplaceReviews.AnyAsync(r => r.TaskId == id, ct))
            { Notice = "This task cannot be reviewed, or already has a review."; return RedirectToPage(); }
            db.MarketplaceReviews.Add(new MarketplaceReview { TaskId = id, ClientId = UserId, FreelancerId = freelancerId.Value, Rating = rating, Feedback = feedback, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); Notice = "Review saved. Thank you for your feedback.";
            return RedirectToPage("/Reviews", new { view = "given" });
        }
        catch (DbUpdateException) { Notice = "Review could not be saved. It may already have been submitted."; }
        catch (MySqlException) { Notice = "Reviews are unavailable. Check that the review table has been created."; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync(string? title, string? description, string? category, string? location, decimal budget, string? schedule, CancellationToken ct)
    {
        title = (title ?? "").Trim(); description = (description ?? "").Trim(); category = (category ?? "").Trim(); location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        var role = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct))?.WorkRole ?? "client";
        if (UserId == 0 || role is not ("client" or "both") || !ModelState.IsValid || title.Length is < 1 or > 120 || description.Length is < 1 or > 5000 || category.Length is < 1 or > 60 || location.Length is < 1 or > 120 || schedule.Length > 120 || budget <= 0 || budget > 9999999999m)
        { Notice = "Please check the task details and your client role."; return RedirectToPage(); }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot create new tasks while restricted. Check your notifications."; return RedirectToPage(); }
        db.MarketplaceTasks.Add(new MarketplaceTask { ClientId = UserId, Title = title, Description = description, Category = category, Location = location, Budget = budget, ScheduleText = schedule, Status = "pending-confirmation", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = "Task saved. Confirm it below to make it visible to freelancers."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostConfirmAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot confirm new tasks while restricted. Check your notifications."; return RedirectToPage(); }
        var count = await db.MarketplaceTasks.Where(t => t.Id == id && t.ClientId == UserId && t.Status == "pending-confirmation").ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "open"), ct);
        await tx.CommitAsync(ct);
        Notice = count == 1 ? "Task is now open for offers." : "Task could not be confirmed."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostCancelAsync(long id, string? reason, CancellationToken ct)
    {
        reason = (reason ?? "").Trim();
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid || reason.Length is < 1 or > 500)
        { Notice = "Please provide a cancellation reason (up to 500 characters)."; return RedirectToPage(new { view = "mine" }); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.ClientId == UserId &&
            (t.Status == "pending-confirmation" || t.Status == "booking-pending" || t.Status == "open" || t.Status == "assigned" || t.Status == "on-the-way" || t.Status == "arrived" || t.Status == "in-progress" || t.Status == "awaiting-confirmation"), ct);
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
        message = (message ?? "").Trim();
        var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct);
        if (profile is null || profile.WorkRole is not ("freelancer" or "both") || profile.Availability != "Available" || !ModelState.IsValid || price <= 0 || price > 9999999999m || message.Length > 500)
        { Notice = "Check your freelancer role, availability and offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId)) { Notice = "This account cannot send offers while restricted. Check your notifications."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.ClientId != UserId && t.Status == "open", ct);
        if (task is null || await db.MarketplaceOffers.AnyAsync(o => o.TaskId == id && o.FreelancerId == UserId, ct)) { Notice = "This task is unavailable or you already sent an offer."; return RedirectToPage("/FindPost", new { mode = "jobs" }); }
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
        var task = offer is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == offer.TaskId && t.ClientId == UserId && t.Status == "open", ct);
        if (task is null || offer is null) { Notice = "This offer is no longer available."; return RedirectToPage(); }
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId, offer.FreelancerId)) { Notice = "This offer cannot be accepted while either account is restricted."; return RedirectToPage(); }
        task.Status = "assigned"; offer.Status = "accepted";
        db.Set<TaskProgressEvent>().Add(new TaskProgressEvent { TaskId = task.Id, ActorId = UserId, Status = "assigned", CreatedAtUtc = DateTime.UtcNow });
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(offer.FreelancerId, task.Id, "accepted", "The client accepted your offer."));
        await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.Id != offer.Id && o.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "declined"), ct);
        await db.MarketplaceBookings.Where(b => b.TaskId == task.Id && b.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "unavailable"), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); Notice = "Offer accepted. The task is assigned."; return RedirectToPage();
    }
}
