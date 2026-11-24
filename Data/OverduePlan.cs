using Microsoft.EntityFrameworkCore;
namespace tts.Data;

public static class OverduePlan
{
    public const string Event = "overdue-plan-confirmed";
    public static bool Expired(MarketplaceTask task, DateTime now) => task.Status == "assigned" && task.WorkEndUtc is { } end && now >= end;
    public static bool Required(MarketplaceTask task, DateTime now) => task.Status == "assigned" && TaskSchedule.StartUtc(task.ScheduleText) is { } start && now >= start.AddMinutes(30);
    public static bool Confirmed(IEnumerable<TaskProgressEvent> events, WorkAgreement? agreement, int actor, DateTime now) =>
        actor > 0 && agreement?.Confirmed == true && events.Any(e => e.TaskId == agreement.TaskId && e.Status == Event && e.ActorId == actor &&
            e.Note == agreement.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) && e.CreatedAtUtc >= now.AddHours(-2) && e.CreatedAtUtc <= now);
    public static bool Ready(MarketplaceTask task, WorkAgreement? agreement, IEnumerable<TaskProgressEvent> events, int freelancer, DateTime now) =>
        !Expired(task, now) && (!Required(task, now) || Confirmed(events, agreement, task.ClientId, now) && Confirmed(events, agreement, freelancer, now));
    public static async Task<TaskProgress.Result> ConfirmAsync(ApplicationDbContext db, int actor, long id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id, ct);
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => o.FreelancerId).SingleOrDefaultAsync(ct);
        if (actor <= 0 || task is null || freelancer <= 0 || actor != task.ClientId && actor != freelancer) return new(false, "This action is unavailable.");
        var now = DateTime.UtcNow;
        var agreement = await AgreementRules.LatestAsync(db, id, ct);
        if (!Required(task, now) || Expired(task, now) || agreement?.Confirmed != true || agreement.Version != version)
            return new(false, "The schedule or agreement changed. Review the latest details or request a future schedule.");
        var events = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id && (e.Status == Event || e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).ToListAsync(ct);
        if (events.Where(e => e.Status != Event).OrderByDescending(e => e.Id).FirstOrDefault()?.Status == TaskProgress.CancellationRequested)
            return new(false, "Resolve the cancellation request before confirming the travel plan.");
        if (Confirmed(events, agreement, actor, now)) return new(true, "Your travel plan confirmation is already saved.");
        db.Add(new TaskProgressEvent { TaskId = id, ActorId = actor, Status = Event, Note = version.ToString(System.Globalization.CultureInfo.InvariantCulture), CreatedAtUtc = now });
        db.Add(MarketplaceNotification.Create(actor == task.ClientId ? freelancer : task.ClientId, id, "agreement", "Your counterpart confirmed the overdue travel plan. Review it in Tracking."));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, "Travel plan confirmation saved. Both participants must confirm before travelling.");
    }
}
