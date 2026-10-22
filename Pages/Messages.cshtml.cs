using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class MessagesModel(ApplicationDbContext db, ILogger<MessagesModel> logger) : PageModel
{
    public record Conversation(long TaskId, string Title, int ClientId, int FreelancerId, string OtherName);
    public List<Conversation> Conversations { get; private set; } = [];
    public Conversation? Selected { get; private set; }
    public List<MarketplaceMessage> Messages { get; private set; } = [];
    public int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public bool Unavailable { get; private set; }
    [TempData] public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(long? taskId, CancellationToken ct)
    {
        if (UserId <= 0 || !await db.Users.AnyAsync(u => u.Id == UserId, ct)) return Challenge();
        if (Request.Query["legacy"] == "1") return Page();
        try
        {
            var rows = await (from task in db.MarketplaceTasks.AsNoTracking()
                join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
                join client in db.Users.AsNoTracking() on task.ClientId equals client.Id
                join freelancer in db.Users.AsNoTracking() on offer.FreelancerId equals freelancer.Id
                where offer.Status == "accepted" && (task.ClientId == UserId || offer.FreelancerId == UserId)
                orderby task.Id descending
                select new { task.Id, task.Title, task.ClientId, offer.FreelancerId, ClientFirst = client.FirstName, ClientLast = client.LastName, FreelancerFirst = freelancer.FirstName, FreelancerLast = freelancer.LastName }).ToListAsync(ct);
            Conversations = rows.Select(x => new Conversation(x.Id, x.Title, x.ClientId, x.FreelancerId,
                (x.ClientId == UserId ? $"{x.FreelancerFirst} {x.FreelancerLast}" : $"{x.ClientFirst} {x.ClientLast}").Trim())).ToList();
            if (taskId.HasValue && !Conversations.Any(c => c.TaskId == taskId.Value)) return NotFound();
            Selected = taskId.HasValue ? Conversations.FirstOrDefault(c => c.TaskId == taskId.Value) : Conversations.FirstOrDefault();
            if (Selected != null)
                Messages = await db.MarketplaceMessages.AsNoTracking().Where(m => m.TaskId == Selected.TaskId).OrderBy(m => m.Id).ToListAsync(ct);
        }
        catch (MySqlException ex) { logger.LogWarning(ex, "Unable to load messages"); Unavailable = true; }
        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync(long taskId, string? body, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        body = (body ?? "").Trim();
        var recipient = await (from task in db.MarketplaceTasks.AsNoTracking()
            join offer in db.MarketplaceOffers.AsNoTracking() on task.Id equals offer.TaskId
            where task.Id == taskId && offer.Status == "accepted" && (task.ClientId == UserId || offer.FreelancerId == UserId)
            select (int?)(task.ClientId == UserId ? offer.FreelancerId : task.ClientId)).SingleOrDefaultAsync(ct);
        if (recipient is null) return NotFound();
        if (!ModelState.IsValid || body.Length is < 1 or > 2000)
        { Notice = "Enter a message of up to 2,000 characters."; return RedirectToPage(new { taskId }); }
        try
        {
            db.MarketplaceMessages.Add(new MarketplaceMessage { TaskId = taskId, SenderId = UserId, Body = body, CreatedAt = DateTime.UtcNow });
            db.MarketplaceNotifications.Add(MarketplaceNotification.Create(recipient.Value, taskId, "message", "You have a new message about your task."));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or MySqlException)
        { logger.LogWarning(ex, "Unable to send task message"); Notice = "Message could not be sent. Please try again."; }
        return RedirectToPage(new { taskId });
    }
}
