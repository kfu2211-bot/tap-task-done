using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class UserActivitySession
{
    public string Id { get; set; } = "";
    public int UserId { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime LastActiveUtc { get; set; }
    public DateTime? LogoutUtc { get; set; }
}

public static class UserActivity
{
    public const string Claim = "activity_session";
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS user_activity_sessions (
          Id varchar(64) NOT NULL PRIMARY KEY, UserId int NOT NULL,
          StartedUtc datetime(6) NOT NULL, LastActiveUtc datetime(6) NOT NULL,
          LogoutUtc datetime(6) NULL, INDEX ix_activity_user_start (UserId, StartedUtc),
          CONSTRAINT fk_activity_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;

    public static string Status(UserActivitySession s, DateTime now) => s.LogoutUtc.HasValue ? "Logged out" : s.LastActiveUtc > now.AddMinutes(-5) ? "Online" : "Inactive";

    public static async Task SigningIn(CookieSigningInContext context)
    {
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        // Reissuing a cookie for profile changes keeps the same activity session.
        if (identity.HasClaim(c => c.Type == Claim)) return;
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        db.Set<UserActivitySession>().Add(new() { Id = id, UserId = userId, StartedUtc = now, LastActiveUtc = now });
        await db.SaveChangesAsync(context.HttpContext.RequestAborted);
        identity.AddClaim(new(Claim, id));
    }

    public static async Task SigningOut(CookieSigningOutContext context)
    {
        var auth = await context.HttpContext.AuthenticateAsync(context.Scheme.Name);
        var id = auth.Principal?.FindFirstValue(Claim);
        if (id is null) return;
        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        await db.Set<UserActivitySession>().Where(s => s.Id == id && s.LogoutUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LogoutUtc, now).SetProperty(x => x.LastActiveUtc, now), context.HttpContext.RequestAborted);
    }

    public static async Task Touch(ApplicationDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var id = principal.FindFirstValue(Claim);
        if (id is null || !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        var now = DateTime.UtcNow;
        await db.Set<UserActivitySession>().Where(s => s.Id == id && s.UserId == userId && s.LogoutUtc == null && s.LastActiveUtc < now.AddSeconds(-45))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActiveUtc, now), ct);
    }
}
