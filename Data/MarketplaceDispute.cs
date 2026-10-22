namespace tts.Data;

public sealed class MarketplaceDispute
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int OpenedById { get; set; }
    public string Reason { get; set; } = "";
    public string PreviousTaskStatus { get; set; } = "";
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; }
    public int? ClosedById { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ClosedAt { get; set; }
}
