using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;

namespace tts.Data;

public sealed class IdentityIdOcr(IDataProtectionProvider protection, ILogger<IdentityIdOcr> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public record Result(bool Success, string Message, string? Token = null);
    private sealed record Receipt(int UserId, string Type, string Number, string Hash, DateTime Expires);
    private static readonly Dictionary<string, (string Heading, string Label)> Rules = new()
    {
        ["Philippine National ID"] = (@"PAMBANSANG\s+PAGKAKAKILANLAN|PHILIPPINE\s+IDENTIFICATION|PHILSYS", @"\b(?:PCN|PSN|PHILSYS\s+CARD\s+NUMBER|CARD\s+NUMBER)\b"),
        ["Philippine Passport"] = (@"PASSPORT|PASAPORTE", @"\bPASSPORT\s*(?:NO\.?|NUMBER)\b"),
        ["Driver's License"] = (@"LAND\s+TRANSPORTATION\s+OFFICE|DRIVER.?S?\s+LICEN[CS]E", @"\bLICEN[CS]E\s*(?:NO\.?|NUMBER)\b"),
        ["UMID"] = (@"UNIFIED\s+MULTI.?PURPOSE|UMID", @"\b(?:CRN|COMMON\s+REFERENCE\s+NUMBER)\b"),
        ["PRC ID"] = (@"PROFESSIONAL\s+REGULATION\s+COMMISSION|PROFESSIONAL\s+IDENTIFICATION", @"\b(?:REGISTRATION|LICENSE|LICENCE)\s*(?:NO\.?|NUMBER)\b"),
        ["Postal ID"] = (@"POSTAL\s+ID|PHILIPPINE\s+POSTAL", @"\b(?:PRN|POSTAL\s+REFERENCE\s+NUMBER|ID\s*(?:NO\.?|NUMBER))\b")
    };
    public static bool Matches(string type, string number, IEnumerable<string> lines)
    {
        if (!IdentityDocumentTypes.ValidNumber(number) || !Rules.TryGetValue(type, out var rule)) return false;
        var text = lines.Take(400).Select(s => s.ToUpperInvariant()).ToArray();
        if (!Regex.IsMatch(string.Join('\n', text), rule.Heading, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) return false;
        for (var i = 0; i < text.Length; i++)
        {
            var label = Regex.Match(text[i], rule.Label, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!label.Success) continue;
            var value = text[i][(label.Index + label.Length)..].TrimStart(' ', '.', ':', '#');
            if (!value.Any(c => c is >= '0' and <= '9') && i + 1 < text.Length) value = text[i + 1].Trim();
            // Read one complete printed number, never a substring or digits
            // taken from unrelated dates, names, or another field.
            var candidate = Regex.Match(value, @"^[A-Z0-9]*[0-9][A-Z0-9-]*(?:[ -]+[0-9][A-Z0-9-]*)*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (candidate.Success && (candidate.Length == value.Length || !"/._".Contains(value[candidate.Length])) &&
                IdentityDocumentTypes.NormalizeNumber(candidate.Value) == IdentityDocumentTypes.NormalizeNumber(number)) return true;
        }
        return false;
    }
    public bool ValidReceipt(string? token, int userId, string type, string number, byte[] bytes)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return false;
        try
        {
            var r = JsonSerializer.Deserialize<Receipt>(protection.CreateProtector("tts.identity.ocr.v1").Unprotect(token));
            return r is not null && r.UserId == userId && r.Type == type && r.Number == IdentityDocumentTypes.NormalizeNumber(number) && r.Expires > DateTime.UtcNow && r.Hash == Convert.ToHexString(SHA256.HashData(bytes));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return false; }
    }
    public async Task<Result> CheckAsync(int userId, string type, string number, byte[] bytes, string contentType, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) return new(false, "ID checking is busy. Please try again in a moment.");
        var directory = Path.Combine(Path.GetTempPath(), "tts-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var input = Path.Combine(directory, "document" + (contentType == "application/pdf" ? ".pdf" : contentType == "image/png" ? ".png" : ".jpg"));
            await File.WriteAllBytesAsync(input, bytes, timeout.Token);
            if (contentType == "application/pdf")
            {
                // OCR rendered pixels, not a PDF text layer that can disagree with its visible image.
                var prefix = Path.Combine(directory, "page");
                await Run("pdftoppm", ["-f", "1", "-l", "1", "-singlefile", "-scale-to", "2200", "-png", input, prefix], timeout.Token);
                input = prefix + ".png";
            }
            var tsv = await Run("tesseract", [input, "stdout", "-l", "eng", "--psm", "11", "tsv"], timeout.Token);
            var lines = ConfidentLines(tsv);
            if (!Matches(type, number, lines)) return new(false, "ID details could not be matched. Check the full number and upload a clear front photo of the selected ID.");
            var receipt = new Receipt(userId, type, IdentityDocumentTypes.NormalizeNumber(number), Convert.ToHexString(SHA256.HashData(bytes)), DateTime.UtcNow.AddMinutes(20));
            return new(true, "ID details matched. You can open the camera.", protection.CreateProtector("tts.identity.ocr.v1").Protect(JsonSerializer.Serialize(receipt)));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(false, "ID reading timed out. Try a clear JPG or PNG photo."); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            // Never log OCR output, ID numbers or uploaded document contents.
            logger.LogWarning("ID reader unavailable ({ErrorType}).", ex.GetType().Name);
            return new(false, "Automatic ID reading is temporarily unavailable. Please try again later; unchecked documents cannot be submitted.");
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Temporary ID cleanup failed."); }
            gate.Release();
        }
    }
    public static string[] ConfidentLines(string tsv) => tsv.Split('\n').Skip(1)
        .Select(line => line.TrimEnd('\r').Split('\t', 12))
        .Where(parts => parts.Length == 12 && parts[0] == "5")
        .GroupBy(parts => string.Join(':', parts.Take(5)))
        // Discard the entire line if any word is uncertain, so a missing digit
        // cannot accidentally turn a longer number into a shorter matching one.
        .Where(group => group.All(parts => double.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var score) && score >= 65))
        .Select(group => string.Join(' ', group.Select(parts => parts[11]))).ToArray();
    private static async Task<string> Run(string executable, string[] arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["OMP_THREAD_LIMIT"] = "1";
        using var process = Process.Start(info) ?? throw new InvalidOperationException();
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); var text = await output; await error;
            if (process.ExitCode != 0 || text.Length > 1024 * 1024) throw new InvalidOperationException();
            return text;
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
}
