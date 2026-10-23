namespace tts.Data;

public sealed class SafetyReportEvidence
{
    public long Id { get; set; }
    public long ReportId { get; set; }
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
}
