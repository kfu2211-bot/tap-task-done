using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed record AccountVerificationState(int UserId, string Email, bool EmailVerified, DateTime? EmailVerifiedAt,
    long IdentityId, string IdentityStatus, DateTime? IdentityReviewedAt)
{
    public string Revision => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        UserId, Email, EmailVerified, EmailTicks = EmailVerifiedAt?.Ticks, IdentityId, IdentityStatus, IdentityTicks = IdentityReviewedAt?.Ticks
    }))));
    public string EmailLabel => EmailVerified ? "Verified" : "Not verified";
    public string IdentityLabel => IdentityStatus == "approved" ? "Verified" : IdentityStatus == "pending" ? "Pending review" : IdentityStatus == "resubmit" ? "Needs re-verification" : "Not verified";
}

public sealed record AdminVerificationView(int UserId, string Name, AccountVerificationState State, bool CanManage);
public sealed class VerificationChangeException(string message) : InvalidOperationException(message);

public static class AdminAccountVerification
{
    public static bool CanManage(string? actorRole, int actorId, string? targetRole, int targetId) =>
        string.Equals(actorRole, "admin", StringComparison.OrdinalIgnoreCase) && actorId > 0 && targetId > 0 && actorId != targetId &&
        !AccountModerationRules.IsStaff(targetRole) && targetRole != "deleted";
    public static bool ValidInput(string? type, string? change, string? reason, bool confirmed) => confirmed &&
        type is "email" or "identity" && change is "verify" or "unverify" && !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 400;
    public static string ActionLabel(string action) => action switch {
        "email-verify" => "Email verified by admin", "email-unverify" => "Email re-verification required",
        "id-verify" => "Identity verified by admin", "id-unverify" => "Identity re-verification required",
        "warn" => "Warning", "restrict" => "Restricted", "suspend" => "Suspended (legacy)", "offense" => "Confirmed offense", "restore" => "Penalty lifted", _ => "Account updated"
    };
    public static async Task<AccountVerificationState> ReadAsync(ApplicationDbContext db, AppUser user, CancellationToken ct)
    {
        var email = await db.VerifiedEmails.AsNoTracking().SingleOrDefaultAsync(v => v.UserId == user.Id, ct);
        var identity = await db.IdentityRequests.AsNoTracking().Where(r => r.UserId == user.Id).OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Status, r.ReviewedAt }).FirstOrDefaultAsync(ct);
        return new(user.Id, user.Username ?? "", email?.Email == user.Username && email is not null, email?.VerifiedUtc,
            identity?.Id ?? 0, identity?.Status ?? "not verified", identity?.ReviewedAt);
    }
    public static async Task ChangeAsync(ApplicationDbContext db, int actorId, int targetId, string type, string change,
        string? reason, bool confirmed, string? expectedRevision, CancellationToken ct)
    {
        if (!ValidInput(type, change, reason, confirmed)) throw new VerificationChangeException("Choose a verification action, add a reason of up to 400 characters, and confirm the change.");
        await using var tx = await db.Database.BeginTransactionAsync(AppTransactions.Isolation, ct);
        foreach (var id in new[] { actorId, targetId }.Distinct().Order())
            await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actorId, ct);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == targetId, ct);
        if (actor is null || target is null || !CanManage(actor.Role, actorId, target.Role, targetId))
            throw new VerificationChangeException("Only an administrator can change verification for another non-staff account.");
        if (await db.Set<AccountDeletionRequest>().AnyAsync(r => r.UserId == targetId && (r.Status == "verifying" || r.Status == "pending" || r.Status == "held" || r.Status == "approved"), ct))
            throw new VerificationChangeException("Review this account's deletion request before changing verification.");
        var state = await ReadAsync(db, target, ct);
        if (state.Revision != expectedRevision) throw new VerificationChangeException("Verification changed. Reload the account and review its current status.");
        var now = DateTime.UtcNow;
        string before, after, action;
        if (type == "email") {
            if (string.IsNullOrWhiteSpace(target.Username)) throw new VerificationChangeException("This account has no email address to verify.");
            var verify = change == "verify";
            if (state.EmailVerified == verify) throw new VerificationChangeException("The account already has this email verification status.");
            before = state.EmailLabel; after = verify ? "Verified" : "Needs re-verification"; action = verify ? "email-verify" : "email-unverify";
            if (verify) {
                var row = await db.VerifiedEmails.SingleOrDefaultAsync(v => v.UserId == targetId, ct);
                if (row is null) db.VerifiedEmails.Add(new VerifiedEmail { UserId = targetId, Email = target.Username, VerifiedUtc = now });
                else { row.Email = target.Username; row.VerifiedUtc = now; }
            } else {
                await db.VerifiedEmails.Where(v => v.UserId == targetId).ExecuteDeleteAsync(ct);
                await LoginSecurity.RevokeAllAsync(db, targetId, ct);
            }
            // Never issue a first-login exemption or reuse an old email/OTP challenge.
            await db.Set<RegistrationEmailCode>().Where(c => c.UserId == targetId).ExecuteDeleteAsync(ct);
            await db.EmailVerifications.Where(c => c.UserId == targetId).ExecuteDeleteAsync(ct);
        } else {
            var newStatus = change == "verify" ? "approved" : "resubmit";
            if (state.IdentityStatus == newStatus) throw new VerificationChangeException("The account already has this identity verification status.");
            before = state.IdentityLabel; after = change == "verify" ? "Verified" : "Needs re-verification"; action = change == "verify" ? "id-verify" : "id-unverify";
            var note = "Manual admin override (not a document-review result). " + reason!.Trim();
            if (state.IdentityId == 0) db.IdentityRequests.Add(new IdentityRequest { UserId = targetId, Status = newStatus, IdLast4 = "", CreatedAt = now, ReviewedAt = now, ReviewedById = actorId, ReviewNote = note });
            else {
                var updated = await db.IdentityRequests.Where(r => r.Id == state.IdentityId && r.Status == state.IdentityStatus && r.ReviewedAt == state.IdentityReviewedAt)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, newStatus).SetProperty(r => r.ReviewedAt, now).SetProperty(r => r.ReviewedById, (int?)actorId)
                        .SetProperty(r => r.ReviewReadAt, (DateTime?)null).SetProperty(r => r.ReviewNote, note), ct);
                if (updated != 1) throw new VerificationChangeException("Verification changed. Reload the account before trying again.");
            }
        }
        db.AccountModerationActions.Add(new AccountModerationAction { UserId = targetId, AdminId = actorId, Action = action,
            Reason = $"Manual admin override: {before} → {after}. Reason: {reason!.Trim()}", CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
