using System.Data;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class AppTransactions
{
    // TiDB supports READ COMMITTED in pessimistic mode. Protect workflow
    // invariants with stable account/task FOR UPDATE locks and conditional
    // writes; do not rely on SERIALIZABLE range locks or stale snapshots.
    public const IsolationLevel Isolation = IsolationLevel.ReadCommitted;
    public static async Task LockTaskAsync(ApplicationDbContext db, long id, CancellationToken ct)
    {
        await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
    }
}
