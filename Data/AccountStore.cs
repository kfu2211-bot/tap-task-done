using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

// A database unique constraint is still required to prevent concurrent duplicates.
public sealed class AccountStore(ApplicationDbContext db)
{
    private readonly PasswordHasher<AppUser> hasher = new();

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim();
        return await db.Users.AnyAsync(user => user.Username == normalized, cancellationToken);
    }

    public string HashPassword(AppUser user, string password) => hasher.HashPassword(user, password);

    public void SetPassword(AppUser user, string password) =>
        user.PasswordHash = Encoding.UTF8.GetBytes(HashPassword(user, password));

    public bool VerifyPassword(AppUser user, string password)
    {
        if (user.PasswordHash is null) return false;
        try
        {
            var storedHash = new UTF8Encoding(false, true).GetString(user.PasswordHash);
            return hasher.VerifyHashedPassword(user, storedHash, password) != PasswordVerificationResult.Failed;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
