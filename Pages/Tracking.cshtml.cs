using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
[RequestSizeLimit(6 * 1024 * 1024)]
public class TrackingModel(ApplicationDbContext db, ILogger<TrackingModel> logger) : PageModel
{
    public async Task<IActionResult> OnPostAdvanceAsync(long id, string? action, string? note, IFormFile? photo, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        byte[]? encrypted = null; string? type = null;
        if (photo is { Length: > 0 })
        {
            if (action != "finish" || photo.Length > 5 * 1024 * 1024) { Notice = "Choose a completion photo up to 5 MB."; return RedirectToPage(new { taskId = id }); }
            using var memory = new MemoryStream();
            await using var stream = photo.OpenReadStream();
            await stream.CopyToAsync(memory, ct);
            type = ProfilePhotoModel.ImageType(memory.ToArray());
            if (type is null) { Notice = "Choose a JPG, PNG or WEBP completion photo."; return RedirectToPage(new { taskId = id }); }
            var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            if (!environment.IsDevelopment() && !HttpContext.RequestServices.GetRequiredService<IdentityVerificationAvailability>().Enabled)
            { Notice = "Secure photo storage is not configured. Submit a completion note without a photo."; return RedirectToPage(new { taskId = id }); }
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
            var type = ProfilePhotoModel.ImageType(bytes);
            return type is null ? NotFound() : File(bytes, type);
        }
        catch (System.Security.Cryptography.CryptographicException) { return StatusCode(503); }
    }
    [TempData] public string? Notice { get; set; }
    public List<TaskProgressEvent> Progress { get; private set; } = [];
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

    public async Task<IActionResult> OnPostDestinationAsync([FromBody] Coordinates input, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        if (!ModelState.IsValid || input is null || !double.IsFinite(input.Latitude) || !double.IsFinite(input.Longitude)
            || input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180) return BadRequest();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var tasks = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {input.TaskId} FOR UPDATE").ToListAsync(ct);
            if (tasks.Count != 1 || tasks[0].ClientId != UserId || tasks[0].Status is not ("assigned" or "on-the-way")) return NotFound();
            var destination = await db.Set<TaskDestination>().SingleOrDefaultAsync(x => x.TaskId == input.TaskId, ct);
            if (destination is null) { destination = new TaskDestination { TaskId = input.TaskId }; db.Add(destination); }
            destination.Latitude = input.Latitude;
            destination.Longitude = input.Longitude;
            destination.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new JsonResult(new { destination.Latitude, destination.Longitude });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Unable to save task destination"); return StatusCode(503, new { message = "Destination storage is not ready." }); }
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
            return new JsonResult(new { active = assignment.Status == "on-the-way", status = assignment.Status, location = latest });
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
            return new JsonResult(new { saved = true });
        }
        catch (MySqlConnector.MySqlException ex)
        { logger.LogError(ex, "Unable to save freelancer location"); return StatusCode(503, new { message = "Location storage is not ready. Please contact support." }); }
    }
    public record TrackingItem(MarketplaceTask Task, string Client, string Freelancer, bool IsClient);
    public List<TrackingItem> Tasks { get; private set; } = [];
    public bool Unavailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
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
                    ((freelancer.FirstName ?? "") + " " + (freelancer.LastName ?? "")).Trim(), task.ClientId == userId)).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load task tracking"); Unavailable = true;
        }
        if (!Unavailable) {
            try {
                var ids = Tasks.Select(x => x.Task.Id).ToList();
                Progress = await db.Set<TaskProgressEvent>().AsNoTracking().Where(x => ids.Contains(x.TaskId))
                    .OrderBy(x => x.Id).Select(x => new TaskProgressEvent { Id = x.Id, TaskId = x.TaskId, Status = x.Status, Note = x.Note, PhotoType = x.PhotoType, CreatedAtUtc = x.CreatedAtUtc }).ToListAsync(ct);
            } catch (MySqlConnector.MySqlException) { ProgressUnavailable = true; }
        }
        return Page();
    }
}
