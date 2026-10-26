using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// Retain the decision/audit metadata, but remove reviewed identity photos and documents after 30 days.
public sealed class IdentityRetentionWorker(IServiceScopeFactory scopes, ILogger<IdentityRetentionWorker> logger, IdentityVerificationAvailability availability) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!availability.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                var expired = db.IdentityRequests.Where(r => r.Status != "pending" && r.ReviewedAt < cutoff);
                await db.IdentityResources.Where(d => d.Kind != "consent" && expired.Any(r => r.Id == d.IdentityRequestId)).ExecuteDeleteAsync(stoppingToken);
                await expired.Where(r => r.GovernmentId != null || r.Certification != null || r.SupportingDocument != null)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.GovernmentId, (byte[]?)null).SetProperty(r => r.Certification, (byte[]?)null).SetProperty(r => r.SupportingDocument, (byte[]?)null), stoppingToken);
                await tx.CommitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Identity document retention will retry ({ErrorType}).", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromHours(6), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
