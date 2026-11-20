using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;

namespace tts.Data;

public sealed class IdentityIdOcr(IDataProtectionProvider protection, ILogger<IdentityIdOcr> logger, Func<string, string[], CancellationToken, Task<string>>? runner = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public record Result(bool Success, string Message, string? Token = null, bool CanManualReview = false);
    private sealed record Receipt(int UserId, string Type, string Number, string Hash, DateTime Expires, string Mode = "matched");
    private static readonly Dictionary<string, (string Heading, string Label)> Rules = new()
    {
        ["Philippine National ID"] = (@"PAMBANSANG\s+PAGKAKAKILANLAN|PHILIPPINE\s+IDENTIFICATION|PHILSYS", @"\b(?:PCN|PHILSYS\s+CARD\s+NUMBER|CARD\s+NUMBER)\b"),
        ["Philippine Passport"] = (@"PASSPORT|PASAPORTE", @"\b(?:PASSPORT\s*(?:NO\.?|NUMBER)|PASAPORTE\s*(?:BLG\.?|NO\.?))\b"),
        ["Driver's License"] = (@"LAND\s+TRANSPORTATION\s+OFFICE|DRIVER.?S?\s+LICEN[CS]E", @"\bLICEN[CS]E\s*(?:NO\.?|NUMBER)\b"),
        ["UMID"] = (@"UNIFIED\s+MULTI.?PURPOSE|UMID", @"\b(?:CRN|COMMON\s+REFERENCE\s+NUMBER)\b"),
        ["PRC ID"] = (@"PROFESSIONAL\s+REGULATION\s+COMMISSION|PROFESSIONAL\s+IDENTIFICATION", @"\bREGISTRATION\s*(?:NO\.?|NUMBER)\b"),
        ["Postal ID"] = (@"POSTAL\s+ID|PHILIPPINE\s+POSTAL", @"\b(?:PRN|POSTAL\s+REFERENCE\s*(?:NO\.?|NUMBER))\b")
    };
    public static bool Matches(string type, string number, IEnumerable<string> lines) => Assess(type, number, lines) == "matched";
    public static string Assess(string type, string number, IEnumerable<string> lines)
    {
        if (!IdentityDocumentTypes.ValidNumber(number) || !Rules.ContainsKey(type)) return "mismatch";
        if (type == "Philippine National ID" && !Regex.IsMatch(IdentityDocumentTypes.NormalizeNumber(number), @"^[0-9]{16}$")) return "mismatch";
        var text = lines.Take(400).ToArray();
        var hasHeading = Regex.IsMatch(string.Join('\n', text), Rules[type].Heading, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!hasHeading && Rules.Any(rule => rule.Key != type && Regex.IsMatch(string.Join('\n', text), rule.Value.Heading, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))) return "mismatch";
        var candidates = Candidates(type, text).Distinct().ToArray();
        if (candidates.Length != 1) return "unreadable";
        return candidates[0] == IdentityDocumentTypes.NormalizeNumber(number) ? "matched" : "mismatch";
    }
    private static IEnumerable<string> Candidates(string type, IEnumerable<string> lines)
    {
        if (!Rules.TryGetValue(type, out var rule)) yield break;
        var text = lines.Take(400).Select(s => s.ToUpperInvariant()).ToArray();
        if (!Regex.IsMatch(string.Join('\n', text), rule.Heading, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) yield break;
        // The PCN can be printed on the front without an explicit PCN label.
        // Accept its complete, standalone 16-digit line only on a National ID.
        if (type == "Philippine National ID")
            foreach (var line in text.Where(line => Regex.IsMatch(line.Trim(), @"^[0-9]{4}[ -]?[0-9]{4}[ -]?[0-9]{4}[ -]?[0-9]{4}$"))) yield return IdentityDocumentTypes.NormalizeNumber(line);
        for (var i = 0; i < text.Length; i++)
        {
            var label = Regex.Match(text[i], rule.Label, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!label.Success) continue;
            var value = text[i][(label.Index + label.Length)..].TrimStart(' ', '.', ':', '#');
            if (!value.Any(c => c is >= '0' and <= '9') && i + 1 < text.Length) value = text[i + 1].Trim();
            // LTO cards can place the license number, expiry and agency code in
            // adjacent columns. Keep a clearly delimited expiry out of the number.
            if (type == "Driver's License" && Regex.IsMatch(text[i], @"EXPIR(?:ATION|Y)\s+DATE"))
            {
                var columns = Regex.Match(value, @"^(?<number>[A-Z][ -]?[0-9]{2}[ -]?[0-9]{2}[ -]?[0-9]{6})\s+[0-9]{4}[-/][0-9]{2}[-/][0-9]{2}(?:\s|$)");
                if (columns.Success) { yield return IdentityDocumentTypes.NormalizeNumber(columns.Groups["number"].Value); continue; }
            }
            // Read one complete printed number, never a substring or digits
            // taken from unrelated dates, names, or another field.
            var candidate = Regex.Match(value, @"^(?:[A-Z]{1,3}[ -]+)?[A-Z0-9]*[0-9][A-Z0-9-]*(?:[ -]+[0-9][A-Z0-9-]*)*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!candidate.Success) continue;
            var completeValue = candidate.Value; var length = candidate.Length;
            if (char.IsAsciiDigit(completeValue[^1]))
            {
                var suffix = Regex.Match(value[length..], @"^[ -]+[A-Z](?![A-Z0-9])");
                if (suffix.Success) { completeValue += suffix.Value; length += suffix.Length; }
            }
            if (length == value.Length || !"/._".Contains(value[length])) yield return IdentityDocumentTypes.NormalizeNumber(completeValue);
        }
    }
    public bool ValidReceipt(string? token, int userId, string type, string number, byte[] bytes) => ReceiptMode(token, userId, type, number, bytes) == "matched";
    public string? ReceiptMode(string? token, int userId, string type, string number, byte[] bytes)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return null;
        try
        {
            var r = JsonSerializer.Deserialize<Receipt>(protection.CreateProtector("tts.identity.ocr.v2").Unprotect(token));
            return r is not null && r.UserId == userId && r.Type == type && r.Number == IdentityDocumentTypes.NormalizeNumber(number) && r.Expires > DateTime.UtcNow && r.Hash == Convert.ToHexString(SHA256.HashData(bytes)) && r.Mode == "matched" ? r.Mode : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }
    public async Task<Result> CheckAsync(int userId, string type, string number, byte[] bytes, string contentType, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) return new(false, "ID checking is busy. Please try again in a moment.");
        var directory = Path.Combine(Path.GetTempPath(), "tts-id-" + Guid.NewGuid().ToString("N"));
        var total = Stopwatch.StartNew(); var stage = "prepare";
        string Token(string mode) => protection.CreateProtector("tts.identity.ocr.v2").Protect(JsonSerializer.Serialize(new Receipt(userId, type, IdentityDocumentTypes.NormalizeNumber(number), Convert.ToHexString(SHA256.HashData(bytes)), DateTime.UtcNow.AddMinutes(20), mode)));

        async Task<string> Timed(string executable, string[] arguments, string name, CancellationToken token)
        {
            stage = name; var watch = Stopwatch.StartNew();
            try
            {
                using var passTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                if (executable == "tesseract") passTimeout.CancelAfter(TimeSpan.FromSeconds(12));
                try { return await (runner is null ? Run(executable, arguments, passTimeout.Token, directory) : runner(executable, arguments, passTimeout.Token)); }
                catch (OperationCanceledException) when (executable == "tesseract" && !token.IsCancellationRequested)
                {
                    logger.LogWarning("Identity OCR pass {Stage} exceeded its budget; trying the next layout if time permits.", name);
                    return "";
                }
            }
            finally { logger.LogInformation("Identity OCR stage {Stage}: {ElapsedMs} ms.", name, watch.ElapsedMilliseconds); }
        }
        try
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var input = Path.Combine(directory, "document" + (contentType == "application/pdf" ? ".pdf" : contentType == "image/png" ? ".png" : ".jpg"));
            await File.WriteAllBytesAsync(input, bytes, timeout.Token);
            if (contentType == "application/pdf")
            {
                // OCR rendered pixels, not a PDF text layer that can disagree with its visible image.
                var prefix = Path.Combine(directory, "page");
                await Timed("pdftoppm", ["-f", "1", "-l", "1", "-singlefile", "-scale-to", "1400", "-png", input, prefix], "pdf-render", timeout.Token);
                input = prefix + ".png";
            }
            var prepared = Path.Combine(directory, "prepared.png");
            await Timed(OperatingSystem.IsWindows() ? "magick" : "convert",
                ["-limit", "memory", "96MiB", "-limit", "map", "96MiB", "-limit", "disk", "128MiB", "-limit", "thread", "1", "-define", "jpeg:size=1400x1400", input + "[0]", "-auto-orient", "-resize", "1400x1400>", "-background", "white", "-alpha", "remove", "-colorspace", "Gray", prepared], "image-prepare", timeout.Token);
            var tsv = await Timed("tesseract", [prepared, "stdout", "-l", "eng", "--psm", "11", "tsv"], "text-read", timeout.Token);
            var lines = ConfidentLines(tsv);
            var outcome = Assess(type, number, lines);
            // Sparse text can split an ID's labels and numbers into separate
            // blocks. Try a block-layout pass once, within the same deadline.
            if (outcome == "unreadable" && total.Elapsed < TimeSpan.FromSeconds(32))
            {
                var blockTsv = await Timed("tesseract", [prepared, "stdout", "-l", "eng", "--psm", "6", "tsv"], "text-read-block", timeout.Token);
                outcome = Assess(type, number, ConfidentLines(blockTsv));
            }
            // Try different image contrast for faint printing, without guessing
            // missing characters or relaxing the full-number comparison.
            if (outcome == "unreadable" && total.Elapsed < TimeSpan.FromSeconds(32))
            {
                var enhanced = Path.Combine(directory, "enhanced.png");
                await Timed(OperatingSystem.IsWindows() ? "magick" : "convert",
                    ["-limit", "memory", "96MiB", "-limit", "map", "96MiB", "-limit", "disk", "128MiB", "-limit", "thread", "1", prepared, "-normalize", "-unsharp", "0x1", enhanced], "image-enhance", timeout.Token);
                var enhancedTsv = await Timed("tesseract", [enhanced, "stdout", "-l", "eng", "--psm", "3", "tsv"], "text-read-enhanced", timeout.Token);
                outcome = Assess(type, number, ConfidentLines(enhancedTsv));
            }
            if (outcome == "mismatch") return new(false, $"The selected ID type or {IdentityDocumentTypes.NumberFor(type).Label} does not match the readable details. Correct it before continuing.");
            if (outcome == "unreadable") return new(false, $"Could not clearly read the full {IdentityDocumentTypes.NumberFor(type).Label} on the selected ID. Crop closely around the entire ID, avoid glare, and upload a sharper photo. Unreadable documents cannot continue to the camera.");
            return new(true, "ID details matched. You can open the camera.", Token("matched"));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Identity OCR timed out during {Stage} after {ElapsedMs} ms.", stage, total.ElapsedMilliseconds);
            return new(false, "ID reading timed out. Upload a clear, smaller JPG or PNG and try again. No unchecked ID was accepted.");
        }
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
            logger.LogInformation("Identity OCR total: {ElapsedMs} ms.", total.ElapsedMilliseconds);
        }
    }
    public static string[] ConfidentLines(string tsv) => tsv.Split('\n').Skip(1)
        .Select(line => line.TrimEnd('\r').Split('\t', 12))
        .Where(parts => parts.Length == 12 && parts[0] == "5")
        .GroupBy(parts => string.Join(':', parts.Take(5)))
        // A faint alphabetic label must not discard a clearly read number.
        // Keep original words; never remove an uncertain numeric group or a
        // standalone prefix/suffix letter and accidentally match a partial ID.
        .Where(group => group.All(parts =>
            !(parts[11].Any(char.IsAsciiDigit) || parts[11].Length == 1 && char.IsAsciiLetter(parts[11][0])) ||
            double.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var score) && score >= 65))
        .Select(group => string.Join(' ', group.Select(parts => parts[11]))).ToArray();
    private static async Task<string> Run(string executable, string[] arguments, CancellationToken ct, string temporaryDirectory)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["OMP_THREAD_LIMIT"] = "1";
        info.Environment["MAGICK_TEMPORARY_PATH"] = temporaryDirectory;
        info.WorkingDirectory = temporaryDirectory;
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
