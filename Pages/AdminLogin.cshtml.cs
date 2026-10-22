using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace tts.Pages;

// Keep old links working without maintaining a separate staff login flow.
public sealed class AdminLoginModel : PageModel
{
    public string? ErrorMessage => null;
    public IActionResult OnGet() => RedirectToPage("/Login");
    public IActionResult OnPost() => RedirectToPage("/Login");
}
