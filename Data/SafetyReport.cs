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
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SafetyReportAction
{
    public long Id { get; set; }
    public long ReportId { get; set; }
    public int AdminId { get; set; }
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public string? PublicMessage { get; set; }
    public bool ReviewConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class SafetyReportPriority
{
    public static int Initial(string reason, bool immediateDanger) =>
        immediateDanger || reason is "Scam or fake account" or "Harassment or inappropriate behavior" or "Threat to safety" ? 3 :
        reason is "Work quality or service issue" or "Minor communication or service concern" ? 1 : 2;

    public static IQueryable<RankedReport> Rank(IQueryable<SafetyReport> reports) =>
        reports.Select(r => new RankedReport { Report = r, Priority = r.Priority == 3 || r.Reason == "Scam or fake account" ||
            r.Reason == "Harassment or inappropriate behavior" || r.Reason == "Threat to safety" ? 3 : (r.Reason == "Minor communication or service concern" || (r.Reason == "Work quality or service issue" && r.Priority != 3)) ? 1 : 2 });

    public sealed class RankedReport
    {
        public required SafetyReport Report { get; init; }
        public int Priority { get; init; }
    }
}