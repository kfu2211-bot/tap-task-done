using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public class AdminReportsModel(ApplicationDbContext db, ILogger<AdminReportsModel> logger, IConfiguration config) : PageModel
{
    public record ReportItem(SafetyReport Report, string Reporter);
    public record ActionItem(SafetyReportAction Action, string AdminName);
    public record EvidenceItem(long Id, long ReportId, string ContentType);
    public List<ReportItem> Reports { get; private set; } = [];
    public List<ActionItem> Actions { get; private set; } = [];
    public List<EvidenceItem> Evidence { get; private set; } = [];
    public Dictionary<long, string> TaskTitles { get; private set; } = [];
    public bool DatabaseUnavailable { get; private set; }
    public bool EvidenceUnavailable { get; private set; }
    public bool HistoryUnavailable { get; private set; }
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [TempData(Key = "AdminReports.Notice")] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        Response.Headers.CacheControl = "no-store";
        try
        {
        Reports = await (from r in db.SafetyReports.AsNoTracking() join u in db.Users.AsNoTracking() on r.ReporterId equals u.Id
            orderby r.Id descending select new ReportItem(r, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).Take(100).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load safety reports.");
            DatabaseUnavailable = true;
            Notice = "Safety reports could not be loaded. Please check the report database setup and try again.";
            return Page();
        }
        var ids = Reports.Select(x => x.Report.Id).ToList();
        if (ids.Count == 0) return Page();
        try
        {
        Evidence = await db.SafetyReportEvidence.AsNoTracking().Where(e => ids.Contains(e.ReportId)).OrderBy(e => e.Id).Select(e => new EvidenceItem(e.Id, e.ReportId, e.ContentType)).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Unable to load report evidence.");
            EvidenceUnavailable = true;
            Notice = "Reports are shown, but attachments could not be loaded. Please check the evidence database setup.";
        }
        try
        {
        var taskIds = Reports.Where(x => x.Report.TaskId.HasValue).Select(x => x.Report.TaskId!.Value).Distinct().ToList();
        if (taskIds.Count > 0) TaskTitles = await db.MarketplaceTasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        Actions = await (from a in db.SafetyReportActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id
            where ids.Contains(a.ReportId) orderby a.Id descending
            select new ActionItem(a, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Unable to load report task details or action history.");
            HistoryUnavailable = true;
            Notice = (Notice is null ? "" : Notice + " ") + "Some task details or action history could not be loaded. Please check the database setup.";
        }
        return Page();
    }

    public async Task<IActionResult> OnGetEvidenceAsync(long id, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        var evidence = await db.SafetyReportEvidence.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (evidence is null) return NotFound();
        var extension = evidence.ContentType switch { "image/jpeg" => "jpg", "image/png" => "png", "application/pdf" => "pdf", _ => null };
        if (extension is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(evidence.Data, "application/octet-stream", $"report-evidence-{evidence.Id}.{extension}");
    }

    public async Task<IActionResult> OnPostUpdateAsync(long id, string? status, string? note, string? publicMessage, bool reviewConfirmed, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        note = (note ?? "").Trim();
        publicMessage = (publicMessage ?? "").Trim();
        if (!ModelState.IsValid || !ReportReviewRules.Valid(status, note, publicMessage, reviewConfirmed))
        { Notice = "Provide a reporter update (10–1000 characters). Closing a report also requires a completed evidence review and an internal decision reason."; return RedirectToPage(new { embedded = Embedded }); }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var report = await db.SafetyReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (report is null) return NotFound();
        var changed = await db.SafetyReports.Where(r => r.Id == id && (r.Status == "open" || r.Status == "reviewing") && r.Status != status)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status), ct);
        if (changed == 0) { Notice = "This report is unavailable or already has that status."; return RedirectToPage(new { embedded = Embedded }); }
        db.SafetyReportActions.Add(new SafetyReportAction { ReportId = id, AdminId = AdminId, Status = status!, Note = note, PublicMessage = publicMessage, ReviewConfirmed = reviewConfirmed, CreatedAt = DateTime.UtcNow });
        if (report.TaskId.HasValue) db.MarketplaceNotifications.Add(MarketplaceNotification.Create(report.ReporterId, report.TaskId.Value, "safety_report", $"Report #{id}: {status}. Open Safety & Reports to read the admin update."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "Decision saved. The reporter can read the update in Safety & Reports.";
        var email = await db.Users.Where(u => u.Id == report.ReporterId).Select(u => u.Username).SingleOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(email))
        {
            try { await SmtpMailer.SendAsync(config, email, $"Tap, Task, Done — report #{id} update", $"Your report #{id} is now {status}.\n\n{publicMessage}\n\nOpen Safety & Reports on the website to review your report history. If you have new evidence, submit a follow-up report referencing #{id}.", ct); }
            catch (System.Net.Mail.SmtpException ex) { logger.LogWarning("Report update email failed ({Reason}).", SmtpMailer.Diagnostic(ex)); Notice += " Email could not be delivered; the update remains available on the website."; }
        }
        return RedirectToPage(new { embedded = Embedded });
    }
}
