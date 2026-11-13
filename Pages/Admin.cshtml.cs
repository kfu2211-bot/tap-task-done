using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

public sealed class AdminModel(ApplicationDbContext db, ILogger<AdminModel> logger, IWebHostEnvironment environment, IdentityVerificationAvailability verification) : PageModel
{
    public record RegisteredUser(string Name, string Email, string Role);
    public record AdminActivity(DateTime Time, string Admin, string Action, string Detail);
    public List<RegisteredUser> RegisteredUsers { get; private set; } = [];
    public List<AdminActivity> Activities { get; private set; } = [];
    public bool ActivityIncomplete { get; private set; }
    public bool UsersUnavailable { get; private set; }
    public bool IsDevelopment => environment.IsDevelopment();
    public bool VerificationAvailable => verification.Enabled;
    public int? PendingVerifications { get; private set; }
    public int? OpenReports { get; private set; }

    public record InboxItem(string Kind, long Id, string Status, DateTime Time, int Priority);
    public List<InboxItem> Inbox { get; } = [];
    public bool InboxIncomplete { get; private set; }

    private async Task LoadInboxAsync(IQueryable<InboxItem> query, CancellationToken ct)
    {
        try { Inbox.AddRange(await query.OrderByDescending(x => x.Time).ThenByDescending(x => x.Id).Take(50).ToListAsync(ct)); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            InboxIncomplete = true;
            logger.LogWarning(ex, "Could not load an admin inbox source");
        }
    }

    public record StaffAccount(string Name, string Email, string Role, DateTime? LastSeen);
    public List<StaffAccount> Staff { get; private set; } = [];
    public bool StaffUnavailable { get; private set; }
    public bool StaffHistoryUnavailable { get; private set; }
    [TempData] public string? StaffNotice { get; set; }

    public async Task<IActionResult> OnPostStaffRoleAsync(string? email, string? role, CancellationToken ct)
    {
        if (!User.IsInRole("admin") || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Forbid("AdminCookie");

        IActionResult Settings() => RedirectToPage("/Admin", null, null, "settings");
        email = email?.Trim();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(email) || email.Length > 254 || role is not ("admin" or "moderator" or "client"))
        {
            StaffNotice = "Enter an existing account email and choose a role.";
            return Settings();
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var actor = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {adminId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            if (actor?.Role != "admin") return Forbid("AdminCookie");

            var changed = await db.Users.Where(u => u.Username == email && u.Id != adminId &&
                    (u.Role == "admin" || u.Role == "moderator" || u.Role == "client" || u.Role == null))
                .ExecuteUpdateAsync(update => update.SetProperty(u => u.Role, role), ct);
            await transaction.CommitAsync(ct);
            StaffNotice = changed == 1 ? "Staff role saved." : "No eligible account was updated. Check the email and choose an account other than your own.";
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Unable to update staff access");
            StaffNotice = "Staff access could not be saved. Please try again.";
        }
        return Settings();
    }

    private async Task LoadStaffAsync(CancellationToken ct)
    {
        if (!User.IsInRole("admin")) return;
        try
        {
            var staff = await db.Users.AsNoTracking().Where(u => u.Role == "admin" || u.Role == "moderator")
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Username, u.Role }).ToListAsync(ct);
            Staff = staff.Select(u => new StaffAccount($"{u.FirstName} {u.LastName}".Trim(), u.Username ?? "", u.Role!, null)).ToList();
            try
            {
                var ids = staff.Select(u => u.Id).ToArray();
                var lastSeen = await db.Set<LoginSession>().AsNoTracking().Where(s => ids.Contains(s.UserId))
                    .GroupBy(s => s.UserId).Select(g => new { Id = g.Key, Time = g.Max(s => s.LastSeenUtc) })
                    .ToDictionaryAsync(s => s.Id, s => s.Time, ct);
                Staff = staff.Select((u, index) => Staff[index] with { LastSeen = lastSeen.TryGetValue(u.Id, out var time) ? time : null }).ToList();
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            {
                StaffHistoryUnavailable = true;
                logger.LogWarning(ex, "Staff session history is unavailable");
            }
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            StaffUnavailable = true;
            logger.LogWarning(ex, "Staff accounts are unavailable");
        }
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        await LoadStaffAsync(cancellationToken);
        await LoadInboxAsync(db.SafetyReports.AsNoTracking().Select(r => new InboxItem("report", r.Id, r.Status, r.CreatedAt, r.Priority)), cancellationToken);
        await LoadInboxAsync(db.IdentityRequests.AsNoTracking().Select(r => new InboxItem("verification", r.Id, r.Status, r.CreatedAt, 0)), cancellationToken);
        await LoadInboxAsync(db.Set<AccountDeletionRequest>().AsNoTracking().Where(r => r.RequestedUtc.HasValue)
            .Select(r => new InboxItem("deletion", r.UserId, r.Status, r.RequestedUtc!.Value, 0)), cancellationToken);
        try
        {
            var users = await db.Users.AsNoTracking().Where(u => u.Role != "deleted").OrderByDescending(u => u.Id)
                .Select(u => new { u.FirstName, u.LastName, u.Username, u.Role })
                .ToListAsync(cancellationToken);
            RegisteredUsers = users.Select(u => new RegisteredUser(
                ($"{u.FirstName} {u.LastName}").Trim(), u.Username ?? "", u.Role ?? "client")).ToList();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Unable to load registered users for admin.");
            UsersUnavailable = true;
        }
            try { PendingVerifications = await db.IdentityRequests.AsNoTracking().CountAsync(x => x.Status == "pending", cancellationToken); }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1146 && !verification.Enabled) { PendingVerifications = 0; }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Unable to load verification count."); }
        try { OpenReports = await db.SafetyReports.AsNoTracking().CountAsync(x => x.Status == "open" || x.Status == "reviewing", cancellationToken); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Unable to load report count."); }
        try
        {
            var reports = await (from a in db.SafetyReportActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id
                orderby a.Id descending select new { a.CreatedAt, a.Status, a.ReportId, u.FirstName, u.LastName }).Take(100).ToListAsync(cancellationToken);
            Activities.AddRange(reports.Select(a => new AdminActivity(a.CreatedAt, ($"{a.FirstName} {a.LastName}").Trim(), "Safety report updated", $"Report #{a.ReportId}: {a.Status}")));
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { ActivityIncomplete = true; logger.LogWarning(ex, "Unable to load report activity."); }

        {
            try
            {
                var moderation = await (from a in db.AccountModerationActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id
                    orderby a.Id descending select new { a.UserId, a.Action, a.CreatedAt, u.FirstName, u.LastName }).Take(100).ToListAsync(cancellationToken);
                Activities.AddRange(moderation.Select(a => new AdminActivity(a.CreatedAt, ($"{a.FirstName} {a.LastName}").Trim(), "Account " + a.Action, $"Account #{a.UserId}")));
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { ActivityIncomplete = true; logger.LogWarning(ex, "Unable to load account moderation activity."); }
        }
        if (verification.Enabled)
        {
            try
            {
                var decisions = await (from r in db.IdentityRequests.AsNoTracking() join u in db.Users.AsNoTracking() on r.ReviewedById equals u.Id
                    where r.ReviewedAt != null orderby r.Id descending select new { r.Id, r.Status, r.ReviewedAt, u.FirstName, u.LastName }).Take(100).ToListAsync(cancellationToken);
                Activities.AddRange(decisions.Select(r => new AdminActivity(r.ReviewedAt!.Value, ($"{r.FirstName} {r.LastName}").Trim(), "Verification reviewed", $"Request #{r.Id}: {r.Status}")));
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { ActivityIncomplete = true; logger.LogWarning(ex, "Unable to load verification activity."); }
        }
        Activities = Activities.OrderByDescending(a => a.Time).Take(100).ToList();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        if (LoginSecurity.Enabled(HttpContext.RequestServices.GetRequiredService<IConfiguration>())) await LoginSecurity.LogoutAsync(db, HttpContext, HttpContext.RequestAborted);
        await HttpContext.SignOutAsync("AdminCookie");
        return RedirectToPage("/Login");
    }
}
