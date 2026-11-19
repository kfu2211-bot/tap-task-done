namespace tts.Data;

public static class IdentityDocumentTypes
{
    public static readonly string[] Accepted = ["Philippine National ID", "Philippine Passport", "Driver's License", "UMID", "PRC ID", "Postal ID"];
    public static bool Supports(string? type) => type is not null && Accepted.Contains(type, StringComparer.Ordinal);
    public sealed record NumberField(string Label, string Help);
    public static NumberField NumberFor(string? type) => type switch
    {
        "Driver's License" => new("License No.", "Enter the License No. printed on your license, including its leading letter. Do not use the card serial number or LTO client ID."),
        "Philippine National ID" => new("PhilSys Card Number (PCN)", "Enter the 16-digit PCN printed on the front of your National ID. Do not enter your private PSN or transaction number."),
        "Philippine Passport" => new("Passport No.", "Enter the Passport No. on the photo / biodata page."),
        "UMID" => new("Common Reference Number (CRN)", "Enter the CRN printed on your UMID card, not your SSS membership number."),
        "PRC ID" => new("Registration No.", "Enter the Registration No. printed on your Professional Identification Card."),
        "Postal ID" => new("Postal Reference Number (PRN)", "Enter the Postal Reference No. / PRN printed on your Postal ID."),
        _ => new("ID number", "Select your ID type to see which number to enter.")
    };
    // Ignore printed separators only; never discard other characters when comparing.
    public static string NormalizeNumber(string value) => value.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
    public static bool ValidNumber(string? value) => value is not null && value.Length <= 50 &&
        NormalizeNumber(value) is { Length: >= 4 and <= 32 } normalized && normalized.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
}
