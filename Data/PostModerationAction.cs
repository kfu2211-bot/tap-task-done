namespace tts.Data;

[System.ComponentModel.DataAnnotations.Schema.Table("post_moderation_actions")]
public sealed class PostModerationAction
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public int ActorId { get; set; }
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
