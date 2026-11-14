using System.ComponentModel.DataAnnotations.Schema;

namespace tts.Data;

[Table("community_feedback")]
public class CommunityFeedback
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string Message { get; set; } = "";
}
