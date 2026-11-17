using System.Globalization;

namespace tts.Data;

public static class TaskSchedule
{
    public static DateTime? StartUtc(string? text) =>
        DateTime.TryParseExact(text?.Trim(),
            ["MMM d, yyyy 'at' h:mm tt '(PHT)'", "MMM d, yyyy, h:mm tt", "yyyy-MM-dd'T'HH:mm"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? new DateTimeOffset(local, TimeSpan.FromHours(8)).UtcDateTime : null;

    public static bool IsUpcoming(MarketplaceTask task, DateTime nowUtc) =>
        task.Status == "assigned" && StartUtc(task.ScheduleText) > nowUtc;

    public static string DisplayStatus(MarketplaceTask task, DateTime nowUtc) =>
        IsUpcoming(task, nowUtc) ? "Scheduled" : TaskProgress.Label(task.Status);
}
