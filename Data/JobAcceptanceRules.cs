using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class JobAcceptanceRules
{
    public const string Message = "Finish your current Long Job or Multi-Day Job and wait for client confirmation before applying for or accepting another job. Approved cancellation also releases this restriction. You can still browse and message clients.";
    public static readonly string[] ExclusiveTypes = ["long", "multi-day"];
    public static readonly string[] StartedStates = ["on-the-way", "arrived", "in-progress", "awaiting-confirmation"];
    public static bool RequiresCompletion(string? type, string status, bool startedBeforePause = false) =>
        (type is "long" or "multi-day") && (StartedStates.Contains(status) || status == "disputed" && startedBeforePause);

    public static IQueryable<MarketplaceTask> BlockingTasks(ApplicationDbContext db, int freelancerId, long? exceptTask = null) =>
        db.MarketplaceTasks.AsNoTracking().Where(task => (!exceptTask.HasValue || task.Id != exceptTask.Value) &&
            ExclusiveTypes.Contains(task.JobType!) &&
            (StartedStates.Contains(task.Status) || task.Status == "disputed" && db.Set<TaskProgressEvent>().Any(e =>
                e.TaskId == task.Id && StartedStates.Contains(e.Status))) &&
            db.MarketplaceOffers.Any(o => o.TaskId == task.Id && o.FreelancerId == freelancerId && o.Status == "accepted"));

    public static Task<bool> IsLockedAsync(ApplicationDbContext db, int freelancerId, CancellationToken ct, long? exceptTask = null) =>
        BlockingTasks(db, freelancerId, exceptTask).AnyAsync(ct);
}

public record JobOfferView(MarketplaceTask Task, string? OfferStatus, bool WorkLocked = false);
