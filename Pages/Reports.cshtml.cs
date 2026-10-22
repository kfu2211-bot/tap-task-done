using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize]
public class ReportsModel(ApplicationDbContext db, ILogger<ReportsModel> logger) : PageModel
{
    private static readonly string[] Reasons = ["Scam or fake account", "Payment outside the app", "No-show", "Harassment or inappropriate behavior", "Work dispute", "Other"];
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public List<SafetyReport> Reports { get; private set; } = [];
    public List<MarketplaceTask> Tasks { get; private set; } = [];
    [TempData] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId <= 0 || !await db.Users.AnyAsync(u => u.Id == UserId, ct)) return Challenge();
        Reports = await db.SafetyReports.AsNoTracking().Where(r => r.ReporterId == UserId).OrderByDescending(r => r.Id).Take(100).ToListAsync(ct);
        Tasks = await db.MarketplaceTasks.AsNoTracking().Where(t => t.ClientId == UserId ||
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == UserId && o.Status == "accepted")).OrderByDescending(t => t.Id).Take(100).ToListAsync(ct);
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string? target, string? reason, string? description, long? taskId, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        target = (target ?? "").Trim(); reason = (reason ?? "").Trim(); description = (description ?? "").Trim();
        var taskAllowed = !taskId.HasValue || await db.MarketplaceTasks.AnyAsync(t => t.Id == taskId && (t.ClientId == UserId ||
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == UserId && o.Status == "accepted")), ct);
        if (!ModelState.IsValid || target.Length is < 1 or > 80 || !Reasons.Contains(reason) || description.Length is < 1 or > 1000 || !taskAllowed)
        { Notice = "Please check your report details and choose a task belonging to you."; return RedirectToPage(); }
        try
        {
            db.SafetyReports.Add(new SafetyReport { ReporterId = UserId, TaskId = taskId, Target = target, Reason = reason, Description = description, Status = "open", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); Notice = "Report submitted. You can check its status below.";
        }
        catch (DbUpdateException ex) { logger.LogError(ex, "Unable to save safety report"); Notice = "The report could not be saved. Please try again."; }
        return RedirectToPage();
    }
}
