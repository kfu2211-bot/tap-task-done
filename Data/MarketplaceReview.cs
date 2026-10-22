namespace tts.Data;

public sealed class MarketplaceReview
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int ClientId { get; set; }
    public int FreelancerId { get; set; }
    public int Rating { get; set; }
    public string Feedback { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
