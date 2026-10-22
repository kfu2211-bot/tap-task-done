using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public class AdminReportsModel(ApplicationDbContext db) : PageModel
{
    public record ReportItem(SafetyReport Report, string Reporter);
    public record ActionItem(SafetyReportAction Action, string AdminName);
    public List<ReportItem> Reports { get; private set; } = [];
    public List<ActionItem> Actions { get; private set; } = [];
    public Dictionary<long, string> TaskTitles { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [TempData] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        Reports = await (from r in db.SafetyReports.AsNoTracking() join u in db.Users.AsNoTracking() on r.ReporterId equals u.Id
            orderby r.Id descending select new ReportItem(r, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).Take(100).ToListAsync(ct);
        var ids = Reports.Select(x => x.Report.Id).ToList();
        var taskIds = Reports.Where(x => x.Report.TaskId.HasValue).Select(x => x.Report.TaskId!.Value).Distinct().ToList();
        TaskTitles = await db.MarketplaceTasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        Actions = await (from a in db.SafetyReportActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id
            where ids.Contains(a.ReportId) orderby a.Id descending
            select new ActionItem(a, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(long id, string? status, string? note, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        note = (note ?? "").Trim();
        if (!ModelState.IsValid || status is not ("reviewing" or "resolved" or "dismissed") || note.Length > 1000)
        { Notice = "Check the status and note."; return RedirectToPage(new { embedded = Embedded }); }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.SafetyReports.Where(r => r.Id == id && (r.Status == "open" || r.Status == "reviewing") && r.Status != status)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status), ct);
        if (changed == 0) { Notice = "This report is unavailable or already has that status."; return RedirectToPage(new { embedded = Embedded }); }
        db.SafetyReportActions.Add(new SafetyReportAction { ReportId = id, AdminId = AdminId, Status = status, Note = note, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "Report status updated.";
        return RedirectToPage(new { embedded = Embedded });
    }
}
