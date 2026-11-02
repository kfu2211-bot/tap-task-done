using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize(AuthenticationSchemes = "Cookies,AdminCookie")]
[EnableRateLimiting("PasswordResetLimit")]
public sealed class AccountDeletionModel(ApplicationDbContext db, AccountStore accounts, IConfiguration config, ILogger<AccountDeletionModel> logger) : PageModel
{
    public AccountDeletionRequest? RequestRow { get; private set; }
    public List<AccountDeletionEvent> History { get; private set; } = [];
    public List<string> Blockers { get; private set; } = [];
    public bool Unavailable { get; private set; }
    public bool IsStaff => User.IsInRole("admin") || User.IsInRole("moderator");
    [TempData] public string? Notice { get; set; }
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            RequestRow = await db.Set<AccountDeletionRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.UserId == UserId, ct);
            History = await db.Set<AccountDeletionEvent>().AsNoTracking().Where(r => r.UserId == UserId).OrderByDescending(r => r.Id).Take(15).ToListAsync(ct);
            Blockers = await tts.Data.AccountDeletion.BlockersAsync(db, UserId, ct);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { Unavailable = true; logger.LogWarning("Deletion request page unavailable ({Type}).", ex.GetType().Name); }
    }
    public async Task<IActionResult> OnPostAsync(string action, string? password, string? code, bool acknowledge, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            if (IsStaff) { Notice = "Staff accounts require a separate administrator process."; return RedirectToPage(); }
            switch (action)
            {
                case "start":
                    if (!acknowledge) { Notice = "Read and acknowledge the deletion conditions first."; break; }
                    await tts.Data.AccountDeletion.BeginAsync(db, accounts, UserId, password ?? "", config, ct);
                    Notice = "A confirmation code was sent to your registered email. Your account is not pending deletion until you verify it."; break;
                case "confirm":
                    if (await tts.Data.AccountDeletion.ConfirmAsync(db, UserId, (code ?? "").Trim(), ct)) { Notice = "Deletion requested. The 72-hour waiting period has started; admin approval is also required."; await NotifyAsync("Deletion request submitted", ct); }
                    else Notice = "Invalid or expired code. Five attempts are allowed. Request a new code using your current password.";
                    break;
                case "cancel": case "review": case "withdraw-offers":
                    await tts.Data.AccountDeletion.UserActionAsync(db, UserId, action, ct);
                    Notice = action == "cancel" ? "Your deletion request was cancelled." : action == "withdraw-offers" ? "Your outgoing pending offers were withdrawn. Accepted work was not changed." : "Your request has been sent back for admin review.";
                    await NotifyAsync(Notice, ct); break;
                default: return BadRequest();
            }
        }
        catch (System.Net.Mail.SmtpException ex) { logger.LogWarning("Deletion email failed ({Reason}).", SmtpMailer.Diagnostic(ex)); Notice = "Email delivery failed. Your status is shown below. Wait 60 seconds before requesting another code."; }
        catch (DeletionFlowException ex) { Notice = ex.Message; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogWarning("Deletion request failed ({Type}).", ex.GetType().Name); Notice = "Deletion storage is unavailable. No deletion was performed. Please try again later."; }
        return RedirectToPage();
    }
    private async Task NotifyAsync(string subject, CancellationToken ct)
    {
        var email = await db.Users.Where(u => u.Id == UserId).Select(u => u.Username).SingleAsync(ct);
        if (email is not null) try { await SmtpMailer.SendAsync(config, email, "Tap, Task, Done — account deletion update", subject + ". Open Settings > Delete account to review the status. Deletion requires 72 hours, admin approval and no unresolved commitments. You can cancel an active request before final deletion.", ct); }
        catch (System.Net.Mail.SmtpException ex) { logger.LogWarning("Deletion status email failed ({Reason}).", SmtpMailer.Diagnostic(ex)); }
    }
}
