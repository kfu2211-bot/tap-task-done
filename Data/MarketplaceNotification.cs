namespace tts.Data;

public sealed class MarketplaceNotification
{
    public static readonly string[] TaskKinds = new[] { "offer", "accepted", "booking_request", "booking_declined", "booking_accepted", "cancelled", "withdrawn", "agreement", "completion_reminder", "task_expired", "assigned", "on-the-way", "arrived", "in-progress", "awaiting-confirmation", "started", "finished", "completed" };

    public long Id { get; set; }
    public int UserId { get; set; }
    public long TaskId { get; set; }
    public long? ConversationId { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public static MarketplaceNotification Create(int userId, long taskId, string kind, string message) =>
        new() { UserId = userId, TaskId = taskId, Kind = kind, Message = message, CreatedAt = DateTime.UtcNow };
}
