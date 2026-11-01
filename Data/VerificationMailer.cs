using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// Verification is now code-based; ownership checks remain shared with login and recovery.
public static class VerificationMailer
{
    public static bool Enabled(IConfiguration config) => config.GetValue<bool>("EmailVerification:Enabled");
    public static async Task<bool> IsVerifiedAsync(ApplicationDbContext db, AppUser user, CancellationToken ct) =>
        await db.VerifiedEmails.AnyAsync(v => v.UserId == user.Id && v.Email == user.Username, ct);
}
