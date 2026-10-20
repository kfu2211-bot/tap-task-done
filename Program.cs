using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using tts.Data;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}
var lanPreview = string.Equals(Environment.GetEnvironmentVariable("TTS_LAN_PREVIEW"), "true", StringComparison.OrdinalIgnoreCase);
var renderDeployment = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
var assignedPort = Environment.GetEnvironmentVariable("PORT");

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
builder.Services.AddRazorPages();
builder.Services.AddScoped<AccountStore>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddAuthorization();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
