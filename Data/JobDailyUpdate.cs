using Microsoft.EntityFrameworkCore;
namespace tts.Data;
public static class JobDailyUpdate
{
    public const string Event = "daily-progress-update";
    public static bool Allowed(MarketplaceTask task, int actor, int freelancer, string? note) =>
        actor > 0 && actor == freelancer && task.JobType == "multi-day" && task.Status == "in-progress" && note?.Trim().Length is > 0 and <= 1000;
    public static async Task<TaskProgress.Result> SaveAsync(ApplicationDbContext db, int actor, long id, string? note, CancellationToken ct)
    {
        note = note?.Trim();
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id, ct);
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => o.FreelancerId).SingleOrDefaultAsync(ct);
        if (task is null || !Allowed(task, actor, freelancer, note)) return new(false, "Only the assigned freelancer can add a progress update to ongoing Multi-Day work. Use 1–1,000 characters.");
        var latest = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id && (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        if (latest?.Status == TaskProgress.CancellationRequested || !AgreementRules.CanProceed(await AgreementRules.LatestAsync(db, id, ct)))
            return new(false, "Resolve pending cancellation or work details before adding a progress update.");
        var previous = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id && e.Status == Event).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        if (previous is not null && previous.Note == note && previous.CreatedAtUtc.AddHours(8).Date == DateTime.UtcNow.AddHours(8).Date) return new(true, "This progress update is already saved.");
        db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = Event, Note = note, CreatedAtUtc = DateTime.UtcNow });
        db.Add(MarketplaceNotification.Create(task.ClientId, id, "agreement", "The freelancer added a Multi-Day progress update. The project remains in progress."));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, "Progress update saved. Mark work finished only when the whole project is ready for review.");
    }
}
