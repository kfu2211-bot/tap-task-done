using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersModel(ApplicationDbContext db, ILogger<AdminUsersModel> logger, IConfiguration config) : PageModel
{
    public record UserRow(int Id, string Name, string Email, string Role, bool Restricted, bool Suspended) { public bool HasPhoto { get; init; } public bool CanDelete { get; init; } public OffensePolicy.Summary Moderation { get; init; } = OffensePolicy.Evaluate(null, 0, DateTime.UtcNow); }
    public record ActionRow(int UserId, string Action, string Reason, DateTime CreatedAt, string Admin);
    public List<UserRow> Users { get; private set; } = [];
    public List<ActionRow> History { get; private set; } = [];
    public bool Unavailable { get; private set; }
    public bool SessionsUnavailable { get; private set; }
    public bool VerificationUnavailable { get; private set; }
    public Dictionary<int, AccountVerificationState> VerificationStates { get; private set; } = [];
    public Dictionary<int, UserActivitySession> LatestSessions { get; private set; } = [];
    public HashSet<int> OnlineUsers { get; private set; } = [];
    public DateTime Now { get; } = DateTime.UtcNow;
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [BindProperty(SupportsGet = true)] public long? ReportId { get; set; }
    [TempData(Key = "AdminUsersNotice")] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (ReportId.HasValue) return RedirectToPage("/AdminReports", null, new { embedded = Embedded, recordId = ReportId }, "reported-account");
        Response.Headers.CacheControl = "no-store";
        try
        {
            var canDelete = User.IsInRole("admin") && VerificationMailer.Enabled(config);
            var users = await db.Users.AsNoTracking().Where(u => u.Role != "deleted").OrderByDescending(u => u.Id).Select(u => new { u.Id, u.FirstName, u.LastName, u.Username, u.Role, HasPhoto = u.ProfilePicture != null, CanDelete = canDelete && u.Username != null && u.Username != "" &&
                !db.VerifiedEmails.Any(v => v.UserId == u.Id && v.Email == u.Username) && !db.AccountModerationActions.Any(a => a.UserId == u.Id && a.Action == "email-unverify") }).ToListAsync(ct);
            foreach (var u in users)
            {
                var moderation = AccountModerationRules.IsStaff(u.Role) ? OffensePolicy.Evaluate(null, 0, DateTime.UtcNow) : await OffensePolicy.GetAsync(db, u.Id, ct);
                Users.Add(new UserRow(u.Id, ($"{u.FirstName} {u.LastName}").Trim(), u.Username ?? "", u.Role ?? "client", moderation.Warning, moderation.Suspended) { Moderation = moderation, HasPhoto = u.HasPhoto, CanDelete = u.CanDelete && u.Id != AdminId && !AccountModerationRules.IsStaff(u.Role) });
            }
            History = await (from a in db.AccountModerationActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id orderby a.Id descending select new ActionRow(a.UserId, a.Action, a.Reason, a.CreatedAt, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).Take(500).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning(ex, "Account moderation unavailable"); Unavailable = true; }
        if (!Unavailable) {
            try {
                var ids = Users.Select(u => u.Id).ToArray();
                var emails = await db.VerifiedEmails.AsNoTracking().Where(v => ids.Contains(v.UserId)).ToDictionaryAsync(v => v.UserId, ct);
                var identities = await db.IdentityRequests.AsNoTracking().Where(r => ids.Contains(r.UserId) && !db.IdentityRequests.Any(n => n.UserId == r.UserId && n.Id > r.Id))
                    .Select(r => new { r.Id, r.UserId, r.Status, r.ReviewedAt }).ToDictionaryAsync(r => r.UserId, ct);
                foreach (var user in Users) {
                    var email = emails.GetValueOrDefault(user.Id); var identity = identities.GetValueOrDefault(user.Id);
                    VerificationStates[user.Id] = new(user.Id, user.Email, email?.Email == user.Email && email is not null, email?.VerifiedUtc, identity?.Id ?? 0, identity?.Status ?? "not verified", identity?.ReviewedAt);
                }
            } catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) {
                logger.LogWarning(ex, "Account verification controls unavailable"); VerificationUnavailable = true;
            }
        }
        if (!Unavailable)
        {
            try
            {
                await LoadActivityAsync(ct);
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { logger.LogWarning(ex, "User session summaries unavailable"); SessionsUnavailable = true; }
        }
        return Page();
    }

    private async Task LoadActivityAsync(CancellationToken ct)
    {
        var sessions = db.Set<UserActivitySession>().AsNoTracking();
        var latest = await sessions.Where(s => db.Users.Any(u => u.Id == s.UserId && u.Role != "deleted") &&
            !sessions.Any(other => other.UserId == s.UserId && other.StartedUtc > s.StartedUtc)).ToListAsync(ct);
        LatestSessions = latest.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.LastActiveUtc).ThenBy(s => s.Id).First());
        var cutoff = Now.AddMinutes(-5);
        OnlineUsers = (await sessions.Where(s => s.LogoutUtc == null && s.LastActiveUtc > cutoff).Select(s => s.UserId).Distinct().ToListAsync(ct)).ToHashSet();
    }

    public async Task<IActionResult> OnGetActivityAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            await LoadActivityAsync(ct);
            return new JsonResult(LatestSessions.Values.Select(s => new {
                userId = s.UserId, online = OnlineUsers.Contains(s.UserId),
                timeIn = DisplayTime.Philippine(s.StartedUtc), lastActive = DisplayTime.Philippine(s.LastActiveUtc),
                timeOut = s.LogoutUtc.HasValue ? DisplayTime.Philippine(s.LogoutUtc.Value) : UserActivity.Status(s, Now) == "Inactive" ? "~ " + DisplayTime.Philippine(s.LastActiveUtc) : "—"
            }));
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning(ex, "Live user activity unavailable"); return StatusCode(503); }
    }

    public async Task<IActionResult> OnGetSessionsAsync(int userId, int pageNumber = 1, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (userId <= 0 || pageNumber < 1 || pageNumber > 10000) return BadRequest();
        try
        {
            var rows = await db.Set<UserActivitySession>().AsNoTracking().Where(s => s.UserId == userId && db.Users.Any(u => u.Id == userId && u.Role != "deleted"))
                .OrderByDescending(s => s.StartedUtc).ThenBy(s => s.Id).Skip((pageNumber - 1) * 20).Take(21).ToListAsync(ct);
            return new JsonResult(new { hasNext = rows.Count > 20, rows = rows.Take(20).Select(s => new {
                timeIn = DisplayTime.Philippine(s.StartedUtc), lastActive = DisplayTime.Philippine(s.LastActiveUtc),
                timeOut = s.LogoutUtc.HasValue ? DisplayTime.Philippine(s.LogoutUtc.Value) : UserActivity.Status(s, Now) == "Inactive" ? "~ " + DisplayTime.Philippine(s.LastActiveUtc) : "—",
                status = UserActivity.Status(s, Now)
            }) });
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning(ex, "User session history unavailable"); return StatusCode(503); }
    }

    public async Task<IActionResult> OnPostVerificationAsync(int userId, string type, string change, string? reason, string? revision, bool confirmChange, CancellationToken ct)
    {
        if (!User.IsInRole("admin") || AdminId <= 0) return Forbid("AdminCookie");
        if (!ModelState.IsValid) { Notice = "Check the verification action and reason."; return RedirectToPage(new { embedded = Embedded }); }
        try {
            await AdminAccountVerification.ChangeAsync(db, AdminId, userId, type, change, reason, confirmChange, revision, ct);
            Notice = "Verification change saved. The decision is recorded in admin history.";
        } catch (VerificationChangeException ex) { Notice = ex.Message; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) {
            logger.LogWarning(ex, "Unable to change account verification"); Notice = "Verification could not be updated. Please try again.";
        }
        return RedirectToPage("/AdminUsers", null, new { embedded = Embedded }, "user-" + userId);
    }

    public async Task<IActionResult> OnPostDeleteUnverifiedAsync(int userId, bool confirmDeletion, CancellationToken ct)
    {
        if (!User.IsInRole("admin") || AdminId <= 0) return Forbid("AdminCookie");
        if (!VerificationMailer.Enabled(config) || !confirmDeletion || !ModelState.IsValid || userId <= 0)
            return BadRequest();

        try
        {
            await AccountDeletion.DeleteUnverifiedAsync(db, AdminId, userId, ct);
            Notice = "Unverified account deleted.";
        }
        catch (DeletionFlowException ex) { Notice = ex.Message; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException)
        {
            logger.LogWarning(ex, "Unable to delete unverified account");
            Notice = "The account could not be deleted. Please try again.";
        }
        return RedirectToPage(new { embedded = Embedded });
    }

    public async Task<IActionResult> OnPostAsync(int userId, string? action, string? reason, string? incidentKey, int expectedNumber, bool reviewConfirmed, CancellationToken ct)
    {
        IActionResult ReturnToReview()
        {
            if (!ReportId.HasValue) return RedirectToPage(new { embedded = Embedded });
            TempData["AdminReports.Notice"] = Notice;
            return RedirectToPage("/AdminReports", null, new { embedded = Embedded, recordId = ReportId, accountId = userId }, "reported-account");
        }
        if (AdminId <= 0) return Challenge();
        if (action == "offense" && !ReportId.HasValue)
        {
            Notice = "Open a Safety Report to record an offense.";
            return RedirectToPage(new { embedded = Embedded });
        }
        if (action is not ("offense" or "restore") || !AccountModerationRules.IsValidReason(reason) || userId == AdminId)
        { Notice = "Choose an action and provide a reason of up to 500 characters."; return ReturnToReview(); }
        if (ReportId.HasValue)
        {
            var report = await db.SafetyReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == ReportId, ct);
            if (report is null || report.ReporterId == userId || action != "offense") return BadRequest();
            incidentKey = $"report:{report.Id}";
        }
        incidentKey = (incidentKey ?? "").Trim().ToLowerInvariant();
        if (action == "offense" && (!reviewConfirmed || !System.Text.RegularExpressions.Regex.IsMatch(incidentKey, @"^[a-z0-9][a-z0-9:._/-]{2,99}$")))
        { Notice = "Confirm your evidence review and enter a unique incident reference, such as report:123. Reuse the same reference for the same incident."; return ReturnToReview(); }
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
            if (ReportId is long reportId)
            {
                var report = (await db.SafetyReports.FromSqlInterpolated($"SELECT * FROM safety_reports WHERE Id = {reportId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
                if (report is null || report.Status == "dismissed" || report.ReporterId == userId || action != "offense")
                    return BadRequest();
            }
            var target = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {userId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            if (target is null || AccountModerationRules.IsStaff(target.Role)) { Notice = "Staff accounts cannot be moderated here."; return ReturnToReview(); }
            if (ReportId.HasValue && await db.Set<AccountOffense>().AnyAsync(o => o.IncidentKey == incidentKey, ct))
            { Notice = "An offense was already recorded for this report."; return ReturnToReview(); }
            var latest = await db.Set<AccountOffense>().Where(o => o.UserId == userId).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
            var current = await OffensePolicy.GetAsync(db, userId, ct);
            var oldRestriction = await db.AccountRestrictions.SingleOrDefaultAsync(r => r.UserId == userId, ct);
            string audit;
            if (action == "offense")
            {
                if (current.Legacy && current.Suspended) { Notice = "Review and lift the previous suspension before starting the new numbered offense policy."; return ReturnToReview(); }
                if (await db.Set<AccountOffense>().AnyAsync(o => o.UserId == userId && o.IncidentKey == incidentKey, ct))
                { Notice = "This incident is already recorded. It has not been counted again."; return ReturnToReview(); }
                int number = (latest?.Number ?? 0) + 1;
                if (number > 5 || current.Permanent) { Notice = "This account already reached the final offense level. Review its history instead."; return ReturnToReview(); }
                if (expectedNumber != number) { Notice = "The offense history changed. Check the updated next penalty before confirming again."; return ReturnToReview(); }
                var now = DateTime.UtcNow;
                var until = OffensePolicy.Days(number) > 0 ? now.AddDays(OffensePolicy.Days(number)) : (DateTime?)null;
                // A new offense cannot shorten an already active temporary suspension.
                if (latest?.SuspendedUntilUtc > until && latest.LiftedAtUtc is null) until = latest.SuspendedUntilUtc;
                db.Set<AccountOffense>().Add(new AccountOffense { UserId = userId, AdminId = AdminId, IncidentKey = incidentKey, Reason = reason!.Trim(), Number = number,
                    RecoveryTarget = OffensePolicy.Target(number), CreatedAtUtc = now, SuspendedUntilUtc = until, Permanent = number >= 5 });
                audit = $"Confirmed offense #{number} ({incidentKey}): {OffensePolicy.NextPenalty(number)}. {reason!.Trim()}";
            }
            else
            {
                if (expectedNumber != (latest?.Number ?? 0) + 1) { Notice = "The penalty changed. Review the updated account before lifting it."; return ReturnToReview(); }
                if (!current.Warning && !current.Suspended) { Notice = "No active penalty needs lifting."; return ReturnToReview(); }
                if (latest is not null) latest.LiftedAtUtc = DateTime.UtcNow;
                audit = "Admin review lifted the current penalty; offense history remains. " + reason!.Trim();
            }
            if (oldRestriction is not null) { oldRestriction.IsRestricted = false; oldRestriction.UpdatedAt = DateTime.UtcNow; oldRestriction.UpdatedById = AdminId; }
            db.AccountModerationActions.Add(new AccountModerationAction { UserId = userId, AdminId = AdminId, Action = action!, Reason = audit.Length > 500 ? audit[..500] : audit, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            Notice = "Decision saved. The user has an account notice.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning(ex, "Unable to save offense decision"); Notice = "Decision could not be saved. Check the offense database setup."; }
        return ReturnToReview();
    }
}
