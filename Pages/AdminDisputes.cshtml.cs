using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public class AdminDisputesModel(ApplicationDbContext db) : PageModel
{
    public record DisputeItem(MarketplaceDispute Dispute, string TaskTitle);
    public List<DisputeItem> Items { get; private set; } = [];
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        Items = await (from d in db.MarketplaceDisputes.AsNoTracking() join t in db.MarketplaceTasks.AsNoTracking() on d.TaskId equals t.Id
            orderby d.Id descending select new DisputeItem(d, t.Title)).Take(100).ToListAsync(ct);
        return Page();
    }
    public async Task<IActionResult> OnPostCloseAsync(long id, string? resolution, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        resolution = (resolution ?? "").Trim();
        if (!ModelState.IsValid || resolution.Length is < 1 or > 1000)
        { Notice = "Enter a resolution (up to 1,000 characters)."; return RedirectToPage(); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var dispute = await db.MarketplaceDisputes.SingleOrDefaultAsync(d => d.Id == id && d.Status == "open", ct);
        var task = dispute is null ? null : await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == dispute.TaskId && t.Status == "disputed", ct);
        if (dispute is null || task is null || dispute.PreviousTaskStatus is not ("assigned" or "in-progress" or "awaiting-confirmation"))
        { Notice = "This dispute is unavailable or already closed."; return RedirectToPage(); }
        var freelancerId = await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.Status == "accepted").Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
        if (freelancerId is null) { Notice = "The assigned freelancer could not be found."; return RedirectToPage(); }
        dispute.Status = "closed"; dispute.Resolution = resolution; dispute.ClosedById = UserId; dispute.ClosedAt = DateTime.UtcNow;
        task.Status = dispute.PreviousTaskStatus;
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, task.Id, "dispute", "The admin closed the dispute. Open the task dispute for the resolution."));
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(freelancerId.Value, task.Id, "dispute", "The admin closed the dispute. Open the task dispute for the resolution."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "Dispute closed. Both participants can see the resolution and the task returns to its previous status.";
        return RedirectToPage();
    }
}
