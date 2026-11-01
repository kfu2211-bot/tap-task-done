using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace tts.Data;

// User-initiated address search only; no location-history or personal data is sent.
public sealed class PlaceSearch(HttpClient http, IConfiguration configuration)
{
    public record Place(string Label, double Latitude, double Longitude);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions { SizeLimit = 256 });
    private static DateTime lastRequest;
    public async Task<Place[]> SearchAsync(string query, CancellationToken ct)
    {
        var key = query.Trim().ToLowerInvariant();
        if (Cache.TryGetValue(key, out Place[]? cached)) return cached!;
        await Gate.WaitAsync(ct);
        try
        {
            if (Cache.TryGetValue(key, out cached)) return cached!;
            var wait = lastRequest.AddSeconds(1) - DateTime.UtcNow; if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            var baseUrl = configuration["Geocoding:BaseUrl"] ?? "https://photon.komoot.io";
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https") throw new InvalidOperationException("Invalid geocoding endpoint.");
            var url = endpoint.ToString().TrimEnd('/') + "/api/?q=" + Uri.EscapeDataString(query) + "&limit=6&bbox=120.54,14.92,120.73,15.10&lat=15.000&lon=120.653&lang=en";
            using var request = new HttpRequestMessage(HttpMethod.Get, url); request.Headers.UserAgent.ParseAdd("TapTaskDone/1.0 (Bacolor task address search)");
            lastRequest = DateTime.UtcNow;
            using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var result = new List<Place>();
            foreach (var feature in json.RootElement.GetProperty("features").EnumerateArray())
            {
                var coordinates = feature.GetProperty("geometry").GetProperty("coordinates"); double lon = coordinates[0].GetDouble(), lat = coordinates[1].GetDouble();
                if (!TaskLocationRules.Valid(lat, lon)) continue;
                var properties = feature.GetProperty("properties");
                var parts = new[] { "name", "street", "district", "city", "state" }.Select(k => properties.TryGetProperty(k, out var v) ? v.GetString() : null).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
                var label = parts.Length > 0 ? string.Join(", ", parts) : "Pinned location, Bacolor, Pampanga";
                result.Add(new Place(label.Length > 120 ? label[..120] : label, lat, lon));
            }
            var values = result.ToArray(); Cache.Set(key, values, new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromHours(12))); return values;
        }
        finally { Gate.Release(); }
    }
}
