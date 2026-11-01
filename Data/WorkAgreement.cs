using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed record TaskLocationInput(string Address = "", double? Latitude = null, double? Longitude = null);
public sealed record AgreementView(MarketplaceTask Task, WorkAgreement? Agreement, TaskDestination? Destination, bool IsClient);

public sealed class TaskConversation
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int FreelancerId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class WorkAgreement
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int Version { get; set; }
    public int ProposedById { get; set; }
    public decimal Price { get; set; }
    public string ScheduleText { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Location { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ClientConfirmedAtUtc { get; set; }
    public DateTime? FreelancerConfirmedAtUtc { get; set; }
    public bool Confirmed => ClientConfirmedAtUtc.HasValue && FreelancerConfirmedAtUtc.HasValue;
}

public static class TaskLocationRules
{
    // Search/service-area envelope, not a cadastral municipal boundary.
    public static bool Valid(double? latitude, double? longitude) => latitude.HasValue && longitude.HasValue &&
        double.IsFinite(latitude.Value) && double.IsFinite(longitude.Value) && latitude is >= 14.92 and <= 15.10 && longitude is >= 120.54 and <= 120.73;
    public static string PublicArea => "Bacolor, Pampanga";
    public static void Save(ApplicationDbContext db, long taskId, TaskDestination? existing, double latitude, double longitude)
    {
        var pin = existing ?? new TaskDestination { TaskId = taskId };
        if (existing is null) db.Add(pin);
        pin.Latitude = latitude; pin.Longitude = longitude; pin.UpdatedAtUtc = DateTime.UtcNow;
    }
}

public static class AgreementRules
{
    public static bool CanProceed(WorkAgreement? agreement) => agreement?.Confirmed == true;
    public static bool MatchesVersion(WorkAgreement? agreement, int version) => (agreement?.Version ?? 0) == version;
    public static bool Editable(string status) => status is "assigned" or "on-the-way";
    public static bool Valid(decimal price, string scope, string schedule, string location, double? latitude, double? longitude) =>
        price is > 0 and <= 9999999999m && decimal.Round(price, 2) == price && scope.Length is > 0 and <= 5000 &&
        schedule.Length is > 0 and <= 120 && location.Length is > 0 and <= 120 && TaskLocationRules.Valid(latitude, longitude);
    public static bool SameTerms(WorkAgreement a, WorkAgreement b) => a.Price == b.Price && a.Scope == b.Scope && a.ScheduleText == b.ScheduleText &&
        a.Location == b.Location && a.Latitude == b.Latitude && a.Longitude == b.Longitude;
    public static async Task<WorkAgreement?> LatestAsync(ApplicationDbContext db, long taskId, CancellationToken ct) =>
        await db.Set<WorkAgreement>().Where(a => a.TaskId == taskId).OrderByDescending(a => a.Version).FirstOrDefaultAsync(ct);
    public static async Task InitialAsync(ApplicationDbContext db, MarketplaceTask task, decimal price, int actor, CancellationToken ct)
    {
        var pin = await db.Set<TaskDestination>().SingleOrDefaultAsync(p => p.TaskId == task.Id, ct);
        if (pin is null || !TaskLocationRules.Valid(pin.Latitude, pin.Longitude)) return; // Existing jobs can propose terms in Tracking.
        var previous = await LatestAsync(db, task.Id, ct);
        db.Add(new WorkAgreement { TaskId = task.Id, Version = (previous?.Version ?? 0) + 1, ProposedById = actor, Price = price,
            Scope = task.Description, ScheduleText = string.IsNullOrWhiteSpace(task.ScheduleText) ? "To be agreed" : task.ScheduleText,
            Location = task.Location, Latitude = pin.Latitude, Longitude = pin.Longitude, CreatedAtUtc = DateTime.UtcNow });
    }
    public static async Task ConversationAsync(ApplicationDbContext db, long taskId, int freelancerId, CancellationToken ct)
    {
        if (!await db.Set<TaskConversation>().AnyAsync(c => c.TaskId == taskId && c.FreelancerId == freelancerId, ct))
            db.Add(new TaskConversation { TaskId = taskId, FreelancerId = freelancerId, CreatedAtUtc = DateTime.UtcNow });
    }
}

public static class ConversationAccess
{
    public static IQueryable<TaskConversation> VisibleTo(ApplicationDbContext db, int viewer) =>
        db.Set<TaskConversation>().Where(c => c.FreelancerId == viewer || db.MarketplaceTasks.Any(t => t.Id == c.TaskId && t.ClientId == viewer));
}
