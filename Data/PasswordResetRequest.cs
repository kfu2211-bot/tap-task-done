namespace tts.Data;
public sealed class PasswordResetRequest
{
    public string TokenHash { get; set; } = "";
    public int UserId { get; set; }
    public DateTime ExpiresUtc { get; set; }
}
