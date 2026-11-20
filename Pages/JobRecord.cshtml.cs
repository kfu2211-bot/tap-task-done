using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class JobRecordModel(ApplicationDbContext db) : PageModel
{
    public MarketplaceTask Work { get; private set; } = null!;
    public bool IsOwner { get; private set; }
    public bool CanBookAgain { get; private set; }
    public MarketplaceOffer? Offer { get; private set; }
    public MarketplaceBooking? Booking { get; private set; }
    public List<TaskProgressEvent> Events { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(long taskId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var viewer = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var task = await db.MarketplaceTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId &&
            (t.ClientId == viewer || db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == viewer) ||
                db.MarketplaceBookings.Any(b => b.TaskId == t.Id && b.FreelancerId == viewer)), ct);
        if (task is null) return NotFound();
        Work = task; IsOwner = task.ClientId == viewer;
        Offer = await db.MarketplaceOffers.AsNoTracking().Where(o => o.TaskId == taskId && (IsOwner ? o.Status == "accepted" : o.FreelancerId == viewer)).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        Booking = await db.MarketplaceBookings.AsNoTracking().Where(b => b.TaskId == taskId && (IsOwner || b.FreelancerId == viewer)).OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);
        CanBookAgain = IsOwner && TaskReuse.CanReuse(task, viewer) && (Offer is not null || Booking is not null);
        // Only the owner/assigned worker sees the accepted work's private progression.
        if (IsOwner || Offer?.Status == "accepted")
            Events = await db.Set<TaskProgressEvent>().AsNoTracking().Where(e => e.TaskId == taskId)
                .OrderBy(e => e.CreatedAtUtc).ThenBy(e => e.Id)
                .Select(e => new TaskProgressEvent { Id = e.Id, TaskId = e.TaskId, Status = e.Status, Note = e.Note, ActorId = e.ActorId, CreatedAtUtc = e.CreatedAtUtc, PhotoType = e.PhotoType }).ToListAsync(ct);
        return Page();
    }
}
