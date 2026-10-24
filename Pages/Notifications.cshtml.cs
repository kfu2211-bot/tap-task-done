using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class NotificationsModel(ApplicationDbContext db) : PageModel
{
    public List<MarketplaceNotification> Items { get; private set; } = [];
    public List<AccountModerationAction> AccountUpdates { get; private set; } = [];
    public bool AccountUpdatesUnavailable { get; private set; }
    public Dictionary<long, string> TaskTitles { get; private set; } = [];
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        Items = await db.MarketplaceNotifications.AsNoTracking().Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.Id).Take(100).ToListAsync(ct);
        var taskIds = Items.Select(n => n.TaskId).Distinct().ToList();
        TaskTitles = await db.MarketplaceTasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        {
            try { AccountUpdates = await db.AccountModerationActions.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct); }
            catch (MySqlConnector.MySqlException) { AccountUpdatesUnavailable = true; }
        }
        return Page();
    }

    public async Task<IActionResult> OnPostReadAccountAsync(long id, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        var item = await db.AccountModerationActions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (item is null) return NotFound();
        item.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostOpenAsync(long id, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        var item = await db.MarketplaceNotifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == UserId, ct);
        if (item == null) return NotFound();
        item.IsRead = true;
        await db.SaveChangesAsync(ct);
        return item.Kind switch
        {
            "message" => RedirectToPage("/Messages", new { taskId = item.TaskId }),
            "dispute" => RedirectToPage("/Disputes", new { taskId = item.TaskId }),
            "booking_request" => RedirectToPage("/MyJobs", new { view = "requests" }),
            "accepted" => RedirectToPage("/MyJobs", new { view = "active" }),
            "completed" => RedirectToPage("/MyJobs", new { view = "completed" }),
            "cancelled" => RedirectToPage("/MyJobs", new { view = "cancelled" }),
            _ => RedirectToPage("/MarketTasks", new { view = "mine" })
        };
    }
}
