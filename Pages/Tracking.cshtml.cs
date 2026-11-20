using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
[RequestSizeLimit(12 * 1024 * 1024)]
public class TrackingModel(ApplicationDbContext db, ILogger<TrackingModel> logger) : PageModel
{
    public async Task<IActionResult> OnPostFeedbackAsync(long taskId, int rating, string? feedback, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        feedback = feedback?.Trim();
        if (!ModelState.IsValid || rating is < 1 or > 5 || feedback is not { Length: > 0 and <= 500 } ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            Notice = "Choose a rating, write up to 500 characters, before publishing.";
            return RedirectToPage("/Tracking", new { taskId });
        }

        if (!await db.MarketplaceTasks.AnyAsync(task => task.Id == taskId && task.Status == "completed" &&
            db.MarketplaceOffers.Any(offer => offer.TaskId == task.Id && offer.Status == "accepted" &&
                (task.ClientId == userId || offer.FreelancerId == userId)), ct))
            return NotFound();

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO community_feedback (UserId, Rating, Message, ShowDisplayName)
                VALUES ({userId}, {rating}, {feedback}, TRUE)
                ON DUPLICATE KEY UPDATE UserId = UserId
                """, ct);
            Notice = "Your feedback is now visible on the landing page.";
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community feedback could not be saved.");
            Notice = "Feedback could not be saved. Please try again later.";
        }
        return RedirectToPage("/Tracking", new { taskId });
    }

    public async Task<IActionResult> OnPostAdvanceAsync(long id, string? action, string? note, IFormFile? video, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        byte[]? encrypted = null; string? type = null;
        if (video is { Length: > 0 })
        {
            if (action != "finish" || video.Length > CompletionMedia.MaxBytes) { Notice = "Choose an MP4 or WebM completion video up to 10 MB."; return RedirectToPage(new { taskId = id }); }
            using var memory = new MemoryStream();
            await using var stream = video.OpenReadStream();
            await stream.CopyToAsync(memory, ct);
            type = CompletionMedia.VideoType(memory.ToArray());
            if (type is null) { Notice = "Choose a valid MP4 or WebM completion video."; return RedirectToPage(new { taskId = id }); }
            var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            if (!environment.IsDevelopment() && !HttpContext.RequestServices.GetRequiredService<IdentityVerificationAvailability>().Enabled)
            { Notice = "Secure video storage is not configured. Submit a completion note or try again later."; return RedirectToPage(new { taskId = id }); }
            encrypted = PhotoProtector(id).Protect(memory.ToArray());
        }
        try
        {
            var result = await TaskProgress.AdvanceAsync(db, UserId, id, action, note, encrypted, type, ct);
            Notice = result.Message;
            return RedirectToPage(new { taskId = id, shareLocation = result.Success && action == "travel" ? "1" : null });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Progress update failed"); Notice = "Progress storage is unavailable. Please check the database setup."; return RedirectToPage(new { taskId = id }); }
    }

    public async Task<IActionResult> OnPostCancellationAsync(long id, string decision, string? reason, CancellationToken ct)
    {
        try { Notice = (await JobCancellation.DecideAsync(db, UserId, id, decision, reason, ct)).Message; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException)
        { logger.LogWarning(ex, "Cancellation update failed"); Notice = "Cancellation could not be saved. Please try again."; }
        return RedirectToPage(new { taskId = id });
    }

    public async Task<IActionResult> OnPostCompletionNoteAsync(long id, string? note, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid) { Notice = "Check your completion note and try again."; return RedirectToPage(new { taskId = id }); }
        try
        {
        note = note?.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        await AppTransactions.LockTaskAsync(db, id, ct);
        var task = await db.MarketplaceTasks.SingleOrDefaultAsync(t => t.Id == id &&
            t.Status == "awaiting-confirmation" && db.MarketplaceOffers.Any(o => o.TaskId == id &&
                o.FreelancerId == UserId && o.Status == "accepted"), ct);
        var completion = task is null ? null : await db.Set<TaskProgressEvent>()
            .Where(e => e.TaskId == id && e.Status == "awaiting-confirmation").OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        if (completion is null) { Notice = "This completion note can no longer be edited."; return RedirectToPage(new { taskId = id }); }
        if (!TaskProgress.ValidCompletion(note, completion.Photo is not null))
        { Notice = "Keep a completion note or uploaded proof, with a note up to 1,000 characters."; return RedirectToPage(new { taskId = id }); }
        completion.Note = note;
        db.Add(MarketplaceNotification.Create(task!.ClientId, id, "finished", "The freelancer updated the completion note. Review it before confirming completion."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Notice = "Completion note updated.";
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException)
        { logger.LogWarning(ex, "Completion note update failed"); Notice = "Completion note could not be saved. Please try again."; }
        return RedirectToPage(new { taskId = id });
    }

    private Microsoft.AspNetCore.DataProtection.IDataProtector PhotoProtector(long taskId) =>
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("TaskProgress.Photo.v1", taskId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public async Task<IActionResult> OnGetCompletionPhotoAsync(long eventId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (UserId <= 0) return Challenge();
        var item = await db.Set<TaskProgressEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId &&
            db.MarketplaceTasks.Any(t => t.Id == x.TaskId && (t.ClientId == UserId || db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.Status == "accepted" && o.FreelancerId == UserId))), ct);
        if (item?.Photo is null) return NotFound();
        try
        {
            var bytes = PhotoProtector(item.TaskId).Unprotect(item.Photo);
            var type = CompletionMedia.VideoType(bytes) ?? ProfilePhotoModel.ImageType(bytes);
            return type is null ? NotFound() : new FileContentResult(bytes, type) { EnableRangeProcessing = true };
        }
        catch (System.Security.Cryptography.CryptographicException) { return StatusCode(503); }
    }
    [TempData] public string? Notice { get; set; }
    public List<TaskProgressEvent> Progress { get; private set; } = [];
    public Dictionary<long, WorkAgreement> Agreements { get; private set; } = [];
    public Dictionary<long, TaskDestination> Destinations { get; private set; } = [];
    public bool ProgressUnavailable { get; private set; }
    public record Coordinates(long TaskId, double Latitude, double Longitude, double? AccuracyMeters);
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetDestinationAsync(long taskId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        if (!await db.MarketplaceTasks.AnyAsync(t => t.Id == taskId && (t.ClientId == UserId ||
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.Status == "accepted" && o.FreelancerId == UserId)), ct)) return NotFound();
        try
        {
            return new JsonResult(await db.Set<TaskDestination>().AsNoTracking().Where(x => x.TaskId == taskId)
                .Select(x => new { x.Latitude, x.Longitude }).SingleOrDefaultAsync(ct));
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Destination storage unavailable"); return StatusCode(503, new { message = "Destination storage is not ready." }); }
    }

    public IActionResult OnPostDestination([FromBody] Coordinates input)
    {
        if (UserId <= 0) return Challenge();
        return StatusCode(409, new { message = "Change the destination through the final work agreement. Both participants must confirm it." });
    }
    public async Task<IActionResult> OnGetLocationAsync(long taskId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        var assignment = await (from task in db.MarketplaceTasks.AsNoTracking()
            join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
            where task.Id == taskId && offer.Status == "accepted" && (task.ClientId == UserId || offer.FreelancerId == UserId)
            select new { task.Status, offer.FreelancerId }).FirstOrDefaultAsync(ct);
        if (assignment is null) return NotFound();
        try
        {
            var latest = await db.Set<FreelancerLocation>().AsNoTracking()
                .Where(x => x.TaskId == taskId && x.UserId == assignment.FreelancerId)
                .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id)
                .Select(x => new { x.Latitude, x.Longitude, x.AccuracyMeters, x.RecordedAtUtc }).FirstOrDefaultAsync(ct);
            var active = assignment.Status == "on-the-way";
            var canArrive = active && UserId == assignment.FreelancerId &&
                await TaskProgress.NearDestinationAsync(db, taskId, UserId, ct);
            var arrivalRequested = active && await db.Set<TaskProgressEvent>().AnyAsync(x =>
                x.TaskId == taskId && x.ActorId == assignment.FreelancerId && x.Status == TaskProgress.ArrivalRequest, ct);
            return new JsonResult(new { active, status = assignment.Status, location = latest, canArrive, arrivalRequested });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Location storage unavailable"); return StatusCode(503, new { message = "Location storage is not ready. Please contact support." }); }
    }

    public async Task<IActionResult> OnPostLocationAsync([FromBody] Coordinates input, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid || input is null || !double.IsFinite(input.Latitude) || !double.IsFinite(input.Longitude)
            || input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180
            || (input.AccuracyMeters is double accuracy && (!double.IsFinite(accuracy) || accuracy is < 0 or > 100000))) return BadRequest();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var task = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {input.TaskId} FOR UPDATE").ToListAsync(ct);
            if (task.Count != 1 || task[0].Status != "on-the-way" ||
                !await db.MarketplaceOffers.AnyAsync(o => o.TaskId == input.TaskId && o.FreelancerId == UserId && o.Status == "accepted", ct)) return NotFound();
            var now = DateTime.UtcNow;
            if (await db.Set<FreelancerLocation>().AnyAsync(x => x.TaskId == input.TaskId && x.UserId == UserId && x.RecordedAtUtc > now.AddSeconds(-10), ct))
                return StatusCode(429);
            db.Set<FreelancerLocation>().Add(new FreelancerLocation { TaskId = input.TaskId, UserId = UserId,
                Latitude = input.Latitude, Longitude = input.Longitude, AccuracyMeters = input.AccuracyMeters, RecordedAtUtc = now });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new JsonResult(new { saved = true, canArrive = await TaskProgress.NearDestinationAsync(db, input.TaskId, UserId, ct) });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Unable to save freelancer location"); return StatusCode(503, new { message = "Location storage is not ready. Please contact support." }); }
    }
    public Task<bool> HasReviewAsync(long taskId, bool byFreelancer) => db.MarketplaceReviews.AnyAsync(r => r.TaskId == taskId && r.ByFreelancer == byFreelancer, HttpContext.RequestAborted);
    public async Task<bool> CanWriteCommunityReviewAsync()
    {
        try
        {
            return !await db.CommunityFeedback.AnyAsync(review => review.UserId == UserId, HttpContext.RequestAborted);
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community review status could not be loaded.");
            return false;
        }
    }

    public record TrackingItem(MarketplaceTask Task, string Client, string Freelancer, bool IsClient) { public int FreelancerId { get; init; } }
    public List<TrackingItem> Tasks { get; private set; } = [];
    public bool Unavailable { get; private set; }
    public Dictionary<long, string> SharedPhones { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0) return Challenge();
        if (Request.Query["legacy"] == "1") return Page();
        try
        {
            Tasks = await (from task in db.MarketplaceTasks.AsNoTracking()
                join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                join freelancer in db.Users.AsNoTracking() on offer.FreelancerId equals freelancer.Id
                where offer.Status == "accepted" && (task.ClientId == userId || offer.FreelancerId == userId)
                orderby task.Id descending
                select new TrackingItem(task, ((client.FirstName ?? "") + " " + (client.LastName ?? "")).Trim(),
                    ((freelancer.FirstName ?? "") + " " + (freelancer.LastName ?? "")).Trim(), task.ClientId == userId) { FreelancerId = offer.FreelancerId }).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load task tracking"); Unavailable = true;
        }
        if (!Unavailable) {
            try {
                var activeIds = Tasks.Where(x => TaskPhoneShare.IsActive(x.Task.Status)).Select(x => x.Task.Id).ToArray();
                SharedPhones = await (from c in ConversationAccess.VisibleTo(db, userId)
                    join t in db.MarketplaceTasks on c.TaskId equals t.Id
                    join o in db.MarketplaceOffers on new { c.TaskId, c.FreelancerId } equals new { o.TaskId, o.FreelancerId }
                    join s in db.Set<TaskPhoneShare>() on c.Id equals s.ConversationId
                    join contact in db.Set<UserContact>() on s.UserId equals contact.UserId
                    where activeIds.Contains(t.Id) && o.Status == "accepted" && contact.PhoneNumber != ""
                        && s.UserId == (t.ClientId == userId ? c.FreelancerId : t.ClientId)
                    select new { t.Id, contact.PhoneNumber }).ToDictionaryAsync(x => x.Id, x => x.PhoneNumber, ct);
            } catch (MySqlConnector.MySqlException ex) { logger.LogWarning(ex, "Shared contact details unavailable"); }
            try {
                var ids = Tasks.Select(x => x.Task.Id).ToList();
                var terms = await db.Set<WorkAgreement>().AsNoTracking().Where(a => ids.Contains(a.TaskId)).OrderByDescending(a => a.Version).ToListAsync(ct);
                Agreements = terms.GroupBy(a => a.TaskId).ToDictionary(g => g.Key, g => g.First());
                Destinations = await db.Set<TaskDestination>().AsNoTracking().Where(d => ids.Contains(d.TaskId)).ToDictionaryAsync(d => d.TaskId, ct);
                Progress = await db.Set<TaskProgressEvent>().AsNoTracking().Where(x => ids.Contains(x.TaskId))
                    .OrderBy(x => x.Id).Select(x => new TaskProgressEvent { Id = x.Id, TaskId = x.TaskId, Status = x.Status, Note = x.Note, PhotoType = x.PhotoType, CreatedAtUtc = x.CreatedAtUtc }).ToListAsync(ct);
            } catch (MySqlConnector.MySqlException) { ProgressUnavailable = true; }
        }
        return Page();
    }
}
