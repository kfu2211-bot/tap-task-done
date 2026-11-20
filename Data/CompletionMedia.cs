namespace tts.Data;

public static class CompletionMedia
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public static string? VideoType(byte[] bytes)
    {
        if (bytes.Length < 16 || bytes.Length > MaxBytes) return null;
        if (bytes[0] == 0x1a && bytes[1] == 0x45 && bytes[2] == 0xdf && bytes[3] == 0xa3 &&
            System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 512)).Contains("webm", StringComparison.Ordinal))
            return "video/webm";
        if (bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            var brands = System.Text.Encoding.ASCII.GetString(bytes, 8, Math.Min(bytes.Length - 8, 64));
            if (new[] { "isom", "iso2", "mp41", "mp42", "avc1", "M4V " }.Any(brands.Contains)) return "video/mp4";
        }
        return null;
    }
}
