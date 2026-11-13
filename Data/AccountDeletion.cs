using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class DeletionFlowException(string message) : InvalidOperationException(message);

public sealed class AccountDeletionRequest
{
    public int UserId { get; set; }
    public string Status { get; set; } = "verifying";
    public DateTime? RequestedUtc { get; set; }
    public DateTime? EligibleUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int? ReviewedById { get; set; }
    public string? ReviewReason { get; set; }
    public DateTime? DeletedUtc { get; set; }
    public string CodeHash { get; set; } = "";
    public string CodeSalt { get; set; } = "";
    public string PasswordVersion { get; set; } = "";
    public DateTime? CodeExpiresUtc { get; set; }
    public DateTime? LastCodeSentUtc { get; set; }
    public DateTime SendWindowUtc { get; set; }
    public int SendCount { get; set; }
    public int Attempts { get; set; }
}
public sealed class AccountDeletionEvent
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public static class AccountDeletion
{
    public static bool Pending(string status) => status is "pending" or "held" or "approved";
    public static bool Due(AccountDeletionRequest r, DateTime now) => r.Status == "approved" && r.ReviewedById.HasValue && r.RequestedUtc.HasValue && r.EligibleUtc.HasValue && r.EligibleUtc.Value >= r.RequestedUtc.Value.AddHours(72) && now >= r.EligibleUtc.Value;
    public static bool Staff(string? role) => role?.Trim().ToLowerInvariant() is "admin" or "moderator";
    public static bool CodeUsable(AccountDeletionRequest r, DateTime now) => r.Status == "verifying" && r.Attempts < 5 && r.CodeExpiresUtc > now;
    public static int Cooldown(AccountDeletionRequest r, DateTime now) => r.LastCodeSentUtc.HasValue ? LoginSecurity.ResendCooldown(r.LastCodeSentUtc.Value, now) : 0;
    public static bool CanSend(AccountDeletionRequest r, DateTime now) => Cooldown(r, now) == 0 && (r.SendWindowUtc <= now.AddMinutes(-15) || r.SendCount < 5);
    private static void ClearCode(AccountDeletionRequest r) { r.CodeHash = r.CodeSalt = r.PasswordVersion = ""; r.CodeExpiresUtc = null; r.Attempts = 0; }
    private static void Event(ApplicationDbContext db, int user, int? actor, string action, string reason, DateTime now) => db.Set<AccountDeletionEvent>().Add(new() { UserId = user, ActorId = actor, Action = action, Reason = reason, CreatedUtc = now });
    private static async Task<AppUser> LockAsync(ApplicationDbContext db, int id, CancellationToken ct) => (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw new DeletionFlowException("Account unavailable.");
    public static async Task<bool> BlocksNewWorkAsync(ApplicationDbContext db, int id, CancellationToken ct) => await db.Set<AccountDeletionRequest>().AnyAsync(r => r.UserId == id && (r.Status == "pending" || r.Status == "held" || r.Status == "approved" || r.Status == "deleted"), ct);

    public static async Task BeginAsync(ApplicationDbContext db, AccountStore accounts, int id, string password, IConfiguration config, CancellationToken ct)
    {
        if (password.Length is 0 or > 256) throw new DeletionFlowException("Enter your current password.");
        var now = DateTime.UtcNow; var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6"); string email;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var user = await LockAsync(db, id, ct);
            if (Staff(user.Role) || user.Role == "deleted" || !accounts.VerifyPassword(user, password)) throw new DeletionFlowException("Check your password. Staff accounts require a separate administrator process.");
            if (!await db.VerifiedEmails.AnyAsync(x => x.UserId == id && x.Email == user.Username, ct)) throw new DeletionFlowException("Verify your account email before requesting deletion.");
            email = user.Username!;
            var r = await db.Set<AccountDeletionRequest>().SingleOrDefaultAsync(x => x.UserId == id, ct);
            if (r is null) { r = new() { UserId = id }; db.Add(r); }
            if (Pending(r.Status) || r.Status == "deleted") throw new DeletionFlowException("A deletion request is already active.");
            if (!CanSend(r, now)) throw new DeletionFlowException("Wait 60 seconds between codes. Only five sends are allowed per 15 minutes.");
            if (r.SendWindowUtc <= now.AddMinutes(-15)) { r.SendWindowUtc = now; r.SendCount = 0; }
            r.SendCount++; r.LastCodeSentUtc = now; r.Attempts = 0; r.CodeSalt = LoginSecurity.Token(); r.CodeHash = LoginSecurity.CodeHash(r.CodeSalt, code);
            r.PasswordVersion = AccountStore.SessionFingerprint(user.PasswordHash); r.CodeExpiresUtc = now.AddMinutes(10); r.Status = "verifying"; r.UpdatedUtc = now;
            r.RequestedUtc = r.EligibleUtc = r.DeletedUtc = null; r.ReviewedById = null; r.ReviewReason = null;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        await SmtpMailer.SendAsync(config, email, "Confirm your account deletion request", $"Your deletion request verification code is {code}. It expires in 10 minutes. This code only submits a deletion request; deletion requires 72 hours, admin approval and a final commitment check. If this wasn't you, don't enter or share this code and secure your account.", ct);
    }

    public static async Task<bool> ConfirmAsync(ApplicationDbContext db, int id, string code, CancellationToken ct)
    {
        if (!RegistrationVerification.ValidCode(code)) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await LockAsync(db, id, ct); var now = DateTime.UtcNow;
        var r = await db.Set<AccountDeletionRequest>().SingleOrDefaultAsync(x => x.UserId == id, ct);
        if (r is null || !CodeUsable(r, now) || Staff(user.Role) || r.PasswordVersion != AccountStore.SessionFingerprint(user.PasswordHash)) return false;
        r.Attempts++;
        if (!LoginSecurity.SameHash(r.CodeHash, LoginSecurity.CodeHash(r.CodeSalt, code))) { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false; }
        r.Status = "pending"; r.RequestedUtc = now; r.EligibleUtc = now.AddHours(72); r.UpdatedUtc = now; ClearCode(r);
        Event(db, id, id, "requested", "Email ownership and current password confirmed. Waiting period started.", now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }

    public static async Task UserActionAsync(ApplicationDbContext db, int id, string action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await LockAsync(db, id, ct);
        var r = await db.Set<AccountDeletionRequest>().SingleOrDefaultAsync(x => x.UserId == id, ct) ?? throw new DeletionFlowException("No deletion request found.");
        if (action == "cancel" && (Pending(r.Status) || r.Status == "verifying")) r.Status = "cancelled";
        else if (action == "review" && r.Status is "held" or "rejected") { r.Status = "pending"; r.ReviewedById = null; }
        else if (action == "withdraw-offers" && Pending(r.Status))
        {
            var offers = await (from o in db.MarketplaceOffers join t in db.MarketplaceTasks on o.TaskId equals t.Id where o.FreelancerId == id && o.Status == "pending" && t.Status != "completed" && t.Status != "cancelled" select new { Offer = o, t.ClientId }).ToListAsync(ct);
            foreach (var item in offers) { item.Offer.Status = "withdrawn"; db.MarketplaceNotifications.Add(MarketplaceNotification.Create(item.ClientId, item.Offer.TaskId, "withdrawn", "The freelancer withdrew a pending offer while closing their account.")); }
        }
        else throw new DeletionFlowException("This request can no longer be changed.");
        r.UpdatedUtc = DateTime.UtcNow; ClearCode(r); Event(db, id, id, action, action == "cancel" ? "User cancelled the deletion request." : action == "withdraw-offers" ? "User withdrew outgoing pending offers; accepted work was not changed." : "User requested another admin review.", r.UpdatedUtc);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    // Free-text report targets are not reliable identifiers. Admins must also review evidence manually.
    public static async Task<List<string>> BlockersAsync(ApplicationDbContext db, int id, CancellationToken ct)
    {
        var reasons = new List<string>();
        var involved = db.MarketplaceTasks.Where(t => t.ClientId == id || db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == id && o.Status == "accepted"));
        if (await involved.AnyAsync(t => t.Status != "completed" && t.Status != "cancelled", ct)) reasons.Add("Unfinished tasks or jobs. Complete or cancel these with the other participant.");
        if (await db.MarketplaceBookings.AnyAsync(b => b.Status == "pending" && db.MarketplaceTasks.Any(t => t.Id == b.TaskId && t.Status != "completed" && t.Status != "cancelled" && (b.FreelancerId == id || t.ClientId == id)), ct)) reasons.Add("Pending booking requests. Withdraw or decline them first.");
        if (await db.MarketplaceOffers.AnyAsync(o => o.Status == "pending" && db.MarketplaceTasks.Any(t => t.Id == o.TaskId && t.Status != "completed" && t.Status != "cancelled" && (o.FreelancerId == id || t.ClientId == id)), ct)) reasons.Add("Pending offers. Resolve or withdraw them first.");
        if (await OffensePolicy.IsSuspendedAsync(db, id, ct)) reasons.Add("An active suspension requires administrator resolution before deletion.");
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, ct); var name = ($"{user.FirstName} {user.LastName}").Trim(); var refId = "user:" + id;
        if (await db.SafetyReports.AnyAsync(r => (r.Status == "open" || r.Status == "reviewing") && (r.ReporterId == id || r.Target == refId || r.Target == user.Username || r.Target == name || (r.TaskId != null && involved.Any(t => t.Id == r.TaskId))), ct)) reasons.Add("Unresolved safety report involving this account or its work.");
        if (await db.MarketplaceDisputes.AnyAsync(d => d.Status != "resolved" && d.Status != "dismissed" && d.Status != "closed" && (d.OpenedById == id || involved.Any(t => t.Id == d.TaskId)), ct)) reasons.Add("An unresolved legacy case requires admin review.");
        return reasons;
    }

    public static async Task ReviewAsync(ApplicationDbContext db, int adminId, int userId, string decision, string reason, bool evidenceReviewed, CancellationToken ct)
    {
        if (adminId == userId || !evidenceReviewed || decision is not ("approved" or "held" or "rejected") || reason.Trim().Length is < 5 or > 1000) throw new DeletionFlowException("Review the evidence and enter a clear reason (5–1000 characters). You cannot review your own request.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var id in new[] { adminId, userId }.Order()) await LockAsync(db, id, ct);
        var admin = await db.Users.SingleAsync(u => u.Id == adminId, ct);
        if (!Staff(admin.Role)) throw new DeletionFlowException("Administrator access required.");
        var r = await db.Set<AccountDeletionRequest>().SingleAsync(x => x.UserId == userId, ct);
        if (!Pending(r.Status)) throw new DeletionFlowException("This request is not awaiting review.");
        if (decision == "approved" && (await BlockersAsync(db, userId, ct)).Count > 0) throw new DeletionFlowException("Approval is blocked until outstanding commitments and reports are resolved.");
        r.Status = decision; r.ReviewReason = reason.Trim(); r.ReviewedById = adminId; r.UpdatedUtc = DateTime.UtcNow;
        Event(db, userId, adminId, decision, r.ReviewReason, r.UpdatedUtc); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public static async Task<string?> FinalizeAsync(ApplicationDbContext db, int userId, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await LockAsync(db, userId, ct); var r = await db.Set<AccountDeletionRequest>().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (r is null || !Due(r, now)) return null;
        var blockers = await BlockersAsync(db, userId, ct);
        if (!await db.Users.AnyAsync(u => u.Id == r.ReviewedById && (u.Role == "admin" || u.Role == "moderator"), ct)) blockers.Add("The approving administrator's access must be reviewed again.");
        if (Staff(user.Role)) blockers.Add("Staff accounts require a separate administrator process.");
        if (blockers.Count > 0) { r.Status = "held"; r.UpdatedUtc = now; r.ReviewReason = "Final check: " + string.Join(" ", blockers); r.ReviewedById = null; Event(db, userId, null, "held", r.ReviewReason, now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "held"; }
        await RemovePersonalDataAsync(db, user, ct);
        r.Status = "deleted"; r.DeletedUtc = r.UpdatedUtc = now; ClearCode(r); Event(db, userId, null, "deleted", "Personal profile and identity uploads removed; historical work/safety records retained under Deleted user.", now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "deleted";
    }

    public static async Task DeleteUnverifiedAsync(ApplicationDbContext db, int adminId, int userId, CancellationToken ct)
    {
        if (adminId == userId) throw new DeletionFlowException("Choose another account.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var id in new[] { adminId, userId }.Order()) await LockAsync(db, id, ct);
        var admin = await db.Users.SingleAsync(u => u.Id == adminId, ct);
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (admin.Role != "admin" || Staff(user.Role) || user.Role == "deleted" || string.IsNullOrWhiteSpace(user.Username) ||
            await VerificationMailer.IsVerifiedAsync(db, user, ct))
            throw new DeletionFlowException("Only unverified non-staff accounts can be deleted here.");

        var request = await db.Set<AccountDeletionRequest>().SingleOrDefaultAsync(r => r.UserId == userId, ct);
        if (request is not null && Pending(request.Status))
            throw new DeletionFlowException("Use Deletion requests to review the existing request.");
        var reports = db.SafetyReports.Where(r => r.ReporterId == userId);
        await db.SafetyReportEvidence.Where(e => reports.Any(r => r.Id == e.ReportId)).ExecuteDeleteAsync(ct);
        await db.SafetyReportActions.Where(a => reports.Any(r => r.Id == a.ReportId)).ExecuteDeleteAsync(ct);
        await reports.ExecuteDeleteAsync(ct);

        var tasks = db.MarketplaceTasks.Where(t => t.ClientId == userId);
        await db.SafetyReports.Where(r => tasks.Any(t => t.Id == r.TaskId))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.TaskId, (long?)null), ct);
        await db.MarketplaceNotifications.Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.MarketplaceMessages.Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.Set<TaskConversation>().Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.Set<WorkAgreement>().Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.Set<TaskProgressEvent>().Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.Set<TaskDestination>().Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.Set<FreelancerLocation>().Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.MarketplaceBookings.Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.MarketplaceOffers.Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.MarketplaceReviews.Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await db.MarketplaceDisputes.Where(x => tasks.Any(t => t.Id == x.TaskId)).ExecuteDeleteAsync(ct);
        await tasks.ExecuteDeleteAsync(ct);

        await RemovePersonalDataAsync(db, user, ct);
        var now = DateTime.UtcNow;
        if (request is not null)
        {
            request.Status = "deleted";
            request.DeletedUtc = request.UpdatedUtc = now;
            ClearCode(request);
        }
        Event(db, userId, adminId, "deleted", "Administrator removed an account with an unverified email.", now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task RemovePersonalDataAsync(ApplicationDbContext db, AppUser user, CancellationToken ct)
    {
        var userId = user.Id;

        // Keep the surrogate user ID for other participants' transaction and safety history.
        user.FirstName = "Deleted"; user.LastName = "user"; user.Username = null; user.PasswordHash = null; user.DateOfBirth = null; user.ProfilePicture = null; user.Role = "deleted";
        await LoginSecurity.RevokeAllAsync(db, userId, ct);
        await db.Set<LoginAlert>().Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.FreelancerProfiles.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<FreelancerLocation>().Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.IdentityResources.Where(x => db.IdentityRequests.Any(rq => rq.Id == x.IdentityRequestId && rq.UserId == userId)).ExecuteDeleteAsync(ct);
        await db.IdentityRequests.Where(x => x.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(x => x.GovernmentId, (byte[]?)null).SetProperty(x => x.Certification, (byte[]?)null).SetProperty(x => x.SupportingDocument, (byte[]?)null).SetProperty(x => x.IdLast4, "").SetProperty(x => x.GovernmentType, (string?)null).SetProperty(x => x.CertificationType, (string?)null).SetProperty(x => x.SupportingType, (string?)null).SetProperty(x => x.Status, "deleted").SetProperty(x => x.ReviewNote, (string?)null), ct);
        await db.VerifiedEmails.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.EmailVerifications.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.PasswordResetRequests.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.PasswordResetCodes.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<RegistrationEmailCode>().Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
    }
}

public sealed class AccountDeletionWorker(IServiceScopeFactory scopes, ILogger<AccountDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow; var ids = await db.Set<AccountDeletionRequest>().AsNoTracking().Where(r => r.Status == "approved" && r.EligibleUtc <= now).Select(r => r.UserId).Take(50).ToListAsync(ct);
                foreach (var id in ids)
                {
                    db.ChangeTracker.Clear();
                    var email = await db.Users.Where(u => u.Id == id).Select(u => u.Username).SingleOrDefaultAsync(ct);
                    var result = await AccountDeletion.FinalizeAsync(db, id, now, ct);
                    if (result is not null && email is not null)
                    {
                        try { await SmtpMailer.SendAsync(scope.ServiceProvider.GetRequiredService<IConfiguration>(), email, "Tap, Task, Done — account deletion update", result == "deleted" ? "Your account deletion is complete. Personal profile, identity uploads and sign-in access have been removed. Historical work and safety records are retained under Deleted user for accountability." : "Your deletion request was put on hold after a final commitment check. Open Settings > Delete account to see what needs to be resolved. No account deletion was performed.", ct); }
                        catch (System.Net.Mail.SmtpException ex) { logger.LogWarning("Deletion final status email failed ({Reason}); the recorded decision remains valid.", SmtpMailer.Diagnostic(ex)); }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Account deletion worker deferred ({Type}). No approval or final check is bypassed.", ex.GetType().Name); }
            try { if (!await timer.WaitForNextTickAsync(ct)) break; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
