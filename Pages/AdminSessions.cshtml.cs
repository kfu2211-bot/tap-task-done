using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminSessionsModel(ApplicationDbContext db, ILogger<AdminSessionsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public record Row(UserActivitySession Session, string Name, string Email, string Role);
    public List<Row> Rows { get; private set; } = [];
    public bool HasNext { get; private set; }
    public bool Unavailable { get; private set; }
    public DateTime Now { get; } = DateTime.UtcNow;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        PageNumber = Math.Clamp(PageNumber, 1, 10000);
        if (!ModelState.IsValid) { Unavailable = true; return; }
        try
        {
            var query = from s in db.Set<UserActivitySession>().AsNoTracking()
                        join u in db.Users.AsNoTracking() on s.UserId equals u.Id
                        where u.Role != "deleted"
                        select new { Session = s, Name = (u.FirstName ?? "") + " " + (u.LastName ?? ""), Email = u.Username ?? "", Role = u.Role ?? "client" };
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(x => x.Name.Contains(term) || x.Email.Contains(term));
            }
            if (Date.HasValue)
            {
                var start = Date.Value.Date.AddHours(-8); var end = start.AddDays(1);
                query = query.Where(x => x.Session.StartedUtc >= start && x.Session.StartedUtc < end);
            }
            var data = await query.OrderByDescending(x => x.Session.StartedUtc).ThenBy(x => x.Session.Id)
                .Skip((PageNumber - 1) * 50).Take(51).ToListAsync(ct);
            HasNext = data.Count > 50;
            Rows = data.Take(50).Select(x => new Row(x.Session, x.Name.Trim(), x.Email, x.Role)).ToList();
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
        { logger.LogWarning(ex, "User activity history unavailable"); Unavailable = true; }
    }
}
