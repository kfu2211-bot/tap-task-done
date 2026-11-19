using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace tts.Data;

// A signed capture session binds the requested sequence to a user and an expiry.
// Uploaded frames remain evidence for manual review, not proof of liveness.
public sealed record IdentityCaptureChallenge(int UserId, DateTime ExpiresAt, string[] Steps)
{
    public static IdentityCaptureChallenge Create(int userId)
    {
        string[] turns = ["left", "right", "tilt"];
        RandomNumberGenerator.Shuffle(turns.AsSpan());
        return new(userId, DateTime.UtcNow.AddMinutes(20), ["front", .. turns, "front"]);
    }

    public string Protect(IDataProtectionProvider provider) => provider.CreateProtector("tts.identity.capture.v1").Protect(JsonSerializer.Serialize(this));

    public static IdentityCaptureChallenge? Read(string? token, int userId, IDataProtectionProvider provider)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return null;
        try
        {
            var value = JsonSerializer.Deserialize<IdentityCaptureChallenge>(provider.CreateProtector("tts.identity.capture.v1").Unprotect(token));
            return value is not null && value.UserId == userId && value.ExpiresAt > DateTime.UtcNow &&
                value.Steps.Length == 5 && value.Steps[0] == "front" && value.Steps[4] == "front" &&
                value.Steps[1..4].Order().SequenceEqual(new[] { "left", "right", "tilt" }.Order()) ? value : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }
}
