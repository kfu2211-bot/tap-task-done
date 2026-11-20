using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class RepeatTaskModel(ApplicationDbContext db) : PageModel
{
    public MarketplaceTask Source { get; private set; } = null!;
    public TaskDestination? Destination { get; private set; }
    public int? FreelancerId { get; private set; }
    public string FreelancerName { get; private set; } = "";
    public bool Booking { get; private set; }
    public async Task<IActionResult> OnGetAsync(long sourceTaskId, bool booking, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var viewer = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var source = await TaskReuse.SourceAsync(db, sourceTaskId, viewer, ct);
        if (source is null) return NotFound();
        var role = await db.FreelancerProfiles.Where(p => p.UserId == viewer).Select(p => p.WorkRole).SingleOrDefaultAsync(ct) ?? "client";
        if (!BookingRequestsModel.CanBook(role)) return Forbid();
        Source = source; Booking = booking;
        Destination = await db.Set<TaskDestination>().AsNoTracking().SingleOrDefaultAsync(d => d.TaskId == sourceTaskId, ct);
        if (booking)
        {
            FreelancerId = await db.MarketplaceOffers.Where(o => o.TaskId == sourceTaskId && o.Status == "accepted")
                .Select(o => (int?)o.FreelancerId).FirstOrDefaultAsync(ct)
                ?? await db.MarketplaceBookings.Where(b => b.TaskId == sourceTaskId).OrderByDescending(b => b.Id)
                    .Select(b => (int?)b.FreelancerId).FirstOrDefaultAsync(ct);
            if (FreelancerId is null) return NotFound();
            FreelancerName = await db.Users.Where(u => u.Id == FreelancerId).Select(u => ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim()).SingleOrDefaultAsync(ct) ?? "Freelancer";
        }
        return Page();
    }
}
