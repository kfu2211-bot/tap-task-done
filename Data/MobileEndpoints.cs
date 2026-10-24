using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class MobileEndpoints
{
    public record LoginRequest(string? Email, string? Password);
    public record LocationRequest(long TaskId, double Latitude, double Longitude, double? AccuracyMeters);
    private record Session(int UserId, string PasswordVersion);

    public static void MapMobileEndpoints(this WebApplication app)
    {
        var protector = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("TapTaskDone.Mobile.Session.v1").ToTimeLimitedDataProtector();
        app.MapPost("/api/mobile/login", async (LoginRequest input, HttpContext context, ApplicationDbContext db, AccountStore accounts, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps) return Results.BadRequest(new { message = "HTTPS is required." });
            var email = (input.Email ?? "").Trim(); var password = input.Password ?? "";
            if (email.Length is 0 or > 45 || password.Length is 0 or > 256) return Results.Unauthorized();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == email, ct);
            if (user is null || !accounts.VerifyPassword(user, password)) return Results.Unauthorized();
            if (!user.DateOfBirth.HasValue || user.DateOfBirth.Value.Date > DateTime.UtcNow.AddHours(8).Date.AddYears(-16))
                return Results.Json(new { message = "Complete your eligible account profile on the website first." }, statusCode: 403);
            if (VerificationMailer.Enabled(app.Configuration) && !await VerificationMailer.IsVerifiedAsync(db, user, ct))
                return Results.Json(new { message = "Verify your email on the website first." }, statusCode: 403);
            if (await ModerationGuard.IsSuspendedAsync(db, user.Id, ct)) return Results.StatusCode(403);
            var session = System.Text.Json.JsonSerializer.Serialize(new Session(user.Id, AccountStore.SessionFingerprint(user.PasswordHash)));
            return Results.Ok(new { token = protector.Protect(session, TimeSpan.FromHours(8)), name = $"{user.FirstName} {user.LastName}".Trim() });
        }).RequireRateLimiting("AdminLoginLimit");

        app.MapPost("/api/mobile/location", async (LocationRequest input, HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps) return Results.BadRequest(new { message = "HTTPS is required." });
            var authorization = context.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || authorization.Length > 8192) return Results.Unauthorized();
            Session? session;
            try { session = System.Text.Json.JsonSerializer.Deserialize<Session>(protector.Unprotect(authorization[7..].Trim())); }
            catch (Exception ex) when (ex is CryptographicException or FormatException or System.Text.Json.JsonException) { return Results.Unauthorized(); }
            if (session is null) return Results.Unauthorized();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == session.UserId, ct);
            if (user is null || session.PasswordVersion != AccountStore.SessionFingerprint(user.PasswordHash) || !user.DateOfBirth.HasValue || user.DateOfBirth.Value.Date > DateTime.UtcNow.AddHours(8).Date.AddYears(-16)) return Results.Unauthorized();
            if (VerificationMailer.Enabled(app.Configuration) && !await VerificationMailer.IsVerifiedAsync(db, user, ct)) return Results.Unauthorized();
            if (await ModerationGuard.IsSuspendedAsync(db, user.Id, ct)) return Results.StatusCode(403);
            var allowedTask = await db.MarketplaceTasks.AsNoTracking().AnyAsync(t => t.Id == input.TaskId && t.Status == "in-progress" && db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == user.Id && o.Status == "accepted"), ct);
            if (!allowedTask) return Results.Json(new { message = "The task is not in progress or is not assigned to this account." }, statusCode: 403);
            if (!double.IsFinite(input.Latitude) || !double.IsFinite(input.Longitude) || input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180 || (input.AccuracyMeters.HasValue && (!double.IsFinite(input.AccuracyMeters.Value) || input.AccuracyMeters.Value < 0 || input.AccuracyMeters.Value > 100000))) return Results.BadRequest(new { message = "Invalid coordinates." });
            var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO mobile_location_updates (UserId, TaskId, Latitude, Longitude, AccuracyMeters, RecordedAtUtc) VALUES ({user.Id}, {input.TaskId}, {input.Latitude}, {input.Longitude}, {input.AccuracyMeters}, {now})", ct);
            return Results.Ok(new { recordedAtUtc = now });
        }).RequireRateLimiting("MobileLocationLimit");
    }
}
