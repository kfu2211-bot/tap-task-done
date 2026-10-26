namespace tts.Data;

public sealed class IdentityRequest
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string IdLast4 { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ReviewReadAt { get; set; }
    public int? ReviewedById { get; set; }
    public string? ReviewNote { get; set; }
    public byte[]? GovernmentId { get; set; }
    public string? GovernmentType { get; set; }
    public byte[]? Certification { get; set; }
    public string? CertificationType { get; set; }
    public byte[]? SupportingDocument { get; set; }
    public string? SupportingType { get; set; }
}
