using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace tts.Pages
{
    [Authorize]
    public class MyTasksModel : PageModel
    {
        public IActionResult OnGet(long? taskId, long? reviewTask)
        {
            Response.Headers.CacheControl = "no-store";
            return RedirectToPage("/MarketTasks", new { view = "mine", reviewTask = reviewTask ?? taskId });
        }
    }
}
