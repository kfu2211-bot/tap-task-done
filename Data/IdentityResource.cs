namespace tts.Data;

public sealed class IdentityResource
{
    public long Id { get; set; }
    public long IdentityRequestId { get; set; }
    public string Kind { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] EncryptedData { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
