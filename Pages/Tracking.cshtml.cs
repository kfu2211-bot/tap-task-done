using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class TrackingModel(ApplicationDbContext db, ILogger<TrackingModel> logger) : PageModel
{
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
                where offer.Status == "accepted" && task.Status != "completed" && (task.ClientId == userId || offer.FreelancerId == userId)
                orderby task.Id descending
                select new TrackingItem(task, ((client.FirstName ?? "") + " " + (client.LastName ?? "")).Trim(),
                    ((freelancer.FirstName ?? "") + " " + (freelancer.LastName ?? "")).Trim(), task.ClientId == userId)).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        {
            logger.LogError(ex, "Unable to load task tracking"); Unavailable = true;
        }
        return Page();
    }
}
