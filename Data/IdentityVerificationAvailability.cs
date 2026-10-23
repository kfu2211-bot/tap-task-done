namespace tts.Data;

// The production feature remains off until secure key storage is configured.
public sealed record IdentityVerificationAvailability(bool Enabled);
