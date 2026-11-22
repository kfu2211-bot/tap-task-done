using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// The same policy is applied to known and unknown addresses, so the timer does
// not disclose whether an account exists. Refreshing/clearing a cookie cannot reset it.
public sealed class PasswordResetSendState
{
    public string EmailHash { get; set; } = "";
    public DateTime WindowUtc { get; set; }
    public DateTime LastSentUtc { get; set; }
    public int SendCount { get; set; }
    private static readonly SemaphoreSlim SetupLock = new(1, 1);
    private static bool ready;
    public static string Key(string email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
    public static int WaitSeconds(PasswordResetSendState row, DateTime now) => Math.Max(
        OtpResendPolicy.Remaining(row.LastSentUtc, now, row.SendCount), row.SendCount >= 5 ? Math.Max(0, (int)Math.Ceiling((row.WindowUtc.AddMinutes(15) - now).TotalSeconds)) : 0);
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS password_reset_send_states (
          EmailHash VARCHAR(64) CHARACTER SET ascii NOT NULL PRIMARY KEY,
          WindowUtc DATETIME(6) NOT NULL, LastSentUtc DATETIME(6) NOT NULL, SendCount INT NOT NULL,
          INDEX IX_reset_send_last (LastSentUtc)
        ) ENGINE=InnoDB;
        """;
    private static async Task EnsureStorageAsync(ApplicationDbContext db, CancellationToken ct)
    {
        await SetupLock.WaitAsync(ct);
        try
        {
            if (ready) return;
            try { await db.Database.ExecuteSqlRawAsync(SchemaSql, ct); }
            catch (MySqlConnector.MySqlException) { await db.Set<PasswordResetSendState>().AsNoTracking().Take(1).ToListAsync(ct); }
            ready = true;
        }
        finally { SetupLock.Release(); }
    }
    public static async Task<int> CurrentWaitAsync(ApplicationDbContext db, string email, CancellationToken ct)
    {
        await EnsureStorageAsync(db, ct);
        var key = Key(email);
        var row = await db.Set<PasswordResetSendState>().AsNoTracking().SingleOrDefaultAsync(r => r.EmailHash == key, ct);
        return row is null ? 0 : WaitSeconds(row, DateTime.UtcNow);
    }
    public static async Task<(bool Allowed, int Wait)> AcquireAsync(ApplicationDbContext db, string email, DateTime now, CancellationToken ct)
    {
        await EnsureStorageAsync(db, ct);
        await db.Set<PasswordResetSendState>().Where(r => r.LastSentUtc < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        var key = Key(email);
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO password_reset_send_states (EmailHash, WindowUtc, LastSentUtc, SendCount) VALUES ({key}, {now}, {now.AddDays(-1)}, 0)", ct);
        var row = (await db.Set<PasswordResetSendState>().FromSqlInterpolated($"SELECT * FROM password_reset_send_states WHERE EmailHash = {key} FOR UPDATE").ToListAsync(ct)).Single();
        var wait = WaitSeconds(row, now);
        if (wait > 0) return (false, wait);
        if (row.WindowUtc <= now.AddMinutes(-15)) { row.WindowUtc = now; row.SendCount = 0; }
        row.SendCount++; row.LastSentUtc = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (true, WaitSeconds(row, now));
    }
}
