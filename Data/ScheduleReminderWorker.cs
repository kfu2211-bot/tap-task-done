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
                await SendDueAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Schedule reminders will retry."); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public static async Task SendDueAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
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
            var agreement = await db.Set<WorkAgreement>().Where(a => a.TaskId == id && a.ScheduleText == task.ScheduleText &&
                a.ClientConfirmedAtUtc != null && a.FreelancerConfirmedAtUtc != null).OrderByDescending(a => a.Version).FirstOrDefaultAsync(ct);
            if (agreement?.Confirmed != true || TaskSchedule.StartUtc(task.ScheduleText) is not { } start) continue;
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
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}
