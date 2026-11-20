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
            await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
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
        string? location, string? schedule, decimal budget, CancellationToken ct, double? latitude = null, double? longitude = null, long? inquiryId = null, string? serviceArea = null, string? arrivalInstructions = null, int? estimatedMinutes = null, string? jobType = null, DateTime? workEnd = null, long? sourceTaskId = null)
    {
        if (UserId <= 0) return Challenge();
        var reuseSource = await TaskReuse.SourceAsync(db, sourceTaskId, UserId, ct);
        if (sourceTaskId.HasValue && (reuseSource is null || inquiryId.HasValue ||
            !await db.MarketplaceOffers.AnyAsync(o => o.TaskId == sourceTaskId && o.FreelancerId == freelancerId && o.Status == "accepted", ct) &&
            !await db.MarketplaceBookings.AnyAsync(b => b.TaskId == sourceTaskId && b.FreelancerId == freelancerId, ct))) return NotFound();
        if (!await IdentityWorkGate.IsApprovedAsync(db, UserId, ct)) { TempData["IdentityVerification.Notice"] = IdentityWorkGate.Message; return RedirectToPage("/IdentityVerification"); }
        arrivalInstructions = arrivalInstructions?.Trim();
        if (arrivalInstructions?.Length > 500 || !TaskLocationRules.ValidServiceLocation(serviceArea, latitude, longitude))
        { Notice = "Confirm a pin inside Bacolor and keep arrival instructions under 500 characters."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        title = (title ?? "").Trim(); description = (description ?? "").Trim();
        location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (!JobTypes.ValidSchedule(jobType, schedule, JobTypes.EndUtc(workEnd))) { Notice = JobTypes.ScheduleMessage; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        if (!ModelState.IsValid || !JobTypes.ValidSchedule(jobType, schedule, JobTypes.EndUtc(workEnd)) || !JobDuration.Valid(estimatedMinutes) || freelancerId <= 0 || freelancerId == UserId || title.Length is < 1 or > 120 ||
            description.Length is < 1 or > 5000 || location.Length is < 1 or > 120 || schedule.Length is < 1 or > 120 ||
            budget is < 1 or > 9999999999m || decimal.Round(budget, 2) != budget || !TaskLocationRules.Valid(latitude, longitude))
        { Notice = "Complete the work details and confirm the location. Enter a valid proposed budget."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        if (!DateTime.TryParseExact(schedule, "yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var preferredTime) ||
            new DateTimeOffset(DateTime.SpecifyKind(preferredTime, DateTimeKind.Unspecified), TimeSpan.FromHours(8)).UtcDateTime <= DateTime.UtcNow)
        { Notice = "Choose a valid future date and time for your booking (Philippine time)."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
        schedule = preferredTime.ToString("MMM d, yyyy 'at' h:mm tt '(PHT)'", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
            if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, freelancerId))
            { Notice = "This request cannot be sent while either account is restricted or has a pending deletion request."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
            var requesterProfile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, ct);
            var role = requesterProfile?.WorkRole ?? "client";
            var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId &&
                (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
            if (!CanBook(role) || provider is null)
            { Notice = "Choose an available freelancer to send a booking."; return RedirectToPage("/FindPost", new { providerId = freelancerId, inquiryId }); }
            var duplicate = await db.MarketplaceBookings.AnyAsync(b => b.FreelancerId == freelancerId && b.Status == "pending" &&
                db.MarketplaceTasks.Any(t => t.Id == b.TaskId && t.ClientId == UserId && t.Status == "booking-pending" &&
                    t.JobType == jobType && t.WorkEndUtc == JobTypes.EndUtc(workEnd) && t.Title == title && t.Description == description && t.Location == location && t.ScheduleText == schedule && t.Budget == budget && t.BudgetMaximum == null), ct);
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
            work.JobType = jobType; work.WorkEndUtc = JobTypes.EndUtc(workEnd);
            work.ScheduleText = schedule;
            work.Budget = budget;
            work.BudgetMaximum = null;
            work.Category = provider.Service;
            work.Status = "booking-pending";
            await db.SaveChangesAsync(ct);
            TaskLocationRules.Save(db, work.Id, null, latitude!.Value, longitude!.Value);
            if (reuseSource is not null) db.Add(TaskReuse.SourceEvent(work.Id, reuseSource.Id, UserId));
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
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        if (!await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, freelancerId)) { Notice = "This request cannot be sent while either account is restricted or has a pending deletion request."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        var requesterProfile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, ct);
            var role = requesterProfile?.WorkRole ?? "client";
        await AppTransactions.LockTaskAsync(db, taskId, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == taskId && t.ClientId == UserId && t.Status == "open" && !t.IsPrivate && (t.PostExpiresUtc == null || t.PostExpiresUtc > DateTime.UtcNow), ct);
        var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId && p.UserId != UserId && (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
        if (!CanBook(role) || task is null || provider is null || await db.MarketplaceBookings.AnyAsync(b => b.TaskId == taskId && b.FreelancerId == freelancerId, ct))
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
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        var hint = await db.MarketplaceBookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.FreelancerId == UserId && b.Status == "pending", ct);
        if (hint is null) { Notice = "This request is no longer available."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
        var owner = await db.MarketplaceTasks.Where(t => t.Id == hint.TaskId).Select(t => t.ClientId).SingleOrDefaultAsync(ct);
        if (decision == "accept" && !await ModerationGuard.NewWorkAllowedAsync(db, environment, ct, UserId, owner)) { Notice = "This request cannot be accepted while an account is restricted."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
        await AppTransactions.LockTaskAsync(db, hint.TaskId, ct);
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
            if (await JobAcceptanceRules.IsLockedAsync(db, UserId, ct, task.Id))
            { Notice = JobAcceptanceRules.Message; return RedirectToPage("/MyJobs", new { view = "requests" }); }
            if (await JobTypes.ConflictsAsync(db, UserId, task.Id, task.ScheduleText, task.WorkEndUtc, ct))
            { Notice = "This booking overlaps another accepted job. Discuss a different schedule with the client."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
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
        return RedirectToPage("/MyJobs", new { view = decision == "accept" ? (TaskSchedule.IsUpcoming(task, DateTime.UtcNow) ? "scheduled" : "active") : "requests" });
    }
}
