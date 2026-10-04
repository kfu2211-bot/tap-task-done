using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
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

app.UseAuthorization();

app.MapRazorPages();

app.Run();
