using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class DisputesModel(ApplicationDbContext db) : PageModel
{
    public MarketplaceTask? TaskItem { get; private set; }
    public List<MarketplaceDispute> Items { get; private set; } = [];
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private IQueryable<MarketplaceTask> ParticipantTasks() => db.MarketplaceTasks.Where(t =>
        db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.Status == "accepted" && (t.ClientId == UserId || o.FreelancerId == UserId)));

    public async Task<IActionResult> OnGetAsync(long taskId, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        TaskItem = await ParticipantTasks().AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId, ct);
        if (TaskItem is null) return NotFound();
        Items = await db.MarketplaceDisputes.AsNoTracking().Where(d => d.TaskId == taskId).OrderByDescending(d => d.Id).ToListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long taskId, string? reason, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        reason = (reason ?? "").Trim();
        if (!ModelState.IsValid || reason.Length is < 1 or > 1000)
        { Notice = "Please provide a reason (up to 1,000 characters)."; return RedirectToPage(new { taskId }); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var task = await ParticipantTasks().SingleOrDefaultAsync(t => t.Id == taskId &&
            (t.Status == "assigned" || t.Status == "in-progress" || t.Status == "awaiting-confirmation"), ct);
        if (task is null) { Notice = "This task is not eligible or already has an open dispute."; return RedirectToPage(new { taskId }); }
        var freelancerId = await db.MarketplaceOffers.Where(o => o.TaskId == taskId && o.Status == "accepted").Select(o => o.FreelancerId).SingleAsync(ct);
        var previous = task.Status;
        task.Status = "disputed";
        db.MarketplaceDisputes.Add(new MarketplaceDispute { TaskId = taskId, OpenedById = UserId, Reason = reason, PreviousTaskStatus = previous, Status = "open", CreatedAt = DateTime.UtcNow });
        var otherId = UserId == task.ClientId ? freelancerId : task.ClientId;
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(otherId, taskId, "dispute", "A dispute was opened for your task. Task progress is paused."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "Dispute submitted. The task is paused while an admin reviews it.";
        return RedirectToPage(new { taskId });
    }
}
