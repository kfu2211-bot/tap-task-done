namespace tts.Data;

public static class NewPasswordPolicy
{
    public const string Requirements = "Use 8–256 characters with at least one uppercase letter, lowercase letter, number, and special character (such as !, @, or #).";
    public const string Pattern = @"(?=[\s\S]*[A-Z])(?=[\s\S]*[a-z])(?=[\s\S]*[0-9])(?=[\s\S]*[\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E])[\s\S]+";

    public static bool IsValid(string? password) =>
        password is { Length: >= 8 and <= 256 } &&
        password.Any(char.IsAsciiLetterUpper) && password.Any(char.IsAsciiLetterLower) &&
        password.Any(char.IsAsciiDigit) &&
        password.Any(c => c is >= '!' and <= '~' && !char.IsAsciiLetterOrDigit(c));
}
