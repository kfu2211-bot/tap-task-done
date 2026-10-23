using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// Call inside a serializable transaction, before changing marketplace state.
public static class ModerationGuard
{
    public static async Task<bool> IsSuspendedAsync(ApplicationDbContext db, int userId, CancellationToken ct = default)
    {
        var latest = await db.AccountModerationActions.AsNoTracking()
            .Where(x => x.UserId == userId && (x.Action == "suspend" || x.Action == "restore"))
            .OrderByDescending(x => x.Id).Select(x => x.Action).FirstOrDefaultAsync(ct);
        return latest == "suspend";
    }
    public static async Task<bool> AllowedAsync(ApplicationDbContext db, IWebHostEnvironment environment, CancellationToken ct, params int[] userIds)
    {
        if (!environment.IsDevelopment()) return true;
        try
        {
            foreach (var id in userIds.Distinct().Order())
            {
                if (id <= 0) return false;
                // Serialize against admin decisions even when no restriction row exists.
                var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
                if (user is null || await db.AccountRestrictions.AsNoTracking().AnyAsync(x => x.UserId == id && x.IsRestricted, ct) || await IsSuspendedAsync(db, id, ct)) return false;
            }
            return true;
        }
        catch (MySqlConnector.MySqlException) { return false; }
    }
}
