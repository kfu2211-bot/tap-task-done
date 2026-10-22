namespace tts.Data;

public sealed class MarketplaceBooking
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int FreelancerId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}
