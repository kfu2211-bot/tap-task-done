using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersModel(ApplicationDbContext db, ILogger<AdminUsersModel> logger) : PageModel
{
    public record UserRow(int Id, string Name, string Email, string Role, bool Restricted, bool Suspended) { public OffensePolicy.Summary Moderation { get; init; } = OffensePolicy.Evaluate(null, 0, DateTime.UtcNow); }
    public record ActionRow(int UserId, string Action, string Reason, DateTime CreatedAt, string Admin);
    public List<UserRow> Users { get; private set; } = [];
    public List<ActionRow> History { get; private set; } = [];
    public bool Unavailable { get; private set; }
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [TempData(Key = "AdminUsersNotice")] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var users = await db.Users.AsNoTracking().OrderByDescending(u => u.Id).Select(u => new { u.Id, u.FirstName, u.LastName, u.Username, u.Role }).ToListAsync(ct);
            foreach (var u in users)
            {
                var moderation = AccountModerationRules.IsStaff(u.Role) ? OffensePolicy.Evaluate(null, 0, DateTime.UtcNow) : await OffensePolicy.GetAsync(db, u.Id, ct);
                Users.Add(new UserRow(u.Id, ($"{u.FirstName} {u.LastName}").Trim(), u.Username ?? "", u.Role ?? "client", moderation.Warning, moderation.Suspended) { Moderation = moderation });
            }
            History = await (from a in db.AccountModerationActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id orderby a.Id descending select new ActionRow(a.UserId, a.Action, a.Reason, a.CreatedAt, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).Take(500).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning(ex, "Account moderation unavailable"); Unavailable = true; }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int userId, string? action, string? reason, string? incidentKey, int expectedNumber, bool reviewConfirmed, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        if (action is not ("offense" or "restore") || !AccountModerationRules.IsValidReason(reason) || userId == AdminId)
        { Notice = "Choose an action and provide a reason of up to 500 characters."; return RedirectToPage(new { embedded = Embedded }); }
        incidentKey = (incidentKey ?? "").Trim().ToLowerInvariant();
        if (action == "offense" && (!reviewConfirmed || !System.Text.RegularExpressions.Regex.IsMatch(incidentKey, @"^[a-z0-9][a-z0-9:._/-]{2,99}$")))
        { Notice = "Confirm your evidence review and enter a unique incident reference, such as report:123. Reuse the same reference for the same incident."; return RedirectToPage(new { embedded = Embedded }); }
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var target = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {userId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            if (target is null || AccountModerationRules.IsStaff(target.Role)) { Notice = "Staff accounts cannot be moderated here."; return RedirectToPage(new { embedded = Embedded }); }
            var latest = await db.Set<AccountOffense>().Where(o => o.UserId == userId).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
            var current = await OffensePolicy.GetAsync(db, userId, ct);
            var oldRestriction = await db.AccountRestrictions.SingleOrDefaultAsync(r => r.UserId == userId, ct);
            string audit;
            if (action == "offense")
            {
                if (current.Legacy && current.Suspended) { Notice = "Review and lift the previous suspension before starting the new numbered offense policy."; return RedirectToPage(new { embedded = Embedded }); }
                if (await db.Set<AccountOffense>().AnyAsync(o => o.UserId == userId && o.IncidentKey == incidentKey, ct))
                { Notice = "This incident is already recorded. It has not been counted again."; return RedirectToPage(new { embedded = Embedded }); }
                int number = (latest?.Number ?? 0) + 1;
                if (number > 5 || current.Permanent) { Notice = "This account already reached the final offense level. Review its history instead."; return RedirectToPage(new { embedded = Embedded }); }
                if (expectedNumber != number) { Notice = "The offense history changed. Check the updated next penalty before confirming again."; return RedirectToPage(new { embedded = Embedded }); }
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
                if (expectedNumber != (latest?.Number ?? 0) + 1) { Notice = "The penalty changed. Review the updated account before lifting it."; return RedirectToPage(new { embedded = Embedded }); }
                if (!current.Warning && !current.Suspended) { Notice = "No active penalty needs lifting."; return RedirectToPage(new { embedded = Embedded }); }
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
        return RedirectToPage(new { embedded = Embedded });
    }
}
