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
        var (from, to) = action switch { "start" => ("assigned", "in-progress"), "finish" => ("in-progress", "awaiting-confirmation"), _ => ("", "") };
        if (from == "") return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.MarketplaceTasks.Where(t => t.Id == id && t.Status == from &&
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == UserId && o.Status == "accepted"))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, to), ct);
        if (updated == 1)
        {
            var clientId = await db.MarketplaceTasks.Where(t => t.Id == id).Select(t => t.ClientId).SingleAsync(ct);
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(clientId, id, action == "start" ? "started" : "finished",
                action == "start" ? "The freelancer started working on your task." : "The freelancer marked the work finished. Please check and confirm completion."));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        Notice = updated == 1 ? (action == "start" ? "Work started." : "Work marked finished. Waiting for client confirmation.") : "The job could not be updated. It may have changed already.";
        if (updated == 1 && action == "start") return RedirectToPage("/Tracking", new { taskId = id, shareLocation = 1 });
        return RedirectToPage(new { view = "active" });
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
        task.Status = "open";
        offer.Status = "withdrawn";
        offer.WithdrawalReason = reason;
        await db.MarketplaceBookings.Where(b => b.TaskId == id && b.FreelancerId == UserId && b.Status == "accepted")
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "withdrawn"), ct);
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "withdrawn",
            "The freelancer withdrew before starting. Your task is open for new offers."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "You withdrew from the job. The client has been notified and the task is open for new offers.";
        return RedirectToPage(new { view = "offers" });
    }
}
