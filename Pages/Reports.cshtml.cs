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
    private static readonly string[] Reasons = ["Scam or fake account", "No-show", "Harassment or inappropriate behavior", "Work dispute", "Other"];
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public List<SafetyReport> Reports { get; private set; } = [];
    public List<MarketplaceTask> Tasks { get; private set; } = [];
    public Dictionary<long, string> FreelancerNames { get; private set; } = [];
    [TempData] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (UserId <= 0 || !await db.Users.AnyAsync(u => u.Id == UserId, ct)) return Challenge();
        Reports = await db.SafetyReports.AsNoTracking().Where(r => r.ReporterId == UserId).OrderByDescending(r => r.Id).Take(100).ToListAsync(ct);
        Tasks = await db.MarketplaceTasks.AsNoTracking().Where(t => t.ClientId == UserId ||
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == UserId && o.Status == "accepted")).OrderByDescending(t => t.Id).Take(100).ToListAsync(ct);
        var taskIds = Tasks.Select(t => t.Id).ToArray();
        var freelancers = await (from offer in db.MarketplaceOffers.AsNoTracking()
            join user in db.Users.AsNoTracking() on offer.FreelancerId equals user.Id
            where taskIds.Contains(offer.TaskId) && offer.Status == "accepted"
            orderby offer.Id descending
            select new { offer.TaskId, user.FirstName, user.LastName }).ToListAsync(ct);
        FreelancerNames = freelancers.GroupBy(x => x.TaskId).ToDictionary(g => g.Key, g =>
        {
            var name = $"{g.First().FirstName} {g.First().LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? "Freelancer name unavailable" : name;
        });
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string? target, string? reason, string? description, long? taskId, List<IFormFile>? evidence, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        target = (target ?? "").Trim(); reason = (reason ?? "").Trim(); description = (description ?? "").Trim();
        var taskAllowed = !taskId.HasValue || await db.MarketplaceTasks.AnyAsync(t => t.Id == taskId && (t.ClientId == UserId ||
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == UserId && o.Status == "accepted")), ct);
        if (!ModelState.IsValid || target.Length is < 1 or > 80 || !Reasons.Contains(reason) || description.Length is < 1 or > 1000 || !taskAllowed)
        { Notice = "Please check your report details and choose a task belonging to you."; return RedirectToPage(); }
        var files = evidence ?? [];
        if (files.Count > 3 || files.Any(f => f.Length <= 0 || f.Length > 3 * 1024 * 1024))
        { Notice = "Choose up to three files, each no larger than 3 MB."; return RedirectToPage(); }
        try
        {
            var uploads = new List<(string Type, byte[] Data)>();
            foreach (var file in files)
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);
                var bytes = stream.ToArray();
                var type = DetectType(bytes);
                if (type is null) { Notice = "Only valid JPG, PNG, and PDF files are accepted."; return RedirectToPage(); }
                uploads.Add((type, bytes));
            }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var report = new SafetyReport { ReporterId = UserId, TaskId = taskId, Target = target, Reason = reason, Description = description, Status = "open", CreatedAt = DateTime.UtcNow };
            db.SafetyReports.Add(report);
            await db.SaveChangesAsync(ct);
            foreach (var upload in uploads) db.SafetyReportEvidence.Add(new SafetyReportEvidence { ReportId = report.Id, ContentType = upload.Type, Data = upload.Data });
            await db.SaveChangesAsync(ct); Notice = "Report submitted. You can check its status below.";
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or MySqlConnector.MySqlException or IOException) { logger.LogError(ex, "Unable to save safety report"); Notice = "The report could not be saved. Please try again."; }
        return RedirectToPage();
    }

    private static string? DetectType(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "image/jpeg";
        if (bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (bytes.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        return null;
    }
}
