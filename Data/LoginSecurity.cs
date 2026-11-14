using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class LoginChallenge
{
    public string Id { get; set; } = "";
    public int UserId { get; set; }
    public string CodeHash { get; set; } = "";
    public string PasswordVersion { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
}
public sealed class LoginSession
{
    public string Id { get; set; } = "";
    public int UserId { get; set; }
    public string PasswordVersion { get; set; } = "";
    public string Device { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
}
public sealed class TrustedLoginDevice
{
    public string Id { get; set; } = "";
    public int UserId { get; set; }
    public string PasswordVersion { get; set; } = "";
    public string Device { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
}
public sealed class LoginAlert
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string SessionId { get; set; } = "";
    public string Device { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReadUtc { get; set; }
    public bool EmailSent { get; set; }
}

public static class LoginSecurity
{
    public const string ChallengeCookie = "tts.login.challenge";
    public static bool Enabled(IConfiguration config) => config.GetValue<bool>("LoginSecurity:Enabled");
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string CodeHash(string token, string code) => Hash(token + ":" + code);
    public static bool ValidToken(string? token) => token is { Length: 64 } && token.All(Uri.IsHexDigit);
    public static bool SameHash(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    public static string Device(string ua)
    {
        var browser = ua.Contains("Edg/") ? "Edge" : ua.Contains("OPR/") ? "Opera" : ua.Contains("Firefox/") ? "Firefox" : ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome" : ua.Contains("Safari/") ? "Safari" : "Browser";
        var os = ua.Contains("Android") ? "Android" : ua.Contains("iPhone") || ua.Contains("iPad") ? "iPhone / iPad" : ua.Contains("Windows") ? "Windows" : ua.Contains("Macintosh") ? "macOS" : ua.Contains("Linux") ? "Linux" : "Unknown device";
        return browser + " · " + os;
    }
    public static CookieOptions Cookie(HttpContext context, TimeSpan lifetime) => new()
    { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = lifetime, IsEssential = true };
    public static string TrustCookie(int id) => "tts.trusted." + id;
    public static async Task<bool> TrustedAsync(ApplicationDbContext db, HttpContext context, AppUser user, CancellationToken ct)
    {
        var token = context.Request.Cookies[TrustCookie(user.Id)];
        if (!ValidToken(token)) return false;
        var hash = Hash(token!); var version = AccountStore.SessionFingerprint(user.PasswordHash); var now = DateTime.UtcNow;
        return await db.Set<TrustedLoginDevice>().AnyAsync(x => x.Id == hash && x.UserId == user.Id && x.PasswordVersion == version && x.ExpiresUtc > now, ct);
    }
    public static int ResendCooldown(DateTime createdUtc, DateTime now) => Math.Max(0, (int)Math.Ceiling((createdUtc.AddSeconds(60) - now).TotalSeconds));
    public static async Task BeginAsync(ApplicationDbContext db, HttpContext context, AppUser user, IConfiguration config, CancellationToken ct, string? previousToken = null)
    {
        var token = Token(); var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); var now = DateTime.UtcNow;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var current = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={user.Id} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            if (current is null || AccountStore.SessionFingerprint(current.PasswordHash) != AccountStore.SessionFingerprint(user.PasswordHash)) throw new InvalidOperationException("Your account changed. Return to login.");
            if (previousToken is not null)
            {
                if (!ValidToken(previousToken)) throw new InvalidOperationException("Return to login to request a code.");
                var previousHash = Hash(previousToken);
                var previous = await db.Set<LoginChallenge>().SingleOrDefaultAsync(x => x.Id == previousHash, ct);
                if (previous is null || previous.UserId != user.Id || previous.Consumed || previous.ExpiresUtc <= now || previous.PasswordVersion != AccountStore.SessionFingerprint(current.PasswordHash)) throw new InvalidOperationException("Your sign-in request expired. Return to login.");
            }
            var cutoff = now.AddMinutes(-15);
            var latest = await db.Set<LoginChallenge>().Where(x => x.UserId == user.Id).MaxAsync(x => (DateTime?)x.CreatedUtc, ct);
            if (latest.HasValue && ResendCooldown(latest.Value, now) > 0) throw new InvalidOperationException("Wait 60 seconds before requesting another code.");
            if (await db.Set<LoginChallenge>().CountAsync(x => x.UserId == user.Id && x.CreatedUtc > cutoff, ct) >= 5)
                throw new InvalidOperationException("Too many verification requests. Wait 15 minutes before trying again.");
            await db.Set<LoginChallenge>().Where(x => x.UserId == user.Id && !x.Consumed).ExecuteUpdateAsync(s => s.SetProperty(x => x.Consumed, true), ct);
            db.Set<LoginChallenge>().Add(new() { Id = Hash(token), CodeHash = CodeHash(token, code), UserId = user.Id, PasswordVersion = AccountStore.SessionFingerprint(user.PasswordHash), CreatedUtc = now, ExpiresUtc = now.AddMinutes(10) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // Keep the pending challenge on delivery failure so a bounded resend remains possible.
        // This cookie never authenticates the user; the code must still be verified.
        context.Response.Cookies.Append(ChallengeCookie, token, Cookie(context, TimeSpan.FromMinutes(10)));
        await SmtpMailer.SendAsync(config, user.Username!, "Your Tap, Task, Done sign-in code", $"Your sign-in code is {code}. It expires in 10 minutes.\n\nRequested from {Device(context.Request.Headers.UserAgent.ToString())}.\nNever share this code. If this wasn't you, change your password using the website.", ct, VerificationEmailTemplate.SignInCode(code, Device(context.Request.Headers.UserAgent.ToString())));
    }
    public static bool Usable(LoginChallenge challenge, DateTime now) => !challenge.Consumed && challenge.Attempts < 5 && challenge.ExpiresUtc > now;
    public record ChallengeProof(string Token, string Code);
    public static async Task<string> SignInAsync(ApplicationDbContext db, HttpContext context, AppUser user, bool trust, CancellationToken ct, ChallengeProof? proof = null, bool registrationOnly = false)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={user.Id} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        if (current is null || AccountStore.SessionFingerprint(current.PasswordHash) != AccountStore.SessionFingerprint(user.PasswordHash) || current.Username != user.Username || !current.DateOfBirth.HasValue || current.DateOfBirth.Value.Date > DateTime.UtcNow.AddHours(8).Date.AddYears(-16))
            throw new InvalidOperationException("Your account changed. Please log in again.");
        user = current;
        if (registrationOnly && (AccountModerationRules.IsStaff(user.Role) || user.Role == "deleted"))
            throw new InvalidOperationException("This account requires regular sign-in.");
        if (proof is null)
        {
            // Repeat under the account lock so a simultaneous revoke cannot reuse stale trust.
            var registrationProof = VerificationMailer.Enabled(config) && await RegistrationVerification.ConsumeFirstLoginAsync(db, context, user, ct);
            if (!registrationProof && (registrationOnly || !await TrustedAsync(db, context, user, ct))) throw new InvalidOperationException("This browser needs a new sign-in code.");
        }
        else
        {
            var challengeHash = Hash(proof.Token);
            var challenge = (await db.Set<LoginChallenge>().FromSqlInterpolated($"SELECT * FROM login_challenges WHERE Id={challengeHash} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (challenge is null || challenge.UserId != user.Id || !Usable(challenge, DateTime.UtcNow)) throw new InvalidOperationException("Invalid or expired sign-in code.");
            challenge.Attempts++;
            if (challenge.PasswordVersion != AccountStore.SessionFingerprint(user.PasswordHash) || !SameHash(challenge.CodeHash, CodeHash(proof.Token, proof.Code)))
            {
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                throw new InvalidOperationException("Invalid or expired sign-in code.");
            }
            challenge.Consumed = true;
        }
        if ((VerificationMailer.Enabled(config) && !await VerificationMailer.IsVerifiedAsync(db, user, ct)) || ((user.Role ?? "").ToLowerInvariant() is not ("admin" or "moderator") && await ModerationGuard.IsSuspendedAsync(db, user.Id, ct)))
            throw new InvalidOperationException("Your account cannot sign in. Please return to login.");
        var now = DateTime.UtcNow; var version = AccountStore.SessionFingerprint(user.PasswordHash);
        var token = Token(); var id = Hash(token); var device = Device(context.Request.Headers.UserAgent.ToString());
        var role = (user.Role ?? "").Trim().ToLowerInvariant(); var staff = role is "admin" or "moderator";
        var expiry = now.Add(staff ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(8));
        var alert = new LoginAlert { UserId = user.Id, SessionId = id, Device = device, CreatedUtc = now };
        string? trustedToken = trust ? Token() : null;
        db.Set<LoginSession>().Add(new() { Id = id, UserId = user.Id, PasswordVersion = version, Device = device, CreatedUtc = now, LastSeenUtc = now, ExpiresUtc = expiry });
        db.Set<LoginAlert>().Add(alert);
        if (trustedToken is not null) db.Set<TrustedLoginDevice>().Add(new() { Id = Hash(trustedToken), UserId = user.Id, PasswordVersion = version, Device = device, ExpiresUtc = now.AddDays(30) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (trustedToken is not null) context.Response.Cookies.Append(TrustCookie(user.Id), trustedToken, Cookie(context, TimeSpan.FromDays(30)));
        var scheme = staff ? "AdminCookie" : CookieAuthenticationDefaults.AuthenticationScheme;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()), new(ClaimTypes.Email, user.Username ?? ""), new("password_version", version), new("login_session", token) };
        if (staff) claims.Add(new(ClaimTypes.Role, role));
        await context.SignInAsync(scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, scheme)), new AuthenticationProperties { ExpiresUtc = expiry, AllowRefresh = false });
        context.Response.Cookies.Delete(ChallengeCookie);
        try
        {
            await SmtpMailer.SendAsync(context.RequestServices.GetRequiredService<IConfiguration>(), user.Username!, "New sign-in to Tap, Task, Done", $"A sign-in was completed on {device}.\nTime: {DisplayTime.Philippine(now)} (Philippine time).\n\nWas this you? Open Settings → Security on the website to review active sessions. If this wasn't you, choose Secure my account to change your password and sign out other devices.", ct, VerificationEmailTemplate.SignIn(device, DisplayTime.Philippine(now)));
            alert.EmailSent = true; await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or FormatException or InvalidOperationException)
        { context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("LoginSecurity").LogWarning("Sign-in alert email delivery failed ({Type}; {Reason}); the in-app alert remains available.", ex.GetType().Name, SmtpMailer.Diagnostic(ex)); }
        return staff ? "/Admin" : "/Dashboard";
    }
    public static async Task<bool> SessionValidAsync(ApplicationDbContext db, ClaimsPrincipal principal, int userId, CancellationToken ct)
    {
        var token = principal.FindFirstValue("login_session"); if (!ValidToken(token)) return false;
        var hash = Hash(token!); var version = principal.FindFirstValue("password_version"); var now = DateTime.UtcNow;
        var session = await db.Set<LoginSession>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == hash && x.UserId == userId && x.PasswordVersion == version && x.RevokedUtc == null && x.ExpiresUtc > now, ct);
        if (session is null) return false;
        if (session.LastSeenUtc < now.AddMinutes(-1)) await db.Set<LoginSession>().Where(x => x.Id == hash && x.RevokedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenUtc, now), ct);
        return true;
    }
    public static async Task RevokeAllAsync(ApplicationDbContext db, int userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.Set<LoginSession>().Where(x => x.UserId == userId && x.RevokedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, now), ct);
        await db.Set<TrustedLoginDevice>().Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<LoginChallenge>().Where(x => x.UserId == userId && !x.Consumed).ExecuteUpdateAsync(s => s.SetProperty(x => x.Consumed, true), ct);
    }
    public static async Task LogoutAsync(ApplicationDbContext db, HttpContext context, CancellationToken ct)
    {
        foreach (var scheme in new[] { "Cookies", "AdminCookie" })
        {
            var auth = await context.AuthenticateAsync(scheme); var token = auth.Principal?.FindFirstValue("login_session");
            if (!ValidToken(token) || !int.TryParse(auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) continue;
            var hash = Hash(token!);
            await db.Set<LoginSession>().Where(x => x.UserId == userId && x.Id == hash).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedUtc, DateTime.UtcNow), ct);
        }
        await context.SignOutAsync("AdminCookie");
    }
}
