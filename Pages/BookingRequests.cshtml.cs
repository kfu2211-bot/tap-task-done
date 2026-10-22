using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class BookingRequestsModel(ApplicationDbContext db, ILogger<BookingRequestsModel> logger) : PageModel
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [TempData] public string? Notice { get; set; }

    public IActionResult OnGet() => RedirectToPage("/FindPost");

    public async Task<IActionResult> OnPostCreateAsync(long taskId, int freelancerId, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var role = (await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == UserId, ct))?.WorkRole ?? "client";
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == taskId && t.ClientId == UserId && t.Status == "open", ct);
        var provider = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == freelancerId && p.UserId != UserId && (p.WorkRole == "freelancer" || p.WorkRole == "both") && p.Availability == "Available" && p.Service != "" && p.Location != "", ct);
        if (role is not ("client" or "both") || task is null || provider is null || await db.MarketplaceBookings.AnyAsync(b => b.TaskId == taskId && b.FreelancerId == freelancerId, ct))
        { Notice = "Request could not be sent. Check that your task is open, the freelancer is available, and you have not sent this request already."; return RedirectToPage("/FindPost"); }
        db.MarketplaceBookings.Add(new MarketplaceBooking { TaskId = taskId, FreelancerId = freelancerId, Status = "pending", CreatedAt = DateTime.UtcNow });
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(freelancerId, taskId, "booking_request", "A client sent you a booking request."));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); Notice = "Booking request sent. The freelancer can accept or decline it."; }
        catch (DbUpdateException ex) { logger.LogWarning(ex, "Unable to save booking request"); Notice = "Request could not be saved. Please try again."; }
        return RedirectToPage("/FindPost");
    }

    public async Task<IActionResult> OnPostDecideAsync(long id, string? decision, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (decision is not ("accept" or "decline")) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var booking = await db.MarketplaceBookings.SingleOrDefaultAsync(b => b.Id == id && b.FreelancerId == UserId && b.Status == "pending", ct);
        var task = booking is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == booking.TaskId && t.Status == "open", ct);
        if (booking is null || task is null) { Notice = "This request is no longer available."; return RedirectToPage("/MyJobs", new { view = "requests" }); }
        if (decision == "decline")
        {
            booking.Status = "declined";
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "booking_declined", "The freelancer declined your booking request."));
        }
        else
        {
            booking.Status = "accepted"; task.Status = "assigned";
            var offer = await db.MarketplaceOffers.SingleOrDefaultAsync(o => o.TaskId == task.Id && o.FreelancerId == UserId, ct);
            if (offer == null) db.MarketplaceOffers.Add(new MarketplaceOffer { TaskId = task.Id, FreelancerId = UserId, Price = task.Budget, Message = "Direct booking at the posted task budget.", Status = "accepted", CreatedAt = DateTime.UtcNow });
            else { offer.Price = task.Budget; offer.Message = "Direct booking at the posted task budget."; offer.Status = "accepted"; }
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
