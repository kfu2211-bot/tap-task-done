using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class TaskProgressEvent
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int ActorId { get; set; }
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public byte[]? Photo { get; set; }
    public string? PhotoType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public static class TaskProgress
{
    public static readonly string[] States = ["assigned", "on-the-way", "arrived", "in-progress", "awaiting-confirmation", "completed"];
    public static readonly string[] Labels = ["Booking accepted", "On the way", "Arrived", "Work in progress", "Awaiting client confirmation", "Completed"];
    public static string Label(string status) => Array.IndexOf(States, status) is var i && i >= 0 ? Labels[i] : status == "disputed" ? "Paused" : status == "cancelled" ? "Cancelled" : status;
    public static (string From, string To) Transition(string? action) => action switch
    {
        "travel" => ("assigned", "on-the-way"), "arrive" => ("on-the-way", "arrived"),
        "start" => ("arrived", "in-progress"), "finish" => ("in-progress", "awaiting-confirmation"),
        "confirm" => ("awaiting-confirmation", "completed"), _ => ("", "")
    };
    public record Result(bool Success, string Message);
    public static bool CanAdvance(string status, string? action, bool isClient, bool isAssignedFreelancer, bool hasDestination)
    {
        var (from, _) = Transition(action);
        return from != "" && status == from && (action == "confirm" ? isClient : isAssignedFreelancer)
            && (action != "travel" || hasDestination);
    }

    public static async Task<Result> AdvanceAsync(ApplicationDbContext db, int actor, long id, string? action,
        string? note, byte[]? photo, string? photoType, CancellationToken ct)
    {
        var (from, to) = Transition(action);
        if (actor <= 0 || from == "") return new(false, "This action is unavailable.");
        var participants = await (from t in db.MarketplaceTasks.AsNoTracking()
            join o in db.MarketplaceOffers.AsNoTracking() on t.Id equals o.TaskId
            where t.Id == id && o.Status == "accepted" select new { t.ClientId, o.FreelancerId }).SingleOrDefaultAsync(ct);
        if (participants is null || (actor != participants.ClientId && actor != participants.FreelancerId)) return new(false, "This action is unavailable.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var userId in new[] { participants.ClientId, participants.FreelancerId }.Distinct().Order())
            await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {userId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (await ModerationGuard.IsSuspendedAsync(db, actor, ct)) return new(false, "This account is suspended. Check your suspension notice.");
        var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct);
        var task = tasks.SingleOrDefault();
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
        if (task is null || freelancer is null || !CanAdvance(task.Status, action, task.ClientId == actor, freelancer == actor, true))
            return new(false, "This action is unavailable or the task has already changed. Refresh and check its progress.");
        if (action == "travel" && !await db.Set<TaskDestination>().AnyAsync(x => x.TaskId == id, ct))
            return new(false, "Ask the client to save the exact destination pin before starting travel.");
        if (action is "travel" or "start" && await FreelancerAvailability.ActiveTasks(db, freelancer.Value).AnyAsync(t => t.Id != id, ct))
            return new(false, "Finish your current travel/work before starting another job.");
        note = note?.Trim();
        if (action == "finish" && (string.IsNullOrWhiteSpace(note) || note.Length > 1000))
            return new(false, "Add a completion note (up to 1,000 characters).");
        task.Status = to;
        db.Set<TaskProgressEvent>().Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = to,
            Note = action == "finish" ? note : null, Photo = action == "finish" ? photo : null,
            PhotoType = action == "finish" ? photoType : null, CreatedAtUtc = DateTime.UtcNow });
        var text = action switch
        {
            "travel" => "The freelancer is on the way. Open Tracking to view their shared location.",
            "arrive" => "The freelancer has marked their arrival. Travel location sharing has stopped.",
            "start" => "The freelancer started working on your task.",
            "finish" => "The freelancer marked the work finished. Open Tracking to review the completion note and confirm.",
            _ => "The client confirmed that the task is complete. You can now leave a review."
        };
        db.MarketplaceNotifications.Add(MarketplaceNotification.Create(action == "confirm" ? freelancer.Value : task.ClientId,
            id, action == "confirm" ? "completed" : action == "start" ? "started" : action == "finish" ? "finished" : to, text));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, Label(to) + ".");
    }
}
