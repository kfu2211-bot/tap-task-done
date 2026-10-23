namespace tts.Data;

public sealed class PasswordResetCode
{
    public string ChallengeHash { get; set; } = "";
    public int UserId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public bool Verified { get; set; }
}
