using Microsoft.AspNetCore.Http;
namespace tts.Data;

public static class WorkspaceNavigation
{
    private static readonly HashSet<string> ReturnPages = new(StringComparer.OrdinalIgnoreCase) {
        "/Dashboard", "/FindPost", "/MarketTasks", "/MyTasks", "/MyJobs", "/Messages", "/Reviews", "/Tracking", "/Settings", "/JobRecord", "/TaskDetails"
    };
    public static string? SafeReturn(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.StartsWith("//") || path.Any(c => char.IsControl(c) || c == '\\')) return null;
        var page = path.Split('?', '#')[0];
        return ReturnPages.Contains(page) ? path : null;
    }
    public static string ProfileBack(HttpRequest request, string? returnUrl)
    {
        if (SafeReturn(returnUrl) is { } specified) return specified;
        if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) &&
            Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var current) &&
            referer.Scheme == current.Scheme && referer.Authority.Equals(current.Authority, StringComparison.OrdinalIgnoreCase) &&
            SafeReturn(referer.PathAndQuery) is { } previous) return previous;
        return "/Dashboard";
    }
    public static string WithoutLegacy(HttpRequest request) => $"{request.PathBase}{request.Path}" + QueryString.Create(request.Query
        .Where(q => !q.Key.Equals("legacy", StringComparison.OrdinalIgnoreCase))
        .SelectMany(q => q.Value.Select(value => new KeyValuePair<string, string?>(q.Key, value))));
}
