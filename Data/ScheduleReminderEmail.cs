using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// Durable outbox: only task/user/version references, never OAuth credentials or OTPs.
public sealed class ScheduleReminderEmail
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int UserId { get; set; }
    public int AgreementVersion { get; set; }
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class ScheduleReminderEmailWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ScheduleReminderEmailWorker> logger) : BackgroundService
{
    public static bool Ready { get; private set; }
    // Idempotent, additive setup. A database without CREATE permission can use the same SQL script.
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS schedule_reminder_emails (
          Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, TaskId BIGINT NOT NULL, UserId INT NOT NULL,
          AgreementVersion INT NOT NULL, Kind VARCHAR(30) NOT NULL, Status VARCHAR(16) NOT NULL,
          Attempts INT NOT NULL DEFAULT 0, NextAttemptUtc DATETIME(6) NOT NULL, CreatedUtc DATETIME(6) NOT NULL,
          UNIQUE KEY UX_schedule_email (TaskId, UserId, AgreementVersion, Kind),
          INDEX IX_schedule_email_due (Status, NextAttemptUtc),
          CONSTRAINT FK_schedule_email_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id) ON DELETE CASCADE,
          CONSTRAINT FK_schedule_email_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
        ) ENGINE=InnoDB;
        """;

    public static bool StillRelevant(string status, int version, string kind, DateTime start, DateTime now, int latestVersion, bool confirmed) =>
        status == "assigned" && version == latestVersion && confirmed && TaskSchedule.ReminderKind(start, now) == kind;

    public static DateTime RetryAt(int attempts, DateTime now) => now.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(attempts, 6))));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (!Ready)
                {
                    try { await db.Database.ExecuteSqlRawAsync(SchemaSql, stoppingToken); }
                    catch (MySqlConnector.MySqlException)
                    {
                        // An operator may have provisioned the table for a least-privilege application user.
                        await db.Set<ScheduleReminderEmail>().AsNoTracking().Take(1).ToListAsync(stoppingToken);
                    }
                    await EmailPresentationPreference.EnsureAsync(db, stoppingToken);
                    Ready = true;
                }
                await SendPendingAsync(db, config, logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Schedule email reminders will retry ({Type}). In-app reminders remain available.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public static async Task SendPendingAsync(ApplicationDbContext db, IConfiguration config, ILogger logger, CancellationToken ct,
        Func<string, string, string, CancellationToken, Task>? deliver = null)
    {
        var now = DateTime.UtcNow;
        var ids = await db.Set<ScheduleReminderEmail>().AsNoTracking().Where(e => e.Status == "pending" && e.NextAttemptUtc <= now)
            .OrderBy(e => e.Id).Select(e => e.Id).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
            // Consistent lock order with schedule changes; serialize queue dispatch across instances.
            var taskId = await db.Set<ScheduleReminderEmail>().Where(e => e.Id == id).Select(e => e.TaskId).SingleAsync(ct);
            await AppTransactions.LockTaskAsync(db, taskId, ct);
            var email = (await db.Set<ScheduleReminderEmail>().FromSqlInterpolated($"SELECT * FROM schedule_reminder_emails WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (email is null || email.Status != "pending" || email.NextAttemptUtc > DateTime.UtcNow) continue;
            var task = await db.MarketplaceTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == email.TaskId, ct);
            var agreement = await AgreementRules.LatestAsync(db, email.TaskId, ct);
            var recipient = await db.Users.AsNoTracking().Where(u => u.Id == email.UserId && u.Role != "deleted").Select(u => u.Username).SingleOrDefaultAsync(ct);
            var participant = task is not null && (email.UserId == task.ClientId || await db.MarketplaceOffers.AnyAsync(o => o.TaskId == task.Id && o.FreelancerId == email.UserId && o.Status == "accepted", ct));
            var start = TaskSchedule.StartUtc(task?.ScheduleText);
            var cancellation = await db.Set<TaskProgressEvent>().AsNoTracking().Where(e => e.TaskId == email.TaskId &&
                (e.Status == TaskProgress.CancellationRequested || e.Status == TaskProgress.CancellationDeclined)).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
            if (!participant || cancellation?.Status == TaskProgress.CancellationRequested || string.IsNullOrWhiteSpace(recipient) || start is null || agreement is null ||
                !StillRelevant(task!.Status, email.AgreementVersion, email.Kind, start.Value, DateTime.UtcNow, agreement.Version, agreement.Confirmed))
                email.Status = "skipped";
            else
            {
                email.Attempts++;
                try
                {
                    var preference = await db.Set<EmailPresentationPreference>().AsNoTracking().SingleOrDefaultAsync(p => p.UserId == email.UserId, ct);
                    var content = ScheduleReminderTemplate.Create(task.Id, task.Title, start.Value, email.Kind, preference?.Language, preference?.Theme, config);
                    if (deliver is null) await SmtpMailer.SendAsync(config, recipient, content.Subject, content.Text, ct, content.Html);
                    else await deliver(recipient, content.Subject, content.Text, ct);
                    email.Status = "sent";
                }
                catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException or FormatException)
                {
                    email.NextAttemptUtc = RetryAt(email.Attempts, DateTime.UtcNow);
                    if (email.Attempts >= 8) email.Status = "failed";
                    logger.LogWarning("Schedule email delivery failed ({Reason}); queue status {Status}.", SmtpMailer.Diagnostic(ex), email.Status);
                }
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
    }
}
