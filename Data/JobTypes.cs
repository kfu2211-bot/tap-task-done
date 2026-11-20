using Microsoft.EntityFrameworkCore;
namespace tts.Data;

public static class JobTypes
{
    public const string ScheduleMessage = "Choose a job type and check the work schedule. Finish must be after start; Short/Long jobs finish on the same date, and Multi-Day jobs finish on a later date.";
    public static bool Valid(string? value) => value is "short" or "long" or "multi-day";
    public static string Label(string? value) => value switch {
        "short" => "Short Job", "long" => "Long Job", "multi-day" => "Multi-Day Job", _ => "Not classified"
    };
    public static DateTime? EndUtc(DateTime? local) => local is null ? null : local < DateTime.MinValue.AddHours(8)
        ? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc) : DateTime.SpecifyKind(local.Value.AddHours(-8), DateTimeKind.Utc);
    public static bool ValidSchedule(string? type, string? schedule, DateTime? endUtc, bool requireEnd = false)
    {
        if (!Valid(type)) return false;
        if (endUtc is null) return !requireEnd && type != "multi-day";
        var start = TaskSchedule.StartUtc(schedule);
        if (start is null || endUtc <= start) return false;
        return type == "multi-day" ? endUtc.Value.AddHours(8).Date > start.Value.AddHours(8).Date
            : endUtc.Value.AddHours(8).Date == start.Value.AddHours(8).Date;
    }
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) => start < otherEnd && otherStart < end;
    public static async Task<bool> ConflictsAsync(ApplicationDbContext db, int freelancerId, long taskId, string schedule, DateTime? endUtc, CancellationToken ct)
    {
        var start = TaskSchedule.StartUtc(schedule);
        if (start is null || endUtc is null) return false;
        var jobs = await db.MarketplaceTasks.AsNoTracking().Where(t => t.Id != taskId &&
            (t.Status == "assigned" || t.Status == "on-the-way" || t.Status == "arrived" || t.Status == "in-progress" || t.Status == "awaiting-confirmation") &&
            db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == freelancerId && o.Status == "accepted"))
            .Select(t => new { t.ScheduleText, t.WorkEndUtc }).ToListAsync(ct);
        return jobs.Any(job => TaskSchedule.StartUtc(job.ScheduleText) is DateTime otherStart && job.WorkEndUtc is DateTime otherEnd && Overlaps(start.Value, endUtc.Value, otherStart, otherEnd));
    }
}
public record JobTypeInput(string? Value = null, DateTime? WorkEndUtc = null, bool RequireEnd = false);
