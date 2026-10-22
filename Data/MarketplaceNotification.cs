namespace tts.Data;

public sealed class MarketplaceNotification
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public long TaskId { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public static MarketplaceNotification Create(int userId, long taskId, string kind, string message) =>
        new() { UserId = userId, TaskId = taskId, Kind = kind, Message = message, CreatedAt = DateTime.UtcNow };
}
