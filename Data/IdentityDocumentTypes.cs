namespace tts.Data;

public static class IdentityDocumentTypes
{
    public static readonly string[] Accepted = ["Philippine National ID", "Philippine Passport", "Driver's License", "UMID", "PRC ID", "Postal ID"];
    public static bool Supports(string? type) => type is not null && Accepted.Contains(type, StringComparer.Ordinal);
    // Ignore printed separators only; never discard other characters when comparing.
    public static string NormalizeNumber(string value) => value.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
    public static bool ValidNumber(string? value) => value is not null && value.Length <= 50 &&
        NormalizeNumber(value) is { Length: >= 4 and <= 32 } normalized && normalized.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
}
