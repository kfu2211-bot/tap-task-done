using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

public sealed class AdminModel(ApplicationDbContext db, ILogger<AdminModel> logger, IWebHostEnvironment environment) : PageModel
{
    public record RegisteredUser(string Name, string Email, string Role);
    public record AdminActivity(DateTime Time, string Admin, string Action, string Detail);
    public List<RegisteredUser> RegisteredUsers { get; private set; } = [];
    public List<AdminActivity> Activities { get; private set; } = [];
    public bool ActivityIncomplete { get; private set; }
    public bool UsersUnavailable { get; private set; }
    public bool IsDevelopment => environment.IsDevelopment();
    public int? PendingVerifications { get; private set; }
    public int? OpenReports { get; private set; }
    public int? OpenDisputes { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var users = await db.Users.AsNoTracking().OrderByDescending(u => u.Id)
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
        if (environment.IsDevelopment())
        {
            try { PendingVerifications = await db.IdentityRequests.AsNoTracking().CountAsync(x => x.Status == "pending", cancellationToken); }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Unable to load verification count."); }
        }
        try { OpenReports = await db.SafetyReports.AsNoTracking().CountAsync(x => x.Status == "open" || x.Status == "reviewing", cancellationToken); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Unable to load report count."); }
        try { OpenDisputes = await db.MarketplaceDisputes.AsNoTracking().CountAsync(x => x.Status == "open", cancellationToken); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Unable to load dispute count."); }
        try
        {
            var reports = await (from a in db.SafetyReportActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id
                orderby a.Id descending select new { a.CreatedAt, a.Status, a.ReportId, u.FirstName, u.LastName }).Take(100).ToListAsync(cancellationToken);
            Activities.AddRange(reports.Select(a => new AdminActivity(a.CreatedAt, ($"{a.FirstName} {a.LastName}").Trim(), "Safety report updated", $"Report #{a.ReportId}: {a.Status}")));
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { ActivityIncomplete = true; logger.LogWarning(ex, "Unable to load report activity."); }
        try
        {
            var disputes = await (from d in db.MarketplaceDisputes.AsNoTracking() join u in db.Users.AsNoTracking() on d.ClosedById equals u.Id
                where d.Status == "closed" && d.ClosedAt != null orderby d.Id descending
                select new { d.Id, d.ClosedAt, u.FirstName, u.LastName }).Take(100).ToListAsync(cancellationToken);
            Activities.AddRange(disputes.Select(d => new AdminActivity(d.ClosedAt!.Value, ($"{d.FirstName} {d.LastName}").Trim(), "Dispute closed", $"Dispute #{d.Id}")));
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { ActivityIncomplete = true; logger.LogWarning(ex, "Unable to load dispute activity."); }
        if (environment.IsDevelopment())
        {
            try
            {
                var moderation = await (from a in db.AccountModerationActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id
                    orderby a.Id descending select new { a.UserId, a.Action, a.CreatedAt, u.FirstName, u.LastName }).Take(100).ToListAsync(cancellationToken);
                Activities.AddRange(moderation.Select(a => new AdminActivity(a.CreatedAt, ($"{a.FirstName} {a.LastName}").Trim(), "Account " + a.Action, $"Account #{a.UserId}")));
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { ActivityIncomplete = true; logger.LogWarning(ex, "Unable to load account moderation activity."); }
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
        await HttpContext.SignOutAsync("AdminCookie");
        return RedirectToPage("/Login");
    }
}
