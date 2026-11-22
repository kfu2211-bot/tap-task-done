using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class ScheduleReminderWorker(IServiceScopeFactory scopes, ILogger<ScheduleReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await SendDueAsync(db, DateTime.UtcNow, stoppingToken, ScheduleReminderEmailWorker.Ready);
                await CancellationFollowUpsAsync(db, DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Schedule reminders will retry."); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public static async Task SendDueAsync(ApplicationDbContext db, DateTime now, CancellationToken ct, bool emailQueueReady = false)
    {
        var ids = await db.MarketplaceTasks.AsNoTracking().Where(t => t.Status == "assigned")
            .Select(t => t.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
            await AppTransactions.LockTaskAsync(db, id, ct);
            var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id && t.Status == "assigned", ct);
            if (task is null) continue;
            var agreement = await AgreementRules.LatestAsync(db, id, ct);
            if (agreement?.Confirmed != true || !TaskSchedule.SameStart(agreement.ScheduleText, task.ScheduleText) || TaskSchedule.StartUtc(task.ScheduleText) is not { } start) continue;
            var cancellation = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id && (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
            if (cancellation?.Status == TaskProgress.CancellationRequested) continue;
            var kind = TaskSchedule.ReminderKind(start, now);
            if (kind is null) continue;
            var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted")
                .Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
            if (freelancer is null) continue;
            var confirmed = agreement.ClientConfirmedAtUtc!.Value > agreement.FreelancerConfirmedAtUtc!.Value
                ? agreement.ClientConfirmedAtUtc.Value : agreement.FreelancerConfirmedAtUtc.Value;
            var message = kind == "schedule_overdue"
                ? $"Schedule overdue: {task.ScheduleText} PHT. No travel update is recorded. Contact each other to continue, reschedule, or request cancellation."
                : $"Upcoming job: {task.ScheduleText} PHT. " + (kind == "schedule_1h" ? "Starts within 1 hour." : "Starts within 24 hours.") + " Review your agreed work details in Tracking.";
            foreach (var user in new[] { task.ClientId, freelancer.Value }.Distinct())
            {
                if (!await db.MarketplaceNotifications.AnyAsync(n => n.TaskId == id && n.UserId == user &&
                    n.Kind == kind && n.CreatedAt >= confirmed, ct))
                    db.Add(MarketplaceNotification.Create(user, id, kind, message));
                if (emailQueueReady && !await db.Set<ScheduleReminderEmail>().AnyAsync(e => e.TaskId == id && e.UserId == user && e.AgreementVersion == agreement.Version && e.Kind == kind, ct))
                    db.Add(new ScheduleReminderEmail { TaskId = id, UserId = user, AgreementVersion = agreement.Version, Kind = kind, CreatedUtc = now, NextAttemptUtc = now });
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }

    public static async Task CancellationFollowUpsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddHours(-24);
        var ids = await db.Set<TaskProgressEvent>().AsNoTracking().Where(e => e.Status == TaskProgress.CancellationRequested && e.CreatedAtUtc <= cutoff)
            .Select(e => e.TaskId).Distinct().ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
            await AppTransactions.LockTaskAsync(db, id, ct);
            var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id, ct);
            if (task is null || !JobCancellation.CanRequest(task.Status)) continue;
            var latest = await db.Set<TaskProgressEvent>().Where(e => e.TaskId == id && (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
            if (!JobCancellation.FollowUpDue(latest, now)) continue;
            var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
            if (!freelancer.HasValue) continue;
            foreach (var user in new[] { task.ClientId, freelancer.Value }.Distinct())
                if (!await db.MarketplaceNotifications.AnyAsync(n => n.TaskId == id && n.UserId == user && n.Kind == "cancellation_followup" && n.CreatedAt >= latest!.CreatedAtUtc, ct))
                    db.Add(MarketplaceNotification.Create(user, id, "cancellation_followup", "No cancellation decision after 24 hours. Open Tracking to respond or ask admin for review. No automatic cancellation or penalty applies."));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
    }
}
