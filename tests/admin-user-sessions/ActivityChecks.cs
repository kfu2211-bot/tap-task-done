using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using tts.Data;

public static class ActivityChecks
{
    public static async Task Run()
    {
        var commands = new Commands();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=unused;User ID=unused", new MySqlServerVersion(new Version(8,0,36)))
            .AddInterceptors(commands, new NoConnection()).Options);
        var issued = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddHours(-1);
        var expires = issued.AddHours(8);
        var auth = new Authentication(issued, expires);
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var principal = await UserActivity.EnsureTrackedAsync(db, context, "Cookies", default);
        Check(principal?.FindFirstValue(UserActivity.Claim) is { Length: 64 }, "Legacy cookie gains tracking claim");
        Check(auth.SignIns == 1 && auth.LastProperties?.ExpiresUtc == expires, "Migration preserves login expiry");
        Check(commands.Sql.Count == 1 && commands.Sql[0].Contains("ON DUPLICATE KEY UPDATE"), "Legacy tracking insert is idempotent");
        Check(commands.Values.Any(v => v is DateTime t && t == issued.UtcDateTime), "Available sign-in time is retained");
        await UserActivity.EnsureTrackedAsync(db, context, "Cookies", default);
        Check(commands.Sql.Count == 1 && auth.SignIns == 1, "Tracked cookie does not create another session");
        var firstId = principal!.FindFirstValue(UserActivity.Claim);
        var anotherRequest = new Authentication(issued, expires);
        using var otherServices = new ServiceCollection().AddSingleton<IAuthenticationService>(anotherRequest).BuildServiceProvider();
        var other = await UserActivity.EnsureTrackedAsync(db, new DefaultHttpContext { RequestServices = otherServices }, "Cookies", default);
        Check(other!.FindFirstValue(UserActivity.Claim) == firstId, "Simultaneous old-cookie requests use the same session ID");
        var before = commands.Sql.Count;
        Check(await UserActivity.EnsureTrackedAsync(db, context, "AdminCookie", default) is null && commands.Sql.Count == before, "Unauthenticated scheme cannot create tracking rows");
        var now = DateTime.UtcNow;
        Check(UserActivity.Status(new() { LastActiveUtc = now.AddSeconds(-30) }, now) == "Online", "Recent heartbeat is online");
        Check(UserActivity.Status(new() { LastActiveUtc = now.AddMinutes(-5) }, now) == "Inactive", "Five-minute cutoff is inactive");
        Check(UserActivity.Status(new() { LastActiveUtc = now, LogoutUtc = now }, now) == "Logged out", "Logout overrides recent activity");
        Console.WriteLine("PASS: old-cookie tracking, original expiry, stable session IDs, authenticated schemes, presence cutoff and logout. Database commands intercepted; no database connection.");
    }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }
    sealed class Commands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public List<object?> Values { get; } = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Sql.Add(command.CommandText);
            Values.AddRange(command.Parameters.Cast<DbParameter>().Select(p => p.Value));
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }
    sealed class Authentication(DateTimeOffset issued, DateTimeOffset expires) : IAuthenticationService
    {
        readonly ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"123"), new Claim("password_version","version")],"Cookies"));
        public int SignIns;
        public AuthenticationProperties? LastProperties;
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(scheme == "Cookies" ? AuthenticateResult.Success(new AuthenticationTicket(principal,new AuthenticationProperties { IssuedUtc=issued,ExpiresUtc=expires },"Cookies")) : AuthenticateResult.NoResult());
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal user, AuthenticationProperties? properties) { SignIns++; LastProperties=properties; return Task.CompletedTask; }
        public Task SignOutAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
        public Task ChallengeAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
    }
}
