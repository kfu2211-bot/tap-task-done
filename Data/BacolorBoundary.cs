using System.Text.Json;

namespace tts.Data;

internal static class BacolorBoundary
{
    // Bacolor polygon from geoBoundaries PHL ADM3 (NAMRIA, PSA, OCHA Philippines).
    // https://github.com/wmgeolab/geoBoundaries/tree/9469f09/releaseData/gbOpen/PHL/ADM3
    // https://creativecommons.org/licenses/by/3.0/igo/
    private static readonly (double Longitude, double Latitude)[] Ring = LoadRing();

    internal static bool Contains(double latitude, double longitude)
    {
        var inside = false;
        for (var i = 1; i < Ring.Length; i++)
        {
            var (x1, y1) = Ring[i - 1];
            var (x2, y2) = Ring[i];
            var cross = (longitude - x1) * (y2 - y1) - (latitude - y1) * (x2 - x1);
            if (Math.Abs(cross) <= 1e-12 &&
                longitude >= Math.Min(x1, x2) && longitude <= Math.Max(x1, x2) &&
                latitude >= Math.Min(y1, y2) && latitude <= Math.Max(y1, y2))
                return true;

            if ((y1 > latitude) != (y2 > latitude) &&
                longitude < x1 + (latitude - y1) * (x2 - x1) / (y2 - y1))
                inside = !inside;
        }
        return inside;
    }

    private static (double Longitude, double Latitude)[] LoadRing()
    {
        using var stream = typeof(BacolorBoundary).Assembly.GetManifestResourceStream("BacolorBoundary.geojson")
            ?? throw new InvalidOperationException("Bacolor boundary resource is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("coordinates")[0].EnumerateArray()
            .Select(point => (point[0].GetDouble(), point[1].GetDouble())).ToArray();
    }
}
