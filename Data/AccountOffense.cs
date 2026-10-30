using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class AccountOffense
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int AdminId { get; set; }
    public string IncidentKey { get; set; } = "";
    public string Reason { get; set; } = "";
    public int Number { get; set; }
    public int RecoveryTarget { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SuspendedUntilUtc { get; set; }
    public bool Permanent { get; set; }
    public DateTime? LiftedAtUtc { get; set; }
}

public static class OffensePolicy
{
    public record Summary(int Offenses, int Target, int Completed, bool Warning, bool Suspended, bool Permanent, DateTime? Until, bool Legacy = false)
    {
        public string Penalty => Permanent ? "Permanent suspension" : Suspended ? Until.HasValue ? $"Suspended until {DisplayTime.Philippine(Until.Value)}" : "Legacy suspension — admin review required" : Warning ? $"Orange warning · recovery {Completed}/{Target}" : "Active";
    }
    public static int Target(int number) => number switch { 2 => 3, 3 => 6, 4 => 10, _ => 0 };
    public static int Days(int number) => number switch { 3 => 3, 4 => 7, _ => 0 };
    public static Summary Evaluate(AccountOffense? offense, int completed, DateTime now)
    {
        if (offense is null) return new(0, 0, 0, false, false, false, null);
        bool lifted = offense.LiftedAtUtc.HasValue;
        bool permanent = !lifted && offense.Permanent;
        bool suspended = permanent || (!lifted && offense.SuspendedUntilUtc > now);
        int progress = suspended ? 0 : Math.Clamp(completed, 0, offense.RecoveryTarget);
        return new(offense.Number, offense.RecoveryTarget, progress, !lifted && offense.RecoveryTarget > progress && !permanent, suspended, permanent, lifted ? null : offense.SuspendedUntilUtc);
    }
    public static string NextPenalty(int number) => number switch
    {
        1 => "Written warning", 2 => "Orange warning · 3 completed tasks",
        3 => "3-day suspension · then 6 completed tasks", 4 => "7-day suspension · then 10 completed tasks",
        _ => "Permanent suspension"
    };
    // Only completed events recorded by the owning client count. Distinct task IDs prevent duplicate credit.
    public static IQueryable<TaskProgressEvent> QualifyingCompletions(ApplicationDbContext db, int userId, DateTime since) =>
        db.Set<TaskProgressEvent>().AsNoTracking().Where(e => e.Status == "completed" && e.CreatedAtUtc > since &&
            db.MarketplaceTasks.Any(t => t.Id == e.TaskId && t.Status == "completed" && e.ActorId == t.ClientId &&
                (t.ClientId == userId || db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.Status == "accepted" && o.FreelancerId == userId))));

    public static async Task<Summary> GetAsync(ApplicationDbContext db, int userId, CancellationToken ct = default)
    {
        var offense = await db.Set<AccountOffense>().AsNoTracking().Where(o => o.UserId == userId).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        if (offense is not null)
        {
            var since = offense.SuspendedUntilUtc ?? offense.CreatedAtUtc;
            var count = offense.RecoveryTarget > 0 && !offense.Permanent && offense.LiftedAtUtc is null && since < now
                ? await QualifyingCompletions(db, userId, since).Select(e => e.TaskId).Distinct().CountAsync(ct) : 0;
            return Evaluate(offense, count, now);
        }
        // Legacy records are preserved, not silently reinterpreted as numbered offenses.
        var action = await db.AccountModerationActions.AsNoTracking().Where(a => a.UserId == userId && (a.Action == "suspend" || a.Action == "restore"))
            .OrderByDescending(a => a.Id).Select(a => a.Action).FirstOrDefaultAsync(ct);
        var restriction = await db.AccountRestrictions.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId && r.IsRestricted, ct);
        var completed = restriction is null ? 0 : Math.Min(3, await QualifyingCompletions(db, userId, restriction.UpdatedAt).Select(e => e.TaskId).Distinct().CountAsync(ct));
        return new(0, restriction is null ? 0 : 3, completed, restriction is not null && completed < 3, action == "suspend", false, null, restriction is not null || action is not null);
    }
    public static async Task<bool> IsSuspendedAsync(ApplicationDbContext db, int userId, CancellationToken ct = default)
    {
        var latest = await db.Set<AccountOffense>().AsNoTracking().Where(o => o.UserId == userId).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        if (latest is not null) return Evaluate(latest, 0, DateTime.UtcNow).Suspended;
        return await db.AccountModerationActions.AsNoTracking().Where(a => a.UserId == userId && (a.Action == "suspend" || a.Action == "restore"))
            .OrderByDescending(a => a.Id).Select(a => a.Action).FirstOrDefaultAsync(ct) == "suspend";
    }
}
