using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// Call inside a serializable transaction, before changing marketplace state.
public static class ModerationGuard
{
    public static async Task<bool> IsSuspendedAsync(ApplicationDbContext db, int userId, CancellationToken ct = default)
    {
        return await OffensePolicy.IsSuspendedAsync(db, userId, ct);
    }
    public static async Task<bool> AllowedAsync(ApplicationDbContext db, IWebHostEnvironment environment, CancellationToken ct, params int[] userIds)
    {
        try
        {
            foreach (var id in userIds.Distinct().Order())
            {
                if (id <= 0) return false;
                // Serialize against admin decisions even when no restriction row exists.
                var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
                if (user is null || await IsSuspendedAsync(db, id, ct)) return false;
            }
            return true;
        }
        catch (MySqlConnector.MySqlException) { return false; }
    }
}
