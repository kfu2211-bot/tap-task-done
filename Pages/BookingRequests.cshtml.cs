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
    public static bool CanBook(string role) => role is "client" or "freelancer" or "both";
    public static string WorkRoleAfterBooking(string role) => role == "freelancer" ? "both" : role;
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [TempData] public string? Notice { get; set; }

    public IActionResult OnGet() => RedirectToPage("/FindPost");

    public async Task<IActionResult> OnPostDirectAsync(int freelancerId, string? title, string? description,
        string? location, string? schedule, decimal budget, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        title = (title ?? "").Trim(); description = (description ?? "").Trim();
        location = (location ?? "").Trim(); schedule = (schedule ?? "").Trim();
        if (!ModelState.IsValid || freelancerId <= 0 || freelancerId == UserId || title.Length is < 1 or > 120 ||
            description.Length is < 1 or > 5000 || location.Length is < 1 or > 120 || schedule.Length is < 1 or > 120 ||
            budget is < 1 or > 9999999999m || decimal.Round(budget, 2) != budget)
        { Notice = "Please complete the work details, location, schedule, and a valid budget."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        if (!DateTime.TryParseExact(schedule, "yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var preferredTime) ||
            new DateTimeOffset(DateTime.SpecifyKind(preferredTime, DateTimeKind.Unspecified), TimeSpan.FromHours(8)).UtcDateTime <= DateTime.UtcNow)
        { Notice = "Choose a valid future date and time for your booking (Philippine time)."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        schedule = preferredTime.ToString("MMM d, yyyy 'at' h:mm tt '(PHT)'", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId, freelancerId))
            { Notice = "This request cannot be sent while either account is restricted."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
            var requesterProfile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, ct);
            var role = requesterProfile?.WorkRole ?? "client";
            var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId &&
                (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
            if (!CanBook(role) || provider is null || await FreelancerAvailability.IsBusyAsync(db, freelancerId, ct))
            { Notice = "Choose an available freelancer to send a booking."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
            var duplicate = await db.MarketplaceBookings.AnyAsync(b => b.FreelancerId == freelancerId && b.Status == "pending" &&
                db.MarketplaceTasks.Any(t => t.Id == b.TaskId && t.ClientId == UserId && t.Status == "booking-pending" &&
                    t.Title == title && t.Description == description && t.Location == location && t.ScheduleText == schedule && t.Budget == budget), ct);
            if (duplicate)
            { Notice = "This booking request has already been sent. Check My Tasks for its status."; return RedirectToPage("/MarketTasks", new { view = "mine" }); }
            if (requesterProfile is not null) requesterProfile.WorkRole = WorkRoleAfterBooking(role);
            // This internal task is private: public directories only select status "open".
            var work = new MarketplaceTask { ClientId = UserId, Title = title, Description = description, Location = location,
                ScheduleText = schedule, Budget = budget, Category = provider.Service, Status = "booking-pending", CreatedAt = DateTime.UtcNow };
            db.MarketplaceTasks.Add(work);
            await db.SaveChangesAsync(ct);
            db.MarketplaceBookings.Add(new MarketplaceBooking { TaskId = work.Id, FreelancerId = freelancerId, Status = "pending", CreatedAt = DateTime.UtcNow });
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(freelancerId, work.Id, "booking_request", "A client sent you a private work request. Review it in My Jobs → Booking requests."));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            Notice = "Booking request sent directly to the freelancer. You can follow its status in My Tasks.";
            return RedirectToPage("/MarketTasks", new { view = "mine" });
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning(ex, "Unable to create direct booking"); Notice = "Booking could not be saved. Please try again."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
    }

    public async Task<IActionResult> OnPostCreateAsync(long taskId, int freelancerId, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId, freelancerId)) { Notice = "This request cannot be sent while either account is restricted."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        var requesterProfile = await db.FreelancerProfiles.SingleOrDefaultAsync(p => p.UserId == UserId, ct);
            var role = requesterProfile?.WorkRole ?? "client";
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == taskId && t.ClientId == UserId && t.Status == "open", ct);
        var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId && p.UserId != UserId && (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
        if (!CanBook(role) || task is null || provider is null || await FreelancerAvailability.IsBusyAsync(db, freelancerId, ct) || await db.MarketplaceBookings.AnyAsync(b => b.TaskId == taskId && b.FreelancerId == freelancerId, ct))
        { Notice = "Request could not be sent. Check that your task is open, the freelancer is available, and you have not sent this request already."; return RedirectToPage("/FindPost", new { providerId = freelancerId }); }
        if (requesterProfile is not null) requesterProfile.WorkRole = WorkRoleAfterBooking(role);
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
        var task = booking is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == booking.TaskId && (t.Status == "open" || t.Status == "booking-pending"), ct);
        if (booking is null || task is null) { Notice = "This request is no longer available."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
        if (decision == "decline")
        {
            booking.Status = "declined";
            if (task.Status == "booking-pending") { task.Status = "cancelled"; task.CancellationReason = "The freelancer declined this direct booking request."; }
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "booking_declined", "The freelancer declined your booking request."));
        }
        else
        {
            if (!await ModerationGuard.AllowedAsync(db, environment, ct, UserId, task.ClientId)) { Notice = "This request cannot be accepted while either account is restricted."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
            booking.Status = "accepted"; task.Status = "assigned";
            db.Set<TaskProgressEvent>().Add(new TaskProgressEvent { TaskId = task.Id, ActorId = UserId, Status = "assigned", CreatedAtUtc = DateTime.UtcNow });
            var offer = await db.MarketplaceOffers.SingleOrDefaultAsync(o => o.TaskId == task.Id && o.FreelancerId == UserId, ct);
            if (offer == null) db.MarketplaceOffers.Add(new MarketplaceOffer { TaskId = task.Id, FreelancerId = UserId, Price = task.Budget, Message = "Direct booking at the agreed request budget.", Status = "accepted", CreatedAt = DateTime.UtcNow });
            else { offer.Price = task.Budget; offer.Message = "Direct booking at the agreed request budget."; offer.Status = "accepted"; }
            await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.FreelancerId != UserId && o.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "declined"), ct);
            await db.MarketplaceBookings.Where(b => b.TaskId == task.Id && b.Id != booking.Id && b.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "unavailable"), ct);
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "booking_accepted", "The freelancer accepted your booking request."));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = decision == "accept" ? "Booking accepted. The task is now in your active jobs." : "Booking request declined.";
        return RedirectToPage("/MyJobs", new { view = decision == "accept" ? "active" : "requests" });
    }
}
