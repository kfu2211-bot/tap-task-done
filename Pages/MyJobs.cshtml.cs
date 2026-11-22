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
    public bool WorkLocked { get; private set; }
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId == 0) return Challenge();
        if (Request.Query["legacy"] == "1") return Page();
        try
        {
            WorkLocked = await JobAcceptanceRules.IsLockedAsync(db, UserId, ct);
            Jobs = await (from offer in db.MarketplaceOffers.AsNoTracking()
                          join task in db.MarketplaceTasks.AsNoTracking() on offer.TaskId equals task.Id
                          join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                          where offer.FreelancerId == UserId
                          orderby offer.Id descending
                          select new JobItem(task, offer, ((client.FirstName ?? "") + " " + (client.LastName ?? "")).Trim())).ToListAsync(ct);
            Bookings = await (from booking in db.MarketplaceBookings.AsNoTracking()
                              join task in db.MarketplaceTasks.AsNoTracking() on booking.TaskId equals task.Id
                              join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                              where booking.FreelancerId == UserId
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

    public async Task<IActionResult> OnPostPendingOfferAsync(long id, string? action, decimal price, string? message, string? revision, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        IActionResult Back(string text) { Notice = text; return RedirectToPage(new { view = "offers" }); }
        message = (message ?? "").Trim();
        if (!ModelState.IsValid || action is not ("edit" or "withdraw") || (action == "edit" && (price <= 0 || price > 9999999999m || message.Length > 500)))
            return Back("Check your offer amount and message.");
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id, ct);
        var offer = await db.MarketplaceOffers.SingleOrDefaultAsync(o => o.TaskId == id && o.FreelancerId == UserId, ct);
        if (task is null || offer is null || task.Status != "open" || task.IsPrivate || task.PostExpiresUtc <= DateTime.UtcNow || offer.Status != "pending")
            return Back("This offer is no longer pending. Review the latest job status.");
        if (revision != OfferRevision.For(offer)) return Back("This offer changed. Review it before trying again.");
        if (action == "withdraw") { offer.Status = "withdrawn"; offer.WithdrawalReason = "Withdrawn before acceptance."; }
        else { offer.Price = price; offer.Message = message; }
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "offer", action == "withdraw" ? "The freelancer withdrew a pending offer." : "The freelancer updated an offer. Review the latest price before accepting."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Back(action == "withdraw" ? "Offer withdrawn. You cannot reapply to this task." : "Offer updated. Waiting for the client response.");
    }

    public async Task<IActionResult> OnPostWithdrawAsync(long id, string? reason, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        reason = (reason ?? "").Trim();
        if (!ModelState.IsValid || reason.Length is < 1 or > 500)
        { Notice = "Please provide a withdrawal reason (up to 500 characters)."; return RedirectToPage(new { view = "active" }); }

        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
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
        return RedirectToPage(new { view = "closed", closedFilter = "withdrawn" });
    }
}
