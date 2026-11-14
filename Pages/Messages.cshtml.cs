using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize]
public class MessagesModel(ApplicationDbContext db, ILogger<MessagesModel> logger) : PageModel
{
    public record Conversation(long TaskId, string Title, int ClientId, int FreelancerId, string OtherName)
    { public long Id { get; init; } public int OtherId { get; init; } public bool CanSend { get; init; } public bool Assigned { get; init; } public string Status { get; init; } = ""; }
    public List<Conversation> Conversations { get; private set; } = [];
    public List<Conversation> SelectedTasks { get; private set; } = [];
    public Dictionary<int, int> UnreadCounts { get; private set; } = [];
    public Dictionary<int, MarketplaceMessage> LatestMessages { get; private set; } = [];
    public static string Initials(string? name) => string.Concat((name ?? "User").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])).ToUpperInvariant();
    public string? SharedPhone { get; private set; }
    public bool SharingPhone { get; private set; }
    public bool HasPhone { get; private set; }
    public bool PhoneSharingAvailable { get; private set; }

    public Conversation? Selected { get; private set; }
    public List<MarketplaceMessage> Messages { get; private set; } = [];
    public int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public bool Unavailable { get; private set; }
    [TempData] public string? Notice { get; set; }
    public static bool MaySend(string status, bool assigned, bool pendingBooking, bool isPrivate = false) => status == "open" && !isPrivate || assigned || status == "booking-pending" && pendingBooking;
    public async Task<IActionResult> OnGetAsync(long? taskId, CancellationToken ct, long? conversationId = null, int? freelancerId = null, int? accountId = null)
    {
        Response.Headers.CacheControl = "no-store";
        if (UserId <= 0) return Challenge();
        if (Request.Query["legacy"] == "1") return Page();
        try
        {
            var rows = await (from c in ConversationAccess.VisibleTo(db, UserId).AsNoTracking()
                join t in db.MarketplaceTasks.AsNoTracking() on c.TaskId equals t.Id
                join client in db.Users.AsNoTracking() on t.ClientId equals client.Id
                join freelancer in db.Users.AsNoTracking() on c.FreelancerId equals freelancer.Id
                where t.ClientId == UserId || c.FreelancerId == UserId
                let selected = db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == c.FreelancerId && o.Status == "accepted")
                let pending = db.MarketplaceBookings.Any(b => b.TaskId == t.Id && b.FreelancerId == c.FreelancerId && b.Status == "pending")
                orderby t.CreatedAt descending, c.CreatedAtUtc descending, c.Id descending
                select new { c.Id, c.CreatedAtUtc, TaskId = t.Id, t.Title, t.ClientId, c.FreelancerId, t.Status, t.IsPrivate, Assigned = selected, Pending = pending, ClientName = (client.FirstName ?? "") + " " + (client.LastName ?? ""), FreelancerName = (freelancer.FirstName ?? "") + " " + (freelancer.LastName ?? "") }).ToListAsync(ct);
            var rowsByCreation = rows.ToDictionary(r => r.Id, r => r.CreatedAtUtc);
            var tasks = rows.Select(r => new Conversation(r.TaskId, r.Title, r.ClientId, r.FreelancerId, (r.ClientId == UserId ? r.FreelancerName : r.ClientName).Trim()) { Id = r.Id, OtherId = r.ClientId == UserId ? r.FreelancerId : r.ClientId, Assigned = r.Assigned, Status = r.Status, CanSend = MaySend(r.Status, r.Assigned, r.Pending, r.IsPrivate) }).ToList();
            var ids = tasks.Select(c => c.Id).ToArray();
            var unread = db.MarketplaceNotifications.Where(n => n.UserId == UserId && !n.IsRead && n.Kind == "message" && n.ConversationId.HasValue && ids.Contains(n.ConversationId.Value));
            var groups = tasks.GroupBy(c => c.OtherId).ToDictionary(g => g.Key, g => g.ToList());
            Conversations = groups.Values.Select(g => g[0]).ToList();
            Selected = conversationId.HasValue ? tasks.FirstOrDefault(c => c.Id == conversationId)
                : taskId.HasValue ? tasks.Where(c => c.TaskId == taskId && (!freelancerId.HasValue || c.FreelancerId == freelancerId)).OrderByDescending(c => c.Assigned).FirstOrDefault()
                : accountId.HasValue ? Conversations.FirstOrDefault(c => c.OtherId == accountId) : null;
            if ((conversationId.HasValue || taskId.HasValue || accountId.HasValue) && Selected is null) return NotFound();
            if (Selected is not null)
            {
                SelectedTasks = groups[Selected.OtherId];
                var selectedId = Selected.Id;
                if (Selected.CanSend && TaskPhoneShare.IsActive(Selected.Status))
                {
                    try
                    {
                        var contacts = TaskPhoneShare.SharedContacts(db, selectedId);
                        SharedPhone = await contacts.Where(x => x.UserId == Selected.OtherId).Select(x => x.PhoneNumber).SingleOrDefaultAsync(ct);
                        SharingPhone = await db.Set<TaskPhoneShare>().AnyAsync(x => x.ConversationId == selectedId && x.UserId == UserId, ct);
                        HasPhone = await db.Set<UserContact>().AnyAsync(x => x.UserId == UserId && x.PhoneNumber != "", ct);
                        PhoneSharingAvailable = true;
                    }
                    catch (MySqlConnector.MySqlException ex) { logger.LogWarning(ex, "Task phone sharing unavailable"); }
                }
                var openedNotices = unread.Where(n => n.ConversationId == selectedId);
                var lastNoticeId = await openedNotices.MaxAsync(n => (long?)n.Id, ct);
                Messages = await db.MarketplaceMessages.AsNoTracking().Where(m => m.ConversationId == selectedId).OrderBy(m => m.Id).ToListAsync(ct);
                if (lastNoticeId.HasValue)
                    await openedNotices.Where(n => n.Id <= lastNoticeId.Value).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
            }
            if (ids.Length > 0)
            {
                var latestIds = db.MarketplaceMessages
                    .Where(m => m.ConversationId.HasValue && ids.Contains(m.ConversationId.Value))
                    .GroupBy(m => m.ConversationId)
                    .Select(group => group.Max(m => m.Id));
                var latestByTask = await db.MarketplaceMessages.AsNoTracking()
                    .Where(m => latestIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.ConversationId!.Value, ct);
                var unreadByTask = await unread.GroupBy(n => n.ConversationId!.Value)
                    .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.ConversationId, g => g.Count, ct);
                foreach (var (otherId, accountTasks) in groups)
                {
                    var latest = accountTasks.Select(c => latestByTask.GetValueOrDefault(c.Id))
                        .OfType<MarketplaceMessage>().MaxBy(m => m.Id);
                    if (latest is not null) LatestMessages[otherId] = latest;
                    UnreadCounts[otherId] = accountTasks.Sum(c => unreadByTask.GetValueOrDefault(c.Id));
                }
            }
            Conversations = Conversations.OrderByDescending(c => LatestMessages.TryGetValue(c.OtherId, out var m) && m.CreatedAt > rowsByCreation[c.Id] ? m.CreatedAt : rowsByCreation[c.Id]).ThenByDescending(c => c.Id).ToList();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Unable to load private conversations"); Unavailable = true; }
        return Page();
    }
    public async Task<IActionResult> OnPostPhoneSharingAsync(long conversationId, bool share, CancellationToken ct)
    {
        if (UserId <= 0) return Challenge();
        var context = await (from c in ConversationAccess.VisibleTo(db, UserId)
            join t in db.MarketplaceTasks on c.TaskId equals t.Id
            where c.Id == conversationId
            select new { t.Status, t.IsPrivate,
                Assigned = db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == c.FreelancerId && o.Status == "accepted"),
                Pending = db.MarketplaceBookings.Any(b => b.TaskId == t.Id && b.FreelancerId == c.FreelancerId && b.Status == "pending") })
            .SingleOrDefaultAsync(ct);
        if (context is null) return NotFound();
        if (share && (!TaskPhoneShare.IsActive(context.Status) || !MaySend(context.Status, context.Assigned, context.Pending, context.IsPrivate)))
            return RedirectToPage(new { conversationId });

        try
        {
            if (!share)
                await db.Set<TaskPhoneShare>().Where(x => x.ConversationId == conversationId && x.UserId == UserId).ExecuteDeleteAsync(ct);
            else if (await db.Set<UserContact>().AnyAsync(x => x.UserId == UserId && x.PhoneNumber != "", ct))
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO task_phone_shares (ConversationId, UserId) VALUES ({conversationId}, {UserId}) ON DUPLICATE KEY UPDATE UserId = UserId", ct);
            else
                Notice = "Add a mobile number to your profile before sharing.";
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Unable to update task phone sharing");
            Notice = "Phone sharing could not be updated. Please try again.";
        }
        return RedirectToPage(new { conversationId });
    }

    public async Task<IActionResult> OnPostStartAsync(long taskId, CancellationToken ct, int? freelancerId = null)
    {
        if (UserId <= 0) return Challenge();
        var task = await db.MarketplaceTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return NotFound();
        var provider = task.ClientId == UserId ? freelancerId ?? 0 : UserId;
        if (provider <= 0 || provider == task.ClientId) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        foreach (var id in new[] { task.ClientId, provider }.Distinct().Order()) await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var locked = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {taskId} FOR UPDATE").ToListAsync(ct); task = locked.Single();
        if (await ModerationGuard.IsSuspendedAsync(db, UserId, ct)) return Forbid();
        var existing = await db.Set<TaskConversation>().SingleOrDefaultAsync(c => c.TaskId == taskId && c.FreelancerId == provider, ct);
        if (existing is null)
        {
            bool invited = await db.MarketplaceBookings.AnyAsync(b => b.TaskId == taskId && b.FreelancerId == provider && (b.Status == "pending" || b.Status == "accepted"), ct) || await db.MarketplaceOffers.AnyAsync(o => o.TaskId == taskId && o.FreelancerId == provider && (o.Status == "pending" || o.Status == "accepted"), ct);
            bool publicQuestion = task.ClientId != UserId && task.Status == "open" && !task.IsPrivate && await db.FreelancerProfiles.AnyAsync(p => p.UserId == UserId && (p.WorkRole == "freelancer" || p.WorkRole == "both"), ct);
            if (!invited && !publicQuestion) return NotFound();
            existing = new TaskConversation { TaskId = taskId, FreelancerId = provider, CreatedAtUtc = DateTime.UtcNow }; db.Add(existing); await db.SaveChangesAsync(ct);
            var notice = MarketplaceNotification.Create(task.ClientId == UserId ? provider : task.ClientId, taskId, "message", "A private conversation about your task is ready."); notice.ConversationId = existing.Id; db.Add(notice); await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return RedirectToPage(new { conversationId = existing.Id });
    }
    public async Task<IActionResult> OnPostSendAsync(long taskId, string? body, CancellationToken ct, long? conversationId = null)
    {
        if (UserId <= 0) return Challenge(); body = (body ?? "").Trim();
        if (!conversationId.HasValue) return NotFound();
        if (!ModelState.IsValid || body.Length is < 1 or > 2000) { Notice = "Enter a message of up to 2,000 characters."; return RedirectToPage(new { conversationId }); }
        var thread = await (from c in ConversationAccess.VisibleTo(db, UserId).AsNoTracking() join t in db.MarketplaceTasks.AsNoTracking() on c.TaskId equals t.Id where c.Id == conversationId && t.Id == taskId && (t.ClientId == UserId || c.FreelancerId == UserId) select new { c, t }).SingleOrDefaultAsync(ct);
        if (thread is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        foreach (var id in new[] { thread.t.ClientId, thread.c.FreelancerId }.Distinct().Order()) await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var locked = await db.MarketplaceTasks.FromSqlInterpolated($"SELECT * FROM marketplace_tasks WHERE Id = {taskId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        bool assigned = await db.MarketplaceOffers.AnyAsync(o => o.TaskId == taskId && o.FreelancerId == thread.c.FreelancerId && o.Status == "accepted", ct);
        bool pending = await db.MarketplaceBookings.AnyAsync(b => b.TaskId == taskId && b.FreelancerId == thread.c.FreelancerId && b.Status == "pending", ct);
        if (!MaySend(locked.Single().Status, assigned, pending, locked.Single().IsPrivate) || await ModerationGuard.IsSuspendedAsync(db, UserId, ct)) { Notice = "This conversation is archived or your account is suspended."; return RedirectToPage(new { conversationId }); }
        try
        {
            db.Add(new MarketplaceMessage { TaskId = taskId, ConversationId = conversationId, SenderId = UserId, Body = body, CreatedAt = DateTime.UtcNow });
            var notice = MarketplaceNotification.Create(thread.t.ClientId == UserId ? thread.c.FreelancerId : thread.t.ClientId, taskId, "message", "You have a new private message about your task."); notice.ConversationId = conversationId; db.Add(notice);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or MySqlConnector.MySqlException) { logger.LogWarning(ex, "Unable to send private message"); Notice = "Message could not be sent. Please try again."; }
        return RedirectToPage(new { conversationId });
    }
}
