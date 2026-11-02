namespace tts.Data;

public static class IdentityDocumentTypes
{
    public static readonly string[] Accepted = ["Philippine National ID", "Philippine Passport", "Driver's License", "UMID", "PRC ID", "Postal ID"];
    public static bool Supports(string? type) => type is not null && Accepted.Contains(type, StringComparer.Ordinal);
}
