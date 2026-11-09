using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class RegistrationFlowException(string message) : InvalidOperationException(message);

public sealed class RegistrationEmailCode
{
    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public string PasswordVersion { get; set; } = "";
    public string Email { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public DateTime SessionExpiresUtc { get; set; }
    public DateTime? LastSentUtc { get; set; }
    public DateTime SendWindowUtc { get; set; }
    public int SendCount { get; set; }
    public int Attempts { get; set; }
    public DateTime? VerifiedUtc { get; set; }
    public string? FirstLoginHash { get; set; }
    public DateTime? FirstLoginExpiresUtc { get; set; }
}

public static class RegistrationVerification
{
    public const string CookieName = "tts.registration.verify";
    public const string FirstLoginCookie = "tts.registration.first-login";
    public static bool ValidEmail(string? email) => email is { Length: > 0 and <= 45 } && new EmailAddressAttribute().IsValid(email);
    public static bool ValidCode(string? code) => code is { Length: 6 } && code.All(char.IsAsciiDigit);
    public static bool CanVerify(RegistrationEmailCode row, DateTime now) => row.VerifiedUtc is null && row.Attempts < 5 && row.ExpiresUtc > now && row.SessionExpiresUtc > now;
    public static int Cooldown(RegistrationEmailCode row, DateTime now) => row.LastSentUtc.HasValue ? Math.Max(0, (int)Math.Ceiling((row.LastSentUtc.Value.AddSeconds(60) - now).TotalSeconds)) : 0;
    public static int SendWaitSeconds(RegistrationEmailCode row, DateTime now)
    {
        var windowWait = row.SendCount >= 5
            ? Math.Max(0, (int)Math.Ceiling((row.SendWindowUtc.AddMinutes(15) - now).TotalSeconds))
            : 0;
        return Math.Max(Cooldown(row, now), windowWait);
    }

    public static bool CanSend(RegistrationEmailCode row, DateTime now) => Cooldown(row, now) == 0 && (row.SendWindowUtc <= now.AddMinutes(-15) || row.SendCount < 5);
    public static string? Token(HttpContext context) => LoginSecurity.ValidToken(context.Request.Cookies[CookieName]) ? context.Request.Cookies[CookieName] : null;
    public static async Task<RegistrationEmailCode?> PendingAsync(ApplicationDbContext db, HttpContext context, CancellationToken ct)
    {
        var token = Token(context); if (token is null) return null;
        var hash = LoginSecurity.Hash(token); var now = DateTime.UtcNow;
        return await db.Set<RegistrationEmailCode>().AsNoTracking().Where(x => x.TokenHash == hash && x.SessionExpiresUtc > now && x.VerifiedUtc == null).SingleOrDefaultAsync(ct);
    }
    private static string NewCode() => RandomNumberGenerator.GetInt32(1000000).ToString("D6", CultureInfo.InvariantCulture);
    private static void Prepare(RegistrationEmailCode row, string token, string code, DateTime now)
    {
        if (!CanSend(row, now)) throw new RegistrationFlowException("Wait 60 seconds between codes. A maximum of five sends is allowed per 15 minutes.");
        if (row.SendWindowUtc <= now.AddMinutes(-15)) { row.SendWindowUtc = now; row.SendCount = 0; }
        row.SendCount++; row.LastSentUtc = now; row.ExpiresUtc = now.AddMinutes(10); row.Attempts = 0;
        row.CodeHash = LoginSecurity.CodeHash(token, code);
        row.FirstLoginHash = null; row.FirstLoginExpiresUtc = null;
    }
    private static Task DeliverAsync(IConfiguration config, string email, string code, CancellationToken ct) =>
        SmtpMailer.SendAsync(config, email, "Verify your Tap, Task, Done account", $"Your email verification code is {code}. It expires in 10 minutes.\n\nEnter this code on the Verify email page to finish registration. This does not sign you in automatically. Never share this code. If you did not register, ignore this message.", ct, VerificationEmailTemplate.Render(code));

    // Only called after registration or after the account password has been checked.
    public static async Task StartAsync(ApplicationDbContext db, HttpContext context, AppUser user, IConfiguration config, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var code = NewCode(); var token = LoginSecurity.Token(); string destination;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var current = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={user.Id} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            if (current is null || AccountStore.SessionFingerprint(current.PasswordHash) != AccountStore.SessionFingerprint(user.PasswordHash) || await VerificationMailer.IsVerifiedAsync(db, current, ct))
                throw new RegistrationFlowException("Account changed. Return to login.");
            var row = await db.Set<RegistrationEmailCode>().SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
            var previousToken = Token(context);
            if (row is not null && row.VerifiedUtc is null && row.SessionExpiresUtc > now && previousToken is not null && row.TokenHash == LoginSecurity.Hash(previousToken) && row.PasswordVersion == AccountStore.SessionFingerprint(current.PasswordHash))
                return; // Reuse the pending browser flow; resend is an explicit action.
            row ??= new RegistrationEmailCode { UserId = user.Id, SendWindowUtc = now };
            if (db.Entry(row).State == EntityState.Detached) db.Add(row);
            if (!CanSend(row, now)) throw new RegistrationFlowException("Wait before requesting another verification code.");
            var passwordVersion = AccountStore.SessionFingerprint(current.PasswordHash);
            if (row.VerifiedUtc.HasValue || row.PasswordVersion != passwordVersion || !ValidEmail(row.Email))
                row.Email = current.Username!;
            row.TokenHash = LoginSecurity.Hash(token); row.PasswordVersion = passwordVersion;
            destination = row.Email; row.VerifiedUtc = null; row.SessionExpiresUtc = now.AddHours(24);
            Prepare(row, token, code, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // Keep the pending cookie even on email delivery failure, so resend/change-email remains possible.
        context.Response.Cookies.Append(CookieName, token, LoginSecurity.Cookie(context, TimeSpan.FromHours(24)));
        await DeliverAsync(config, destination, code, ct);
    }
    public static async Task SendAsync(ApplicationDbContext db, HttpContext context, IConfiguration config, string? newEmail, string? password, CancellationToken ct)
    {
        var token = Token(context); var pending = await PendingAsync(db, context, ct);
        if (token is null || pending is null) throw new RegistrationFlowException("Your registration session expired. Return to login.");
        var code = NewCode(); string destination;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={pending.UserId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
            var row = await db.Set<RegistrationEmailCode>().SingleAsync(x => x.UserId == pending.UserId, ct);
            var now = DateTime.UtcNow;
            if (user is null || row.TokenHash != LoginSecurity.Hash(token) || row.VerifiedUtc.HasValue || row.SessionExpiresUtc <= now || row.PasswordVersion != AccountStore.SessionFingerprint(user.PasswordHash) || await VerificationMailer.IsVerifiedAsync(db, user, ct))
                throw new RegistrationFlowException("Your account changed. Return to login.");
            if (newEmail is not null)
            {
                if (!ValidEmail(newEmail) || password is null || password.Length is 0 or > 256 || !context.RequestServices.GetRequiredService<AccountStore>().VerifyPassword(user, password))
                    throw new RegistrationFlowException("Enter a valid email and your registration password.");
                if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Username == newEmail, ct)) throw new RegistrationFlowException("That email cannot be used. Choose another email or log in to the existing account.");
                row.Email = newEmail; // Username changes only after proof of ownership of this address.
            }
            Prepare(row, token, code, now); destination = row.Email;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        await DeliverAsync(config, destination, code, ct);
    }
    public static async Task<bool> ConfirmAsync(ApplicationDbContext db, HttpContext context, string code, CancellationToken ct)
    {
        if (!ValidCode(code)) return false;
        var token = Token(context); var pending = await PendingAsync(db, context, ct);
        if (token is null || pending is null) return false;
        var now = DateTime.UtcNow; var firstToken = LoginSecurity.Token();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id={pending.UserId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        var row = await db.Set<RegistrationEmailCode>().SingleAsync(x => x.UserId == pending.UserId, ct);
        if (user is null || row.TokenHash != LoginSecurity.Hash(token) || row.PasswordVersion != AccountStore.SessionFingerprint(user.PasswordHash) || !CanVerify(row, now) || await VerificationMailer.IsVerifiedAsync(db, user, ct)) return false;
        row.Attempts++;
        if (!LoginSecurity.SameHash(row.CodeHash, LoginSecurity.CodeHash(token, code)))
        { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false; }
        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Username == row.Email, ct)) throw new RegistrationFlowException("That email can no longer be used. Choose another email.");
        user.Username = row.Email;
        var verified = await db.VerifiedEmails.SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
        if (verified is null) db.VerifiedEmails.Add(new() { UserId = user.Id, Email = row.Email, VerifiedUtc = now });
        else { verified.Email = row.Email; verified.VerifiedUtc = now; }
        row.VerifiedUtc = now; row.CodeHash = ""; row.FirstLoginHash = LoginSecurity.Hash(firstToken); row.FirstLoginExpiresUtc = now.AddMinutes(10);
        await db.EmailVerifications.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        context.Response.Cookies.Delete(CookieName);
        context.Response.Cookies.Append(FirstLoginCookie, firstToken, LoginSecurity.Cookie(context, TimeSpan.FromMinutes(10)));
        return true;
    }
    public static async Task<bool> FirstLoginAvailableAsync(ApplicationDbContext db, HttpContext context, AppUser user, CancellationToken ct)
    {
        var token = context.Request.Cookies[FirstLoginCookie]; if (!LoginSecurity.ValidToken(token)) return false;
        var hash = LoginSecurity.Hash(token!); var version = AccountStore.SessionFingerprint(user.PasswordHash); var now = DateTime.UtcNow;
        return await db.Set<RegistrationEmailCode>().AnyAsync(x => x.UserId == user.Id && x.Email == user.Username && x.PasswordVersion == version && x.VerifiedUtc != null && x.FirstLoginHash == hash && x.FirstLoginExpiresUtc > now, ct);
    }
    // Called only while LoginSecurity holds the account lock, inside its sign-in transaction.
    public static async Task<bool> ConsumeFirstLoginAsync(ApplicationDbContext db, HttpContext context, AppUser user, CancellationToken ct)
    {
        if (!await FirstLoginAvailableAsync(db, context, user, ct)) return false;
        var row = await db.Set<RegistrationEmailCode>().SingleAsync(x => x.UserId == user.Id, ct);
        row.FirstLoginHash = null; row.FirstLoginExpiresUtc = null;
        context.Response.Cookies.Delete(FirstLoginCookie);
        return true;
    }
}
