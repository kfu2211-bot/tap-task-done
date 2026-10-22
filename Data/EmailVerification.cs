namespace tts.Data;

public sealed class EmailVerification
{
    public string TokenHash { get; set; } = "";
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
}

public sealed class VerifiedEmail
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public DateTime VerifiedUtc { get; set; }
}
