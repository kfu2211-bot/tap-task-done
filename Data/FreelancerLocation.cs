namespace tts.Data;

public sealed class TaskDestination
{
    public long TaskId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class FreelancerLocation
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public long TaskId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
