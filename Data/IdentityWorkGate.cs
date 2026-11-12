using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class IdentityWorkGate
{
    public const string Message = "Your identity must be approved before you can post a task, request a booking, or make an offer. Submit your ID and selfie for admin review.";

    public static async Task<bool> IsApprovedAsync(ApplicationDbContext db, int userId, CancellationToken ct)
    {
        if (userId <= 0) return false;
        try
        {
            var status = await db.IdentityRequests.AsNoTracking().Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Id).Select(x => x.Status).FirstOrDefaultAsync(ct);
            return string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);
        }
        catch (MySqlConnector.MySqlException) { return false; }
    }
}
