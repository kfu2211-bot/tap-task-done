using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;
[Authorize]
public class AgreementModel(ApplicationDbContext db, IWebHostEnvironment environment, ILogger<AgreementModel> logger) : PageModel
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [TempData] public string? Notice { get; set; }
    public IActionResult OnGet(long taskId) => RedirectToPage("/Tracking", new { taskId });
    private async Task<(MarketplaceTask Task, int Freelancer)?> LockAsync(long id, CancellationToken ct)
    {
        // Called after user rows have been locked by the handlers.
        var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {id} FOR UPDATE").ToListAsync(ct);
        var task = tasks.SingleOrDefault();
        var freelancer = await db.MarketplaceOffers.Where(o => o.TaskId == id && o.Status == "accepted").Select(o => (int?)o.FreelancerId).SingleOrDefaultAsync(ct);
        if (task is null || freelancer is null || UserId != task.ClientId && UserId != freelancer.Value || !AgreementRules.Editable(task.Status)) return null;
        return (task, freelancer.Value);
    }
    private async Task<int[]> ParticipantsAsync(long id, CancellationToken ct)
    {
        var pair = await (from t in db.MarketplaceTasks.AsNoTracking()
            join o in db.MarketplaceOffers.AsNoTracking() on t.Id equals o.TaskId
            where t.Id == id && o.Status == "accepted" && (t.ClientId == UserId || o.FreelancerId == UserId)
            select new { t.ClientId, o.FreelancerId }).SingleOrDefaultAsync(ct);
        return pair is null ? [] : [pair.ClientId, pair.FreelancerId];
    }
    public async Task<IActionResult> OnPostProposeAsync(long taskId, int expectedVersion, decimal price, string? scope, string? schedule, string? location, double? latitude, double? longitude, CancellationToken ct)
    {
        scope = (scope ?? "").Trim(); schedule = (schedule ?? "").Trim(); location = (location ?? "").Trim();
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid || !AgreementRules.Valid(price, scope, schedule, location, latitude, longitude)) { Notice = "Check price, scope, schedule and the confirmed Bacolor-area destination pin."; return RedirectToPage("/Tracking", new { taskId }); }
        try
        {
            var participants = await ParticipantsAsync(taskId, ct); if (participants.Length != 2) return NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await ModerationGuard.AllowedAsync(db, environment, ct, participants)) return Forbid();
            var locked = await LockAsync(taskId, ct); if (locked is null || !participants.Contains(locked.Value.Freelancer)) return NotFound();
            var latest = await AgreementRules.LatestAsync(db, taskId, ct);
            if (!AgreementRules.MatchesVersion(latest, expectedVersion)) { Notice = "The agreement changed. Review the latest version before proposing again."; return RedirectToPage("/Tracking", new { taskId }); }
            var proposal = new WorkAgreement { TaskId = taskId, Version = expectedVersion + 1, ProposedById = UserId, Price = price, Scope = scope, ScheduleText = schedule, Location = location,
                Latitude = latitude!.Value, Longitude = longitude!.Value, CreatedAtUtc = DateTime.UtcNow };
            if (latest is not null && AgreementRules.SameTerms(latest, proposal)) { Notice = "These terms are unchanged. Confirm the existing version instead."; return RedirectToPage("/Tracking", new { taskId }); }
            db.Add(proposal);
            db.Add(MarketplaceNotification.Create(UserId == locked.Value.Task.ClientId ? locked.Value.Freelancer : locked.Value.Task.ClientId, taskId, "agreement", "New work terms proposed. Both participants must confirm the same version in Tracking."));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = "New agreement saved. Both of you must confirm this version. The current destination stays unchanged until then.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning(ex, "Unable to propose work agreement"); Notice = "Could not save the agreement. Check the agreement database setup and try again."; }
        return RedirectToPage("/Tracking", new { taskId });
    }
    public async Task<IActionResult> OnPostConfirmAsync(long taskId, int version, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        try
        {
            var participants = await ParticipantsAsync(taskId, ct); if (participants.Length != 2) return NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await ModerationGuard.AllowedAsync(db, environment, ct, participants)) return Forbid();
            var locked = await LockAsync(taskId, ct); if (locked is null || !participants.Contains(locked.Value.Freelancer)) return NotFound();
            var latest = await AgreementRules.LatestAsync(db, taskId, ct);
            if (latest is null || !AgreementRules.MatchesVersion(latest, version)) { Notice = "The agreement changed. Read and confirm the latest version."; return RedirectToPage("/Tracking", new { taskId }); }
            if (UserId == locked.Value.Task.ClientId ? latest.ClientConfirmedAtUtc.HasValue : latest.FreelancerConfirmedAtUtc.HasValue)
            { Notice = "Your confirmation for this version is already saved."; return RedirectToPage("/Tracking", new { taskId }); }
            if (!AgreementRules.Valid(latest.Price, latest.Scope, latest.ScheduleText, latest.Location, latest.Latitude, latest.Longitude)) return BadRequest();
            if (UserId == locked.Value.Task.ClientId) latest.ClientConfirmedAtUtc ??= DateTime.UtcNow;
            else latest.FreelancerConfirmedAtUtc ??= DateTime.UtcNow;
            if (latest.Confirmed)
            {
                locked.Value.Task.Budget = latest.Price; locked.Value.Task.Description = latest.Scope; locked.Value.Task.ScheduleText = latest.ScheduleText; locked.Value.Task.Location = latest.Location;
                var offer = await db.MarketplaceOffers.SingleAsync(o => o.TaskId == taskId && o.Status == "accepted", ct); offer.Price = latest.Price;
                TaskLocationRules.Save(db, taskId, await db.Set<TaskDestination>().SingleOrDefaultAsync(d => d.TaskId == taskId, ct), latest.Latitude, latest.Longitude);
            }
            db.Add(MarketplaceNotification.Create(UserId == locked.Value.Task.ClientId ? locked.Value.Freelancer : locked.Value.Task.ClientId, taskId, "agreement", latest.Confirmed ? "Both participants confirmed the work agreement. Travel can proceed." : "Your counterpart confirmed the work terms. Review and confirm the same version."));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = latest.Confirmed ? "Agreement confirmed by both. The destination and agreed terms are ready." : "Your confirmation was saved. Waiting for the other participant.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning(ex, "Unable to confirm work agreement"); Notice = "Could not confirm the agreement. Please try again."; }
        return RedirectToPage("/Tracking", new { taskId });
    }
}
