using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

public class LandingModel(ApplicationDbContext db, ILogger<LandingModel> logger) : PageModel
{
    public List<CommunityFeedback> Reviews { get; private set; } = [];
    [TempData] public string? FeedbackNotice { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Reviews = await db.Set<CommunityFeedback>().AsNoTracking()
                .OrderByDescending(x => x.Id).Take(6).ToListAsync(ct);
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community feedback could not be loaded.");
        }
    }

    public async Task<IActionResult> OnPostFeedbackAsync(int rating, string? feedback, bool publish, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        feedback = feedback?.Trim();
        if (rating is < 1 or > 5 || feedback is not { Length: > 0 and <= 500 } || !publish ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            FeedbackNotice = "Choose a rating, write up to 500 characters, and confirm public sharing.";
            return RedirectToPage("/Landing", null, null, "community-reviews");
        }

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO community_feedback (UserId, Rating, Message)
                VALUES ({userId}, {rating}, {feedback})
                ON DUPLICATE KEY UPDATE Rating = {rating}, Message = {feedback}
                """, ct);
            FeedbackNotice = "Your feedback is now visible on the landing page.";
        }
        catch (MySqlConnector.MySqlException ex)
        {
            logger.LogWarning(ex, "Community feedback could not be saved.");
            FeedbackNotice = "Feedback could not be saved. Please try again later.";
        }
        return RedirectToPage("/Landing", null, null, "community-reviews");
    }
}
