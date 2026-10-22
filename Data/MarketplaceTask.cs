namespace tts.Data;

public sealed class MarketplaceTask
{
    public long Id { get; set; }
    public int ClientId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public decimal Budget { get; set; }
    public string ScheduleText { get; set; } = "";
    public string Status { get; set; } = "pending-confirmation";
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MarketplaceOffer
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int FreelancerId { get; set; }
    public decimal Price { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? WithdrawalReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
