using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class TaskReuse
{
    public const string EventStatus = "task-recreated";
    public static bool CanReuse(MarketplaceTask task, int viewer) => task.ClientId == viewer &&
        task.Status is "expired" or "cancelled" or "canceled" or "completed";
    public static TaskProgressEvent SourceEvent(long newId, long sourceId, int actor) => new() {
        TaskId = newId, ActorId = actor, Status = EventStatus,
        Note = sourceId.ToString(System.Globalization.CultureInfo.InvariantCulture), CreatedAtUtc = DateTime.UtcNow
    };
    public static async Task<MarketplaceTask?> SourceAsync(ApplicationDbContext db, long? id, int viewer, CancellationToken ct) =>
        id.HasValue ? await db.MarketplaceTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.ClientId == viewer &&
            (t.Status == "expired" || t.Status == "cancelled" || t.Status == "canceled" || t.Status == "completed"), ct) : null;
}
