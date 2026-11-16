using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public class AdminPostsModel(ApplicationDbContext db) : PageModel
{
    public static readonly string[] Reasons = ["Fake or misleading task", "Spam or trolling", "Offensive language", "Other policy violation"];
    public List<MarketplaceTask> Posts { get; private set; } = [];
    public MarketplaceTask? Selected { get; private set; }
    public List<SafetyReport> Reports { get; private set; } = [];
    public List<PostModerationAction> History { get; private set; } = [];
    public bool Unavailable { get; private set; }
    [TempData(Key = "AdminPosts.Notice")] public string? Notice { get; set; }

    public async Task OnGetAsync(string? q, string? filter, long? id, CancellationToken ct)
    {
        var posts = db.MarketplaceTasks.AsNoTracking().Where(t => !t.IsPrivate);
        var now = DateTime.UtcNow;
        var query = filter switch
        {
            "removed" => posts.Where(t => t.Status == "moderated"),
            "reported" => posts.Where(t => db.SafetyReports.Any(r => r.TaskId == t.Id && r.Status == "open")),
            _ => posts.Where(t => t.Status == "open" && (t.PostExpiresUtc == null || t.PostExpiresUtc > now))
        };
        q = q?.Trim();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(t => t.Title.Contains(q) || t.Description.Contains(q));
        Posts = await query.OrderByDescending(t => t.Id).Take(100).ToListAsync(ct);
        if (id is null) return;
        Selected = await posts.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (Selected is null) return;
        Reports = await db.SafetyReports.AsNoTracking().Where(r => r.TaskId == id).OrderByDescending(r => r.Id).Take(30).ToListAsync(ct);
        try
        {
            History = await db.Set<PostModerationAction>().AsNoTracking().Where(a => a.TaskId == id)
                .OrderByDescending(a => a.Id).Take(50).ToListAsync(ct);
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1146) { Unavailable = true; }
    }

    public async Task<IActionResult> OnPostReviewAsync(long id, string decision, string? reason, bool confirmed, CancellationToken ct)
    {
        var restore = decision == "restore";
        if (restore && !User.IsInRole("admin")) return Forbid("AdminCookie");
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor <= 0)
            return Forbid("AdminCookie");
        if (!ModelState.IsValid || !confirmed || (!restore && (decision != "remove" || !Reasons.Contains(reason))))
        {
            Notice = "Choose a reason and confirm the action.";
            return RedirectToPage(new { id });
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var task = (await db.MarketplaceTasks.FromSqlInterpolated(
                $"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (task is null || task.IsPrivate || task.Status != (restore ? "moderated" : "open") ||
                (restore && task.PostExpiresUtc <= DateTime.UtcNow) ||
                await db.MarketplaceOffers.AnyAsync(o => o.TaskId == id && o.Status == "accepted", ct))
            {
                Notice = "This listing is no longer eligible for that action. Review its current status.";
                return RedirectToPage(new { id });
            }

            task.Status = restore ? "open" : "moderated";
            task.CancellationReason = restore ? null : reason;
            db.Set<PostModerationAction>().Add(new PostModerationAction
            {
                TaskId = id, ActorId = actor, Decision = decision,
                Reason = restore ? "Listing restored" : reason!, CreatedAtUtc = DateTime.UtcNow
            });
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, id, "moderation",
                restore ? "Your task listing was restored." : $"Your task was removed from listings. Reason: {reason}. Open My Tasks for details."));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            Notice = restore ? "Listing restored." : "Listing removed and client notified.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException)
        {
            Notice = "Moderation storage is unavailable. The listing was not changed.";
        }
        return RedirectToPage(new { id, filter = restore ? "published" : "removed" });
    }
}
