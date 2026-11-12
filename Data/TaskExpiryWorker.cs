using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// Only open public posts expire; accepted work and private bookings are untouched.
public sealed class TaskExpiryWorker(IServiceScopeFactory scopes, ILogger<TaskExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await ExpireAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), DateTime.UtcNow, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Task expiration will retry"); }
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public static async Task ExpireAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var ids = await db.MarketplaceTasks.AsNoTracking().Where(t => t.Status == "open" && !t.IsPrivate && t.PostExpiresUtc != null && t.PostExpiresUtc <= now)
            .OrderBy(t => t.Id).Select(t => t.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var task = (await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (task is not null && task.Status == "open" && !task.IsPrivate && task.PostExpiresUtc <= now)
            {
                var recipients = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "pending").Select(o => o.FreelancerId)
                    .Union(db.MarketplaceBookings.Where(b => b.TaskId == id && b.Status == "pending").Select(b => b.FreelancerId)).Distinct().ToListAsync(ct);
                task.Status = "expired";
                await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "cancelled"), ct);
                await db.MarketplaceBookings.Where(b => b.TaskId == id && b.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "unavailable"), ct);
                db.MarketplaceNotifications.Add(MarketplaceNotification.Create(task.ClientId, id, "task_expired", "Your task post expired without an accepted offer."));
                foreach (var recipient in recipients) db.MarketplaceNotifications.Add(MarketplaceNotification.Create(recipient, id, "task_expired", "A task you responded to has expired."));
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
    }
}
