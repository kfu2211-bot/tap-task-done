namespace tts.Data;

public sealed class AccountRestriction
{
    public int UserId { get; set; }
    public bool IsRestricted { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UpdatedById { get; set; }
}

public sealed class AccountModerationAction
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int AdminId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
