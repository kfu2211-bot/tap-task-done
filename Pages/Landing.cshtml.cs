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
}
