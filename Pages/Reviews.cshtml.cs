using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tts.Data;

namespace tts.Pages;

[Authorize]
public class ReviewsModel(ApplicationDbContext db) : PageModel
{
    public record ReviewItem(MarketplaceReview Review, string TaskTitle, string OtherName);
    public List<ReviewItem> Items { get; private set; } = [];
    public string View { get; private set; } = "received";
    public bool Legacy { get; private set; }
    public bool Unavailable { get; private set; }
    [TempData] public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(string? view, int? legacy, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == 0 ||
            !await db.Users.AnyAsync(u => u.Id == userId, ct)) return Challenge();
        Legacy = legacy == 1;
        View = view == "given" ? "given" : "received";
        if (Legacy) return Page();
        try
        {
            var query = from r in db.MarketplaceReviews.AsNoTracking()
                        join task in db.MarketplaceTasks.AsNoTracking() on r.TaskId equals task.Id
                        join person in db.Users.AsNoTracking() on (View == "given" ? r.FreelancerId : r.ClientId) equals person.Id
                        where View == "given" ? r.ClientId == userId : r.FreelancerId == userId
                        orderby r.CreatedAt descending, r.Id descending
                        select new { Review = r, task.Title, person.FirstName, person.LastName };
            var rows = await query.ToListAsync(ct);
            Items = rows.Select(x => new ReviewItem(x.Review, x.Title, ($"{x.FirstName} {x.LastName}").Trim())).ToList();
        }
        catch (MySqlException) { Unavailable = true; }
        return Page();
    }
}
