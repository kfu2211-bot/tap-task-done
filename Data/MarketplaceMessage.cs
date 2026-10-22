namespace tts.Data;

public sealed class MarketplaceMessage
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int SenderId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
