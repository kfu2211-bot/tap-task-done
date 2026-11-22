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
    public record ReportItem(SafetyReport Report, string Reporter, int Priority);
    public record ActionItem(SafetyReportAction Action, string AdminName);
    public record EvidenceItem(long Id, long ReportId, string ContentType);
    public List<ReportItem> Reports { get; private set; } = [];
    public List<ActionItem> Actions { get; private set; } = [];
    public List<EvidenceItem> Evidence { get; private set; } = [];
    public Dictionary<long, string> TaskTitles { get; private set; } = [];
    public record ReviewAccount(int Id, string Name, string Email, OffensePolicy.Summary? Moderation);
    public List<ReviewAccount> AccountMatches { get; private set; } = [];
    public ReviewAccount? SelectedAccount { get; private set; }
    public AccountOffense? RecordedOffense { get; private set; }
    [BindProperty(SupportsGet = true)] public long? RecordId { get; set; }
    public bool DatabaseUnavailable { get; private set; }
    public bool EvidenceUnavailable { get; private set; }
    public bool HistoryUnavailable { get; private set; }
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    public string ReviewNote { get; private set; } = "";
    public string ReviewMessage { get; private set; } = "";
    public string? ReviewStatus { get; private set; }
    public bool ReviewConfirmed { get; private set; }
    [BindProperty(SupportsGet = true)] public string StatusFilter { get; set; } = "open";
    [BindProperty(SupportsGet = true)] public string PriorityFilter { get; set; } = "all";
    [TempData(Key = "AdminReports.Notice")] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnPostCancellationReviewAsync(long id, string decision, string? note, bool reviewed, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        if (!reviewed || !ModelState.IsValid) { Notice = "Confirm that you reviewed the evidence before making a cancellation decision."; return RedirectToPage(new { recordId = id, embedded = Embedded }); }
        try { Notice = (await JobCancellation.ReviewAsync(db, AdminId, id, decision, note, ct)).Message; }
        catch (Exception ex) when (ex is DbUpdateException or MySqlConnector.MySqlException)
        { logger.LogWarning("Cancellation review could not be saved ({Type}).", ex.GetType().Name); Notice = "Cancellation review could not be saved. Refresh and try again."; }
        return RedirectToPage(new { recordId = id, embedded = Embedded, statusFilter = "all" });
    }

    public async Task<IActionResult> OnGetAccountMatchesAsync(long recordId, string? accountSearch, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        Response.Headers.CacheControl = "no-store";
        var term = accountSearch?.Trim();
        if (term is not { Length: >= 1 and <= 120 }) return new JsonResult(Array.Empty<ReviewAccount>());
        var reporter = await db.SafetyReports.AsNoTracking().Where(r => r.Id == recordId)
            .Select(r => (int?)r.ReporterId).SingleOrDefaultAsync(ct);
        if (reporter is null) return NotFound();
        return new JsonResult(await FindAccountsAsync(reporter.Value, term, ct));
    }

    private async Task<List<ReviewAccount>> FindAccountsAsync(int reporterId, string term, CancellationToken ct)
    {
        var matches = await db.Users.AsNoTracking().Where(u => u.Id != reporterId && u.Id != AdminId &&
            u.Role != "deleted" && (((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(term) ||
            (u.Username ?? "").Contains(term))).OrderBy(u => u.Id).Take(20).ToListAsync(ct);
        return matches.Where(u => !AccountModerationRules.IsStaff(u.Role))
            .Select(u => new ReviewAccount(u.Id, $"{u.FirstName} {u.LastName}".Trim(), u.Username ?? "", null)).ToList();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct, long? recordId = null, int? accountId = null, string? accountSearch = null)
    {
        if (AdminId <= 0) return Challenge();
        Response.Headers.CacheControl = "no-store";
        try
        {
        var query = from r in SafetyReportPriority.Rank(db.SafetyReports.AsNoTracking()) join u in db.Users.AsNoTracking() on r.Report.ReporterId equals u.Id
            select new { r.Report, r.Priority, u.FirstName, u.LastName };
        if (recordId.HasValue) query = query.Where(x => x.Report.Id == recordId.Value);
        if (recordId.HasValue) StatusFilter = "all";
        if (StatusFilter == "history") query = query.Where(x => x.Report.Status == "resolved" || x.Report.Status == "dismissed");
        else if (StatusFilter == "pending") query = query.Where(x => x.Report.Status == "open" || x.Report.Status == "reviewing");
        else if (StatusFilter is "open" or "reviewing" or "resolved" or "dismissed") query = query.Where(x => x.Report.Status == StatusFilter);
        else StatusFilter = "all";
        if (!recordId.HasValue && int.TryParse(PriorityFilter, out var priority) && priority is >= 1 and <= 3) query = query.Where(x => x.Priority == priority);
        else PriorityFilter = "all";
        Sort = Sort is "oldest" or "priority" or "recent" ? Sort : StatusFilter == "open" ? "priority" : "recent";
        var rows = await (Sort == "oldest" ? query.OrderBy(x => x.Report.CreatedAt).ThenBy(x => x.Report.Id)
            : Sort == "priority" ? query.OrderByDescending(x => x.Priority).ThenBy(x => x.Report.CreatedAt).ThenBy(x => x.Report.Id)
            : query.OrderByDescending(x => x.Report.CreatedAt).ThenByDescending(x => x.Report.Id)).Take(100).ToListAsync(ct);
        Reports = rows.Select(x => new ReportItem(x.Report, ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(), x.Priority)).ToList();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load safety reports.");
            DatabaseUnavailable = true;
            Notice = ex is MySqlConnector.MySqlException { Number: 1054 } mysql && mysql.Message.Contains("Priority", StringComparison.OrdinalIgnoreCase)
                ? "Safety reports need a database update before they can be displayed. Contact the administrator to apply the report priority setup."
                : "Safety reports could not be loaded. Please try again or contact the administrator.";
            return Page();
        }
        if (recordId.HasValue && Reports.SingleOrDefault()?.Report is { } report)
        {
            try
            {
                var incident = $"report:{report.Id}";
                RecordedOffense = await db.Set<AccountOffense>().AsNoTracking().FirstOrDefaultAsync(o => o.IncidentKey == incident, ct);
                var accounts = db.Users.AsNoTracking().Where(u => u.Id != report.ReporterId && u.Id != AdminId);
                if (RecordedOffense is not null) accountId = RecordedOffense.UserId;
                if (accountId is int selectedId)
                {
                    var account = await accounts.SingleOrDefaultAsync(u => u.Id == selectedId, ct);
                    if (account is not null && !AccountModerationRules.IsStaff(account.Role))
                        SelectedAccount = new(account.Id, $"{account.FirstName} {account.LastName}".Trim(), account.Username ?? "", await OffensePolicy.GetAsync(db, account.Id, ct));
                }
                else if (!string.IsNullOrWhiteSpace(accountSearch) && accountSearch.Trim().Length is >= 1 and <= 120)
                {
                    AccountMatches = await FindAccountsAsync(report.ReporterId, accountSearch.Trim(), ct);
                    if (AccountMatches.Count == 0) Notice = "No matching accounts. Try the full name or email.";
                }
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Unable to load report moderation");
                Notice = "Account moderation could not be loaded.";
            }
        }
        var ids = Reports.Select(x => x.Report.Id).ToList();
        if (!recordId.HasValue || ids.Count == 0) return Page();
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
        if (id <= 0 || (ModelState.TryGetValue(nameof(id), out var reportIdField) && reportIdField.Errors.Count > 0))
            return BadRequest();
        // Queue filters and the detail-view ID are navigation state, not review inputs.
        ModelState.Remove(nameof(StatusFilter));
        ModelState.Remove(nameof(PriorityFilter));
        ModelState.Remove(nameof(Sort));
        ModelState.Remove(nameof(RecordId));
        note = (note ?? "").Trim();
        publicMessage = (publicMessage ?? "").Trim();
        if (!ModelState.IsValid || !ReportReviewRules.Valid(status, note, publicMessage, reviewConfirmed))
        {
            RecordId = id;
            ReviewNote = note;
            ReviewMessage = publicMessage;
            ReviewStatus = status;
            ReviewConfirmed = reviewConfirmed;
            if (publicMessage.Length is < 10 or > 1000) ModelState.AddModelError("publicMessage", "Enter a reporter update of 10–1000 characters.");
            if (note.Length > 1000 || (status is "resolved" or "dismissed" && note.Length < 10))
                ModelState.AddModelError("note", "Enter an internal decision reason of 10–1000 characters.");
            if (status is "resolved" or "dismissed" && !reviewConfirmed)
                ModelState.AddModelError("reviewConfirmed", "Confirm that you reviewed the evidence.");
            if (status is not ("reviewing" or "resolved" or "dismissed"))
                ModelState.AddModelError("status", "Choose a valid review status.");
            if (!new[] { "note", "publicMessage", "reviewConfirmed", "status" }.Any(key =>
                ModelState.TryGetValue(key, out var field) && field.Errors.Count > 0))
                ModelState.AddModelError("review", "Check the review details and try again.");
            return await OnGetAsync(ct, id);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var report = await db.SafetyReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (report is null) return NotFound();
        if (report.Reason == JobCancellation.FollowUpReason && status is "resolved" or "dismissed")
        {
            var request = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == report.TaskId &&
                (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
            var active = await db.MarketplaceTasks.AnyAsync(t => t.Id == report.TaskId && (t.Status == "assigned" || t.Status == "on-the-way" || t.Status == "arrived" || t.Status == "in-progress" || t.Status == "awaiting-confirmation"), ct);
            if (active && request?.Status == TaskProgress.CancellationRequested && request.CreatedAtUtc <= report.CreatedAt)
            { Notice = "Use Resolve cancellation follow-up to record the job decision and notify both participants."; return RedirectToPage(new { recordId = id, embedded = Embedded }); }
        }
        var changed = await db.SafetyReports.Where(r => r.Id == id && (r.Status == "open" || r.Status == "reviewing") && r.Status != status)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status), ct);
        if (changed == 0)
        {
            await transaction.RollbackAsync(ct);
            var currentStatus = await db.SafetyReports.Where(r => r.Id == id).Select(r => r.Status).SingleOrDefaultAsync(ct);
            if (currentStatus is null) return NotFound();
            Notice = "This report already has a saved status. No duplicate update was sent.";
            return RedirectToPage("/AdminReports", new { embedded = Embedded, statusFilter = currentStatus is "resolved" or "dismissed" ? "history" : currentStatus });
        }
        db.SafetyReportActions.Add(new SafetyReportAction { ReportId = id, AdminId = AdminId, Status = status!, Note = note, PublicMessage = publicMessage, ReviewConfirmed = reviewConfirmed, CreatedAt = DateTime.UtcNow });
        if (report.TaskId.HasValue) db.MarketplaceNotifications.Add(MarketplaceNotification.Create(report.ReporterId, report.TaskId.Value, "safety_report", $"Report #{id}: {status}. Open Safety & Reports to read the admin update."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = status == "reviewing"
            ? "Review saved. The report moved to Under review."
            : "Report closed and moved to History.";
        Notice += " The reporter can read the update in Safety & Reports.";
        var email = await db.Users.Where(u => u.Id == report.ReporterId).Select(u => u.Username).SingleOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(email))
        {
            try { await SmtpMailer.SendAsync(config, email, $"Tap, Task, Done — report #{id} update", $"Your report #{id} is now {status}.\n\n{publicMessage}\n\nOpen Safety & Reports on the website to review your report history. If you have new evidence, submit a follow-up report referencing #{id}.", ct); }
            catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException or FormatException or ArgumentException)
            {
                logger.LogWarning("Report update email failed ({Type}).", ex.GetType().Name);
                Notice += " Email could not be delivered. The update is available on the website.";
            }
        }
        return RedirectToPage("/AdminReports", new { embedded = Embedded, statusFilter = status == "reviewing" ? "reviewing" : "history" });
    }
}
