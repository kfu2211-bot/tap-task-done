using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class JobCancellation
{
    public const string Escalated = "cancellation-escalated";
    public const string FollowUpReason = "Cancellation follow-up";
    public static bool FollowUpDue(TaskProgressEvent? request, DateTime now) =>
        request?.Status == TaskProgress.CancellationRequested && now >= request.CreatedAtUtc.AddHours(24);
    public static bool CanRequest(string status) => status is "assigned" or "on-the-way" or "arrived" or "in-progress" or "awaiting-confirmation";
    public static bool NeedsReview(string status) => status is "on-the-way" or "arrived" or "in-progress" or "awaiting-confirmation";
    public static bool CanRespond(int actor, int client, int freelancer, TaskProgressEvent? request) =>
        actor > 0 && (actor == client || actor == freelancer) && request?.Status == TaskProgress.CancellationRequested && actor != request.ActorId;
    public static async Task<bool> PendingAsync(ApplicationDbContext db, long id, CancellationToken ct) =>
        await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id && (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined))
            .OrderByDescending(e => e.Id).Select(e => e.Status).FirstOrDefaultAsync(ct) == TaskProgress.CancellationRequested;

    public static async Task<TaskProgress.Result> DecideAsync(ApplicationDbContext db, int actor, long id,
        string decision, string? reason, CancellationToken ct)
    {
        if (actor <= 0 || decision is not ("request" or "accept" or "decline" or "escalate"))
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
        if (decision == "escalate")
        {
            if ((actor != task.ClientId && actor != freelancer) || !FollowUpDue(latest, DateTime.UtcNow))
                return new(false, "Admin follow-up is available after 24 hours without a cancellation decision.");
            if (await db.Set<TaskProgressEvent>().AnyAsync(e => e.TaskId == id && e.Status == Escalated && e.Id > latest!.Id, ct))
                return new(true, "This cancellation is already with the admin for review.");
            db.Add(new SafetyReport { ReporterId = actor, TaskId = id, Target = "Task #" + id,
                Reason = FollowUpReason, Description = "Cancellation request has no decision after 24 hours. Reason: " + latest!.Note,
                Status = "open", Priority = 2, CreatedAt = DateTime.UtcNow });
            db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = Escalated, CreatedAtUtc = DateTime.UtcNow,
                Note = "Admin review requested after 24 hours without a cancellation decision." });
            foreach (var user in new[] { task.ClientId, freelancer.Value }.Distinct())
                db.Add(MarketplaceNotification.Create(user, id, "cancellation", "Cancellation follow-up sent for admin review. No automatic cancellation or penalty applies."));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(true, "Cancellation follow-up submitted. Track the admin response in Safety & Reports.");
        }
        if (decision == "request")
        {
            reason = reason?.Trim();
            if ((actor != freelancer && actor != task.ClientId) || pending || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                return new(false, "Enter a cancellation reason up to 500 characters. Only one request can be pending.");
            db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = TaskProgress.CancellationRequested,
                Note = reason, CreatedAtUtc = DateTime.UtcNow });
            db.Add(MarketplaceNotification.Create(actor == task.ClientId ? freelancer.Value : task.ClientId, id, "cancellation", "Your counterpart requested cancellation. Review the reason in Tracking."));
        }
        else
        {
            if (!CanRespond(actor, task.ClientId, freelancer.Value, latest)) return new(false, "Only the other participant can respond to this cancellation request.");
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
            db.Add(MarketplaceNotification.Create(latest!.ActorId, id, "cancellation",
                decision == "accept" ? "Your counterpart approved cancellation of this job." : "Your counterpart declined cancellation. Discuss the next steps in Messages."));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, decision == "request" ? "Cancellation request sent to the other participant."
            : decision == "accept" ? "Job cancelled." : "Cancellation declined.");
    }

    public static async Task<TaskProgress.Result> ReviewAsync(ApplicationDbContext db, int admin, long reportId, string decision, string? note, CancellationToken ct)
    {
        note = note?.Trim();
        if (decision is not ("cancel" or "continue") || note is not { Length: >= 10 and <= 500 } ||
            !await db.Users.AnyAsync(u => u.Id == admin && (u.Role == "admin" || u.Role == "moderator"), ct))
            return new(false, "Choose a decision and enter a review reason of 10–500 characters.");
        var reportTaskId = await db.SafetyReports.Where(r => r.Id == reportId && r.Reason == FollowUpReason).Select(r => r.TaskId).SingleOrDefaultAsync(ct);
        if (!reportTaskId.HasValue) return new(false, "Cancellation follow-up not found.");
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, reportTaskId.Value, ct);
        var report = await db.SafetyReports.SingleAsync(r => r.Id == reportId, ct);
        var task = await db.MarketplaceTasks.SingleAsync(t => t.Id == reportTaskId, ct);
        var request = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == task.Id &&
            (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        var escalated = request is not null && await db.Set<TaskProgressEvent>().AnyAsync(e => e.TaskId == task.Id && e.Status == Escalated && e.Id > request.Id, ct);
        if (report.Status is not ("open" or "reviewing") || !CanRequest(task.Status) || !FollowUpDue(request, DateTime.UtcNow) || !escalated || report.CreatedAt < request!.CreatedAtUtc)
            return new(false, "The cancellation has already changed. Refresh and review the latest history.");
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.Status == "accepted").Select(o => o.FreelancerId).SingleAsync(ct);
        if (decision == "cancel")
        {
            task.Status = "cancelled"; task.CancellationReason = note;
            await db.MarketplaceBookings.Where(b => b.TaskId == task.Id && (b.Status == "accepted" || b.Status == "pending"))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "cancelled"), ct);
        }
        var message = decision == "cancel" ? "Admin approved cancellation after review." : "Admin kept the job active after review. Discuss the next steps in Messages.";
        db.Add(new TaskProgressEvent { TaskId = task.Id, ActorId = admin, Status = decision == "cancel" ? "cancelled" : TaskProgress.CancellationDeclined, Note = note, CreatedAtUtc = DateTime.UtcNow });
        report.Status = "resolved";
        db.Add(new SafetyReportAction { ReportId = reportId, AdminId = admin, Status = "resolved", Note = note, PublicMessage = message + " " + note, ReviewConfirmed = true, CreatedAt = DateTime.UtcNow });
        foreach (var user in new[] { task.ClientId, freelancer }.Distinct()) db.Add(MarketplaceNotification.Create(user, task.Id, "cancellation", message));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, message);
    }
}
