namespace tts.Data;

public sealed class SafetyReport
{
    public long Id { get; set; }
    public int ReporterId { get; set; }
    public long? TaskId { get; set; }
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; }
}

public sealed class SafetyReportAction
{
    public long Id { get; set; }
    public long ReportId { get; set; }
    public int AdminId { get; set; }
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
