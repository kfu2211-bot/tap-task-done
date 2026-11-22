using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace tts.Data;
public static class TaskAcceptanceTerms
{
    public const string EventStatus = "accepted-terms-snapshot";
    public static string Revision(MarketplaceTask task, decimal price) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        task.Id, Price = price.ToString("G29", System.Globalization.CultureInfo.InvariantCulture), task.JobType, WorkEndTicks = task.WorkEndUtc?.Ticks, task.EstimatedMinutes, task.ScheduleText, task.Description, task.Location, task.ArrivalInstructions
    }))));
    public static TaskProgressEvent Snapshot(MarketplaceTask task, decimal price, int actor) => new() {
        TaskId = task.Id, ActorId = actor, Status = EventStatus, Note = Revision(task, price), CreatedAtUtc = DateTime.UtcNow
    };
    public static bool CanUseAcceptance(MarketplaceTask task, decimal price, string? revision) => task.JobType == "short" &&
        JobTypes.ValidSchedule(task.JobType, task.ScheduleText, task.WorkEndUtc) && revision == Revision(task, price);
}
