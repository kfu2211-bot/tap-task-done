using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// In-app reminder only. No SMTP, automatic completion or repeated daily reminders.
public sealed class CompletionReminderWorker(IServiceScopeFactory scopes, ILogger<CompletionReminderWorker> logger) : BackgroundService
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
            catch (Exception ex) { logger.LogWarning("Completion reminders will retry ({ErrorType}).", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public static async Task SendDueAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddHours(-24);
        var due = await (from e in db.Set<TaskProgressEvent>().AsNoTracking()
            join t in db.MarketplaceTasks.AsNoTracking() on e.TaskId equals t.Id
            where t.Status == "awaiting-confirmation" && e.Status == "awaiting-confirmation" && e.CreatedAtUtc <= cutoff &&
                !db.MarketplaceNotifications.Any(n => n.TaskId == t.Id && n.Kind == "completion_reminder" && n.CreatedAt >= e.CreatedAtUtc)
            orderby e.Id select new { t.Id, t.ClientId, e.CreatedAtUtc }).Take(100).ToListAsync(ct);
        foreach (var item in due)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {item.ClientId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {item.Id} FOR UPDATE").ToListAsync(ct);
            if (tasks.Count == 1 && tasks[0].Status == "awaiting-confirmation" &&
                !await db.MarketplaceNotifications.AnyAsync(n => n.TaskId == item.Id && n.Kind == "completion_reminder" && n.CreatedAt >= item.CreatedAtUtc, ct))
            {
                db.MarketplaceNotifications.Add(MarketplaceNotification.Create(item.ClientId, item.Id, "completion_reminder",
                    "Please review the finished work and confirm completion in Tracking. The freelancer can already accept other work."));
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
    }
}
