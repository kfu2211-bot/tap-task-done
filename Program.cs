using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using tts.Data;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

var builder = WebApplication.CreateBuilder(args);
// Keep sender OAuth credentials in a private JSON settings file, never in tracked source.
builder.Configuration.AddJsonFile("appsettings.Gmail.json", optional: true, reloadOnChange: true);
if (!builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("/etc/secrets/appsettings.Gmail.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHttpClient<PlaceSearch>(client => client.Timeout = TimeSpan.FromSeconds(10));
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}
var lanPreview = string.Equals(Environment.GetEnvironmentVariable("TTS_LAN_PREVIEW"), "true", StringComparison.OrdinalIgnoreCase);
var renderDeployment = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
var assignedPort = Environment.GetEnvironmentVariable("PORT");

// Production verification is opt-in and requires database-backed, encrypted keys.
var verificationEnabled = builder.Environment.IsDevelopment();
if (!verificationEnabled && builder.Configuration.GetValue<bool>("IdentityVerification:Enabled"))
{
    var certificateBase64 = builder.Configuration["IdentityVerification:CertificateBase64"];
    var certificatePassword = builder.Configuration["IdentityVerification:CertificatePassword"];
    var keyConnection = builder.Configuration.GetConnectionString("ApplicationDatabase");
    if (string.IsNullOrWhiteSpace(certificateBase64) || string.IsNullOrWhiteSpace(certificatePassword) || string.IsNullOrWhiteSpace(keyConnection))
        throw new InvalidOperationException("Identity verification requires a database and a password-protected certificate.");
    var certificate = new X509Certificate2(Convert.FromBase64String(certificateBase64), certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
    if (!certificate.HasPrivateKey) throw new InvalidOperationException("Identity verification certificate must include its private key.");
    builder.Services.AddDataProtection().SetApplicationName("TapTaskDone")
        .ProtectKeysWithCertificate(certificate);
    builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = new MySqlKeyRepository(keyConnection));
    verificationEnabled = true;
}
builder.Services.AddSingleton(new IdentityVerificationAvailability(verificationEnabled));

// Hosting services such as Render provide the port at runtime. This keeps the
// local development address unchanged while making the deployed app reachable.
if (!string.IsNullOrWhiteSpace(assignedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{assignedPort}");
}

// LAN preview is intentionally HTTP-only so phones on the same Wi-Fi can test
// the local app without a development certificate or Windows Event Log access.
if (lanPreview)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

// Add services to the container.
builder.Services.AddRazorPages(options => options.Conventions.AuthorizePage("/Admin", "AdminOnly"));
builder.Services.AddScoped<AccountStore>();
builder.Services.AddHostedService<CompletionReminderWorker>();
builder.Services.AddHostedService<TaskExpiryWorker>();
builder.Services.AddHostedService<IdentityRetentionWorker>();
builder.Services.AddHostedService<AccountDeletionWorker>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddAuthentication().AddCookie("BirthdayPending", options =>
{
    options.Cookie.Name = "tts.birthday.pending";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    options.SlidingExpiration = false;
    options.LoginPath = "/Login";
});
builder.Services.AddAuthentication().AddCookie("AdminCookie", options =>
{
    options.Cookie.Name = "tts.admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = async context =>
    {
        var valid = false;
        if (int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            try
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var storedRole = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Role).SingleOrDefaultAsync();
                var currentRole = (storedRole ?? "").Trim().ToLowerInvariant();
                var cookieRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
                valid = (currentRole is "admin" or "moderator") && currentRole == cookieRole;
            }
            catch (Exception ex)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AdminAuthorization").LogWarning(ex, "Admin role validation failed.");
            }
        }
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("AdminCookie");
        }
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
    policy.AddAuthenticationSchemes("AdminCookie").RequireAuthenticatedUser().RequireRole("admin", "moderator")));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PlaceSearchLimit", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AdminLoginLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy("PasswordResetLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    options.AddPolicy("MobileLocationLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ApplicationDatabase");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:ApplicationDatabase must be configured before using the database.");
    var versionText = builder.Configuration["Database:MySqlVersion"];
    // Accept "8", "8.0", "8.0.36" or "8.0.36-log"; missing parts default to 0.
    var versionMatch = System.Text.RegularExpressions.Regex.Match(versionText ?? "", @"^\s*(\d+)(?:\.(\d+))?(?:\.(\d+))?");
    var serverVersion = string.IsNullOrWhiteSpace(versionText)
        ? ServerVersion.AutoDetect(connectionString)
        : versionMatch.Success
            ? new MySqlServerVersion(new Version(
                int.Parse(versionMatch.Groups[1].Value),
                versionMatch.Groups[2].Success ? int.Parse(versionMatch.Groups[2].Value) : 0,
                versionMatch.Groups[3].Success ? int.Parse(versionMatch.Groups[3].Value) : 0))
            : throw new InvalidOperationException("Database:MySqlVersion must contain the MySQL version, for example 8.0.36.");
    options.UseMySql(connectionString, serverVersion);
});

if (renderDeployment)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (renderDeployment)
{
    app.UseForwardedHeaders();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment() && !lanPreview)
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!lanPreview)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var admin = await context.AuthenticateAsync("AdminCookie");
    var principal = admin.Succeeded ? admin.Principal : context.User;
    if (principal?.Identity?.IsAuthenticated == true && int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
    {
        try
        {
            var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            if (LoginSecurity.Enabled(app.Configuration) && !await LoginSecurity.SessionValidAsync(db, principal, id, context.RequestAborted))
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignOutAsync("AdminCookie");
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else context.Response.Redirect("/Login?sessionEnded=1");
                return;
            }
            var account = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new { u.DateOfBirth, u.PasswordHash }).SingleOrDefaultAsync();
            var emailVerified = !VerificationMailer.Enabled(app.Configuration) || await db.VerifiedEmails.AnyAsync(v => v.UserId == id && db.Users.Any(u => u.Id == id && u.Username == v.Email));
            if (!admin.Succeeded && await ModerationGuard.IsSuspendedAsync(db, id, context.RequestAborted))
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignOutAsync("BirthdayPending");
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else context.Response.Redirect("/Login?suspended=1");
                return;
            }
            if (!emailVerified || account is null || !account.DateOfBirth.HasValue || account.DateOfBirth.Value.Date > DateTime.UtcNow.AddHours(8).Date.AddYears(-16) ||
                principal.FindFirstValue("password_version") != AccountStore.SessionFingerprint(account.PasswordHash))
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignOutAsync("AdminCookie");
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else context.Response.Redirect("/Login");
                return;
            }
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BirthdayCheck").LogError(ex, "Birthday check failed.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
    }
    await next();
});
app.UseAuthorization();

app.MapGet("/api/providers/availability", async (HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var profiles = await db.FreelancerProfiles.AsNoTracking().Where(p => p.WorkRole == "freelancer" || p.WorkRole == "both")
        .Select(p => new { p.UserId, p.Availability }).ToListAsync(ct);
    var active = await FreelancerAvailability.AssignmentsAsync(db, profiles.Select(p => p.UserId).ToArray(), ct);
    return Results.Json(profiles.Select(p => new { id = p.UserId, availability = FreelancerAvailability.Effective(p.Availability, active.GetValueOrDefault(p.UserId) ?? []) }));
}).RequireAuthorization();

app.MapGet("/api/work-preferences", async (HttpContext context, ApplicationDbContext db) =>
{
    if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        return Results.Unauthorized();
    var profile = await db.FreelancerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);
    var effective = await FreelancerAvailability.GetAsync(db, userId, profile?.Availability ?? "Available", context.RequestAborted);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Json(new { role = profile?.WorkRole ?? "client", service = profile?.Service ?? "", availability = effective, preferredAvailability = profile?.Availability ?? "Available" });
}).RequireAuthorization();

app.MapGet("/api/account-warning", async (HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0) return Results.Unauthorized();
    return Results.Json(await OffensePolicy.GetAsync(db, userId, ct));
}).RequireAuthorization();
app.MapGet("/api/profile-warnings", async (HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var ids = context.Request.Query["ids"].ToString().Split(',').Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id > 0).Distinct().Take(20).ToArray();
    var result = new List<object>();
    foreach (var id in ids)
    {
        var status = await OffensePolicy.GetAsync(db, id, ct);
        result.Add(new { id, warning = status.Warning, unavailable = status.Suspended });
    }
    return Results.Json(result);
}).RequireAuthorization();

app.MapGet("/api/notifications/unread-count", async (HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
{
    if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
        return Results.Unauthorized();
    var messageCount = await db.MarketplaceNotifications.CountAsync(n => n.UserId == userId && !n.IsRead && n.Kind == "message", ct);
    var taskKinds = new[] { "offer", "accepted", "booking_request", "booking_declined", "booking_accepted", "cancelled", "withdrawn", "agreement", "completion_reminder", "task_expired", "assigned", "on-the-way", "arrived", "in-progress", "awaiting-confirmation", "started", "finished", "completed" };
    var taskCount = await db.MarketplaceNotifications.CountAsync(n => n.UserId == userId && !n.IsRead && taskKinds.Contains(n.Kind), ct);
    var count = await db.MarketplaceNotifications.CountAsync(n => n.UserId == userId && !n.IsRead && n.Kind != "dispute", ct);
    count += await db.IdentityRequests.CountAsync(r => r.UserId == userId && r.ReviewedAt != null && r.ReviewReadAt == null, ct);
    count += await db.AccountModerationActions.CountAsync(a => a.UserId == userId && a.ReadAt == null, ct);
    if (LoginSecurity.Enabled(app.Configuration)) count += await db.Set<LoginAlert>().CountAsync(a => a.UserId == userId && a.ReadUtc == null, ct);
    return Results.Json(new { count, messageCount, taskCount, otherCount = count - messageCount - taskCount });
}).RequireAuthorization();

app.MapGet("/api/security/status", async (HttpContext context, ApplicationDbContext db, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (!LoginSecurity.Enabled(app.Configuration)) return Results.Json(new { alert = (object?)null });
    var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var token = context.User.FindFirstValue("login_session");
    var sessionId = LoginSecurity.ValidToken(token) ? LoginSecurity.Hash(token!) : "";
    var alert = await db.Set<LoginAlert>().AsNoTracking().Where(a => a.UserId == userId && a.SessionId != sessionId && a.ReadUtc == null)
        .OrderByDescending(a => a.Id).Select(a => new { a.Id, a.Device, a.CreatedUtc }).FirstOrDefaultAsync(ct);
    return Results.Json(new { alert });
}).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = "Cookies,AdminCookie" });
app.MapMobileEndpoints();
app.MapGet("/api/places/search", async (string? q, PlaceSearch search, CancellationToken ct) =>
{
    q = (q ?? "").Trim();
    if (q.Length is < 3 or > 120) return Results.BadRequest(new { message = "Enter 3–120 characters of an address or landmark." });
    try { return Results.Json(await search.SearchAsync(q, ct)); }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
    { return Results.Json(new { message = "Location search is unavailable. Use the map pin or current location instead." }, statusCode: 503); }
}).RequireAuthorization().RequireRateLimiting("PlaceSearchLimit");
app.MapGet("/api/places/reverse", async (double lat, double lon, PlaceSearch search, CancellationToken ct) =>
{
    if (!TaskLocationRules.Valid(lat, lon)) return Results.BadRequest();
    try { return Results.Json(await search.ReverseAsync(lat, lon, ct)); }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
    { return Results.Json(new { message = "Address lookup is unavailable." }, statusCode: 503); }
}).RequireAuthorization().RequireRateLimiting("PlaceSearchLimit");
app.MapRazorPages();

app.Run();
