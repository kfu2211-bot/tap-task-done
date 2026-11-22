using System.Globalization;

namespace tts.Data;

public static class TaskSchedule
{
    public static string Display(string? text) => StartUtc(text) is { } start
        ? start.AddHours(8).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture) + " PHT"
        : string.IsNullOrWhiteSpace(text) ? "To be scheduled" : text;
    public static DateTime? StartUtc(string? text) =>
        DateTime.TryParseExact(text?.Trim(),
            ["MMM d, yyyy 'at' h:mm tt '(PHT)'", "MMM d, yyyy, h:mm tt", "yyyy-MM-dd'T'HH:mm"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? new DateTimeOffset(local, TimeSpan.FromHours(8)).UtcDateTime : null;

    public static bool IsUpcoming(MarketplaceTask task, DateTime nowUtc) =>
        task.Status == "assigned" && StartUtc(task.ScheduleText) > nowUtc;

    public static bool TravelOpen(MarketplaceTask task, DateTime nowUtc) =>
        task.Status == "assigned" && StartUtc(task.ScheduleText) is { } start && nowUtc >= start.AddHours(-1);

    public static bool SameStart(string first, string second) =>
        StartUtc(first) is { } start && StartUtc(second) is { } other ? start == other : first == second;

    public static string DisplayStatus(MarketplaceTask task, DateTime nowUtc) =>
        task.Status == "assigned" && StartUtc(task.ScheduleText) is { } start
            ? ScheduleLabel(start, nowUtc) : TaskProgress.Label(task.Status);

    public static string ScheduleLabel(DateTime start, DateTime now) =>
        now < start ? "Scheduled" : now >= start.AddMinutes(30) ? "Schedule overdue" : "Due now";

    // One reminder per window and confirmed agreement version; never changes job status.
    public static string? ReminderKind(DateTime start, DateTime now) =>
        now >= start.AddMinutes(30) ? "schedule_overdue" :
        now >= start.AddHours(-1) && now < start ? "schedule_1h" :
        now >= start.AddHours(-24) && now < start.AddHours(-1) ? "schedule_24h" : null;
}
