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
    public record UserRow(int Id, string Name, string Email, string Role, bool Restricted, bool Suspended);
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
            var restricted = await db.AccountRestrictions.AsNoTracking().Where(r => r.IsRestricted).Select(r => r.UserId).ToListAsync(ct);
            var suspensionEvents = await db.AccountModerationActions.AsNoTracking().Where(a => a.Action == "suspend" || a.Action == "restore").OrderByDescending(a => a.Id).Select(a => new { a.UserId, a.Action }).ToListAsync(ct);
            var suspended = suspensionEvents.GroupBy(a => a.UserId).Where(g => g.First().Action == "suspend").Select(g => g.Key).ToHashSet();
            Users = users.Select(u => new UserRow(u.Id, ($"{u.FirstName} {u.LastName}").Trim(), u.Username ?? "", u.Role ?? "client", restricted.Contains(u.Id), suspended.Contains(u.Id))).ToList();
            History = await (from a in db.AccountModerationActions.AsNoTracking() join u in db.Users.AsNoTracking() on a.AdminId equals u.Id orderby a.Id descending select new ActionRow(a.UserId, a.Action, a.Reason, a.CreatedAt, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())).Take(500).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning(ex, "Account moderation unavailable"); Unavailable = true; }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int userId, string? action, string? reason, CancellationToken ct)
    {
        if (AdminId <= 0) return Challenge();
        if (!AccountModerationRules.IsValidAction(action) || !AccountModerationRules.IsValidReason(reason) || userId == AdminId)
        { Notice = "Choose an action and provide a reason of up to 500 characters."; return RedirectToPage(new { embedded = Embedded }); }
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var target = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {userId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            if (target is null || AccountModerationRules.IsStaff(target.Role)) { Notice = "Staff accounts cannot be moderated here."; return RedirectToPage(new { embedded = Embedded }); }
            var status = await db.AccountRestrictions.SingleOrDefaultAsync(x => x.UserId == userId, ct);
            var suspended = await ModerationGuard.IsSuspendedAsync(db, userId, ct);
            if ((action == "restrict" && (status?.IsRestricted == true || suspended)) || (action == "suspend" && suspended) || (action == "restore" && status?.IsRestricted != true && !suspended))
            { Notice = "The account is already in that state."; return RedirectToPage(new { embedded = Embedded }); }
            if (action is "restrict" or "restore")
            {
                if (status is null) { status = new AccountRestriction { UserId = userId }; db.AccountRestrictions.Add(status); }
                status.IsRestricted = action == "restrict"; status.UpdatedAt = DateTime.UtcNow; status.UpdatedById = AdminId;
            }
            db.AccountModerationActions.Add(new AccountModerationAction { UserId = userId, AdminId = AdminId, Action = action!, Reason = reason!.Trim(), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            Notice = "Decision saved and the user has an account notice.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        { logger.LogWarning(ex, "Unable to save moderation decision"); Notice = "Decision could not be saved. Check the moderation database setup."; }
        return RedirectToPage(new { embedded = Embedded });
    }
}
