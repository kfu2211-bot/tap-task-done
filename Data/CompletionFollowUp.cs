using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace tts.Data;

public static class CompletionFollowUp
{
    public const string Reason = "Completion follow-up";
    public const string Requested = "completion-review-requested";
    public const string KeptPending = "completion-review-pending";
    public const string Response = "completion-review-response";
    public static string Revision(MarketplaceTask task, IEnumerable<TaskProgressEvent> events)
    {
        var rows = events.Where(e => e.Status != TaskAcceptanceTerms.EventStatus && e.Status != OfferPriceHistory.Event).OrderBy(e => e.Id).ToList();
        var finished = rows.LastOrDefault(e => e.Status == "awaiting-confirmation");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { task.Id, task.Status,
            Submission = finished?.Id, Note = finished?.Note, PhotoType = finished?.PhotoType, LastEvent = rows.LastOrDefault()?.Id }))));
    }
    public static async Task<TaskProgress.Result> RespondAsync(ApplicationDbContext db, int actor, long id, string? note, CancellationToken ct)
    {
        note = note?.Trim();
        if (actor <= 0 || note?.Length is not (>= 1 and <= 1000)) return new(false, "Write a review response of 1–1,000 characters.");
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.Status == "awaiting-confirmation", ct);
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => o.FreelancerId).SingleOrDefaultAsync(ct);
        var events = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id).OrderBy(e => e.Id).ToListAsync(ct);
        if (task is null || actor != task.ClientId && actor != freelancer || !Pending(events)) return new(false, "This completion review is no longer pending.");
        if (events.LastOrDefault(e => e.Status == Response && e.ActorId == actor)?.Note == note) return new(true, "Your review response is already saved.");
        db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = Response, Note = note, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, "Review response saved for both participants and the admin.");
    }
    public static DateTime? AvailableAt(IEnumerable<TaskProgressEvent> events) => events
        .Where(e => e.Status is "awaiting-confirmation" or KeptPending).OrderByDescending(e => e.Id).FirstOrDefault()?.CreatedAtUtc.AddHours(24);
    public static bool Due(MarketplaceTask task, IEnumerable<TaskProgressEvent> events, DateTime now) =>
        task.Status == "awaiting-confirmation" && AvailableAt(events) is { } due && now >= due;
    public static bool Pending(IEnumerable<TaskProgressEvent> events) => events.Where(e => e.Status is Requested or KeptPending or "completed")
        .OrderByDescending(e => e.Id).FirstOrDefault()?.Status == Requested;
    public static async Task<TaskProgress.Result> RequestAsync(ApplicationDbContext db, int actor, long id, long submissionId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id, ct);
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => o.FreelancerId).SingleOrDefaultAsync(ct);
        if (actor <= 0 || freelancer != actor || task is null) return new(false, "Only the assigned freelancer can request completion review.");
        var events = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id).OrderBy(e => e.Id).ToListAsync(ct);
        var finished = events.LastOrDefault(e => e.Status == "awaiting-confirmation");
        if (!Due(task, events, DateTime.UtcNow) || finished?.Id != submissionId)
            return new(false, "Completion review becomes available after 24 hours. Refresh to review the latest submission.");
        if (Pending(events)) return new(true, "Completion review is already with the admin.");
        if (events.LastOrDefault(e => e.Status is TaskProgress.CancellationRequested or TaskProgress.CancellationDeclined)?.Status == TaskProgress.CancellationRequested)
            return new(false, "Resolve the pending cancellation request before requesting completion review.");
        var now = DateTime.UtcNow;
        var report = new SafetyReport { TaskId = id, ReporterId = actor, Target = "Task #" + id, Reason = Reason,
            Description = "Client confirmation is pending for completion submission #" + finished.Id + ". Review the task history, optional completion proof and both participants' explanations.", Priority = 2, CreatedAt = now };
        db.Add(report); await db.SaveChangesAsync(ct);
        db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = Requested, Note = report.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), CreatedAtUtc = now });
        foreach (var user in new[] { task.ClientId, freelancer }.Distinct())
            db.Add(MarketplaceNotification.Create(user, id, "completion_review", "Completion review requested. The job stays pending until a reviewed decision or client confirmation."));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, "Completion review requested. Follow the admin response in Safety & Reports.");
    }
    public static async Task<TaskProgress.Result> ReviewAsync(ApplicationDbContext db, int admin, long reportId, string decision, string? note, string? revision, CancellationToken ct)
    {
        note = note?.Trim();
        if (decision is not ("complete" or "keep") || note?.Length is not (>= 10 and <= 500) ||
            !await db.Users.AnyAsync(u => u.Id == admin && (u.Role == "admin" || u.Role == "moderator"), ct))
            return new(false, "Choose a decision and enter a review reason of 10–500 characters.");
        var taskId = await db.SafetyReports.Where(r => r.Id == reportId && r.Reason == Reason).Select(r => r.TaskId).SingleOrDefaultAsync(ct);
        if (!taskId.HasValue) return new(false, "Completion follow-up not found.");
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, taskId.Value, ct);
        var task = await db.MarketplaceTasks.SingleAsync(t => t.Id == taskId, ct);
        var report = await db.SafetyReports.SingleAsync(r => r.Id == reportId, ct);
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == task.Id && o.Status == "accepted").Select(o => o.FreelancerId).SingleOrDefaultAsync(ct);
        var events = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == task.Id).OrderBy(e => e.Id).ToListAsync(ct);
        if (revision != Revision(task, events)) return new(false, "The work record changed. Reload and review the latest notes and responses before deciding.");
        var request = events.LastOrDefault(e => e.Status == Requested);
        var finished = events.LastOrDefault(e => e.Status == "awaiting-confirmation");
        if (admin == task.ClientId || admin == freelancer || report.Status is not ("open" or "reviewing") || !Due(task, events, DateTime.UtcNow) ||
            !Pending(events) || request?.Note != reportId.ToString(System.Globalization.CultureInfo.InvariantCulture) || finished is null || request.Id <= finished.Id)
            return new(false, "The completion review has changed or is unavailable. Refresh the task history.");
        if (events.LastOrDefault(e => e.Status is TaskProgress.CancellationRequested or TaskProgress.CancellationDeclined)?.Status == TaskProgress.CancellationRequested)
            return new(false, "Resolve the pending cancellation request before deciding completion.");
        var message = decision == "complete" ? "Admin confirmed completion after reviewing the work." : "Admin kept completion pending after review. Read the next steps in Tracking.";
        if (decision == "complete") task.Status = "completed";
        db.Add(new TaskProgressEvent { TaskId = task.Id, ActorId = admin, Status = decision == "complete" ? "completed" : KeptPending, Note = note, CreatedAtUtc = DateTime.UtcNow });
        report.Status = "resolved";
        db.Add(new SafetyReportAction { ReportId = reportId, AdminId = admin, Status = "resolved", Note = note, PublicMessage = message + " " + note, ReviewConfirmed = true, CreatedAt = DateTime.UtcNow });
        foreach (var user in new[] { task.ClientId, freelancer }.Distinct()) db.Add(MarketplaceNotification.Create(user, task.Id, "completion_review", message));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, message);
    }
}
