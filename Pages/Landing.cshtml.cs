using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

public class LandingModel(ApplicationDbContext db, ILogger<LandingModel> logger) : PageModel
{
    public record PublicReview(int Rating, string Message, string? DisplayName, DateTime PublishedAt);
    public List<PublicReview> Reviews { get; private set; } = [];
    public bool ReviewsUnavailable { get; private set; }
    [TempData] public string? FeedbackNotice { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Reviews = await (from review in db.CommunityFeedback
                join user in db.Users on review.UserId equals user.Id
                where user.Role != "deleted" && review.Rating >= 1 && review.Rating <= 5
                orderby review.PublishedAt descending, review.Id descending
                select new PublicReview(review.Rating, review.Message, review.ShowDisplayName
                    ? ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim()
                    : null, review.PublishedAt)).Take(6).ToListAsync(ct);
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community feedback could not be loaded.");
            ReviewsUnavailable = true;
        }
    }
}
