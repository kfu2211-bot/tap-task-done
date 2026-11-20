using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class JobCancellation
{
    public static bool CanRequest(string status) => status is "on-the-way" or "arrived" or "in-progress" or "awaiting-confirmation";

    public static async Task<TaskProgress.Result> DecideAsync(ApplicationDbContext db, int actor, long id,
        string decision, string? reason, CancellationToken ct)
    {
        if (actor <= 0 || decision is not ("request" or "accept" or "decline"))
            return new(false, "This cancellation action is unavailable.");
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id, ct);
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted")
            .Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
        if (task is null || freelancer is null || !CanRequest(task.Status))
            return new(false, "This job has changed and cannot use this cancellation action.");
        var latest = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id &&
            (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined))
            .OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        var pending = latest?.Status == TaskProgress.CancellationRequested;
        if (decision == "request")
        {
            reason = reason?.Trim();
            if (actor != freelancer || pending || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                return new(false, "Enter a cancellation reason up to 500 characters. Only one request can be pending.");
            db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = TaskProgress.CancellationRequested,
                Note = reason, CreatedAtUtc = DateTime.UtcNow });
            db.Add(MarketplaceNotification.Create(task.ClientId, id, "cancellation", "The freelancer requested cancellation. Review the reason in Tracking."));
        }
        else
        {
            if (actor != task.ClientId || !pending) return new(false, "This cancellation request is no longer pending.");
            if (decision == "accept")
            {
                task.Status = "cancelled";
                task.CancellationReason = latest!.Note;
                await db.MarketplaceBookings.Where(b => b.TaskId == id && (b.Status == "pending" || b.Status == "accepted"))
                    .ExecuteUpdateAsync(update => update.SetProperty(b => b.Status, "cancelled"), ct);
            }
            db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor,
                Status = decision == "accept" ? "cancelled" : TaskProgress.CancellationDeclined,
                Note = latest!.Note, CreatedAtUtc = DateTime.UtcNow });
            db.Add(MarketplaceNotification.Create(freelancer.Value, id, "cancellation",
                decision == "accept" ? "The client approved cancellation of this job." : "The client declined cancellation. Discuss the next steps in Messages."));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, decision == "request" ? "Cancellation request sent to the client."
            : decision == "accept" ? "Job cancelled." : "Cancellation declined.");
    }
}
