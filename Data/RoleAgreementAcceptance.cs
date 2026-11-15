namespace tts.Data;

public sealed class RoleAgreementAcceptance
{
    public const int CurrentVersion = 2;
    public int UserId { get; set; }
    public string Role { get; set; } = "";
    public int Version { get; set; }
    public DateTime AcceptedAtUtc { get; set; }
}
