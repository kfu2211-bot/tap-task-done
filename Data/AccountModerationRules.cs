namespace tts.Data;

/// <summary>Shared validation for admin moderation actions. This does not itself enforce restrictions.</summary>
public static class AccountModerationRules
{
    public const int MaxReasonLength = 500;

    public static bool IsStaff(string? role) =>
        string.Equals(role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role?.Trim(), "moderator", StringComparison.OrdinalIgnoreCase);

    public static bool IsValidAction(string? action) => action is "warn" or "restrict" or "suspend" or "restore";

    public static bool IsValidReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= MaxReasonLength;
}
