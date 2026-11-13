using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class TaskDetailsModel(ApplicationDbContext db) : PageModel
{
    public MarketplaceTask Work { get; private set; } = null!;
    public long ConversationId { get; private set; }
    public bool IsOwner { get; private set; }
    public bool IsAssigned { get; private set; }

    public async Task<IActionResult> OnGetAsync(long taskId, long conversationId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Challenge();

        var result = await (from conversation in ConversationAccess.VisibleTo(db, userId)
            join task in db.MarketplaceTasks.AsNoTracking() on conversation.TaskId equals task.Id
            where conversation.Id == conversationId && task.Id == taskId
            select new
            {
                Task = task,
                Assigned = db.MarketplaceOffers.Any(offer => offer.TaskId == task.Id &&
                    offer.FreelancerId == conversation.FreelancerId && offer.Status == "accepted")
            }).SingleOrDefaultAsync(ct);
        if (result is null) return NotFound();

        Work = result.Task;
        ConversationId = conversationId;
        IsOwner = Work.ClientId == userId;
        IsAssigned = result.Assigned;
        return Page();
    }
}
