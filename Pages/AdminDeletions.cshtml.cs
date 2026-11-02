using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("PasswordResetLimit")]
public sealed class AdminDeletionsModel(ApplicationDbContext db, IConfiguration config, ILogger<AdminDeletionsModel> logger) : PageModel
{
    public record Row(AccountDeletionRequest Request, string Name, string Email, List<string> Blockers);
    public List<Row> Requests { get; } = [];
    public List<AccountDeletionEvent> History { get; private set; } = [];
    public bool Unavailable { get; private set; }
    [TempData] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var rows = await (from r in db.Set<AccountDeletionRequest>().AsNoTracking() join u in db.Users.AsNoTracking() on r.UserId equals u.Id where r.RequestedUtc != null orderby r.UpdatedUtc descending select new { Request = r, u.FirstName, u.LastName, u.Username }).Take(50).ToListAsync(ct);
            foreach (var row in rows) Requests.Add(new(row.Request, ($"{row.FirstName} {row.LastName}").Trim(), row.Username ?? "Deleted account", row.Request.Status == "deleted" ? [] : await tts.Data.AccountDeletion.BlockersAsync(db, row.Request.UserId, ct)));
            History = await db.Set<AccountDeletionEvent>().AsNoTracking().OrderByDescending(e => e.Id).Take(100).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { Unavailable = true; logger.LogWarning("Admin deletion review unavailable ({Type}).", ex.GetType().Name); }
    }
    public async Task<IActionResult> OnPostAsync(int userId, string decision, string? reason, bool evidenceReviewed, CancellationToken ct)
    {
        try
        {
            await tts.Data.AccountDeletion.ReviewAsync(db, AdminId, userId, decision, reason ?? "", evidenceReviewed, ct);
            Notice = "Review saved. Approval never bypasses the 72-hour wait or final checks.";
            var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Username).SingleAsync(ct);
            if (email is not null) try { await SmtpMailer.SendAsync(config, email, "Tap, Task, Done — deletion request reviewed", $"Your deletion request status is {decision}. Reason: {reason}\nReview the status in Settings > Delete account. You can cancel an active request before final deletion. Approval does not bypass the 72-hour wait or unresolved commitments.", ct); }
            catch (System.Net.Mail.SmtpException ex) { logger.LogWarning("Deletion review email failed ({Reason}).", SmtpMailer.Diagnostic(ex)); Notice += " Email delivery failed; the decision remains visible in the account's request history."; }
        }
        catch (DeletionFlowException ex) { Notice = ex.Message; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning("Deletion review deferred ({Type}).", ex.GetType().Name); Notice = "Review could not be completed. Refresh the request before trying again."; }
        return RedirectToPage();
    }
}
