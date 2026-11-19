using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize]
[RequestSizeLimit(28 * 1024 * 1024)]
public class IdentityVerificationModel(ApplicationDbContext db, IDataProtectionProvider protection, IdentityVerificationAvailability availability, IWebHostEnvironment environment, ILogger<IdentityVerificationModel> logger, IdentityIdOcr idOcr) : PageModel
{
    public bool IsDevelopment => environment.IsDevelopment();
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public string Status { get; private set; } = "not verified";
    public string? ReviewNote { get; private set; }
    public string CaptureToken { get; private set; } = "";
    public string CaptureSteps { get; private set; } = "[]";
    [TempData(Key = "IdentityVerification.Notice")] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!availability.Enabled) return NotFound();
        var challenge = IdentityCaptureChallenge.Create(UserId);
        CaptureToken = challenge.Protect(protection);
        CaptureSteps = System.Text.Json.JsonSerializer.Serialize(challenge.Steps);
        try { var latest = await db.IdentityRequests.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.Id).Select(x => new { x.Status, x.ReviewNote }).FirstOrDefaultAsync(ct); Status = latest?.Status ?? "not verified"; ReviewNote = latest?.ReviewNote; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Identity verification unavailable"); Notice = "Verification is temporarily unavailable. Please try again later."; }
        return Page();
    }
    public async Task<IActionResult> OnPostCheckIdAsync(string? idNumber, string? documentType, IFormFile? governmentId, bool consent, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!availability.Enabled) return NotFound();
        if (UserId <= 0 || !consent || !IdentityDocumentTypes.Supports(documentType) || !IdentityDocumentTypes.ValidNumber(idNumber) || governmentId is null)
            return new JsonResult(new { success = false, message = "Choose your ID type, enter its printed number, upload the ID and agree to identity review first." }) { StatusCode = 422 };
        try
        {
            var file = await ReadFile(governmentId, ct);
            var result = await idOcr.CheckAsync(UserId, documentType!, idNumber!, file.Bytes, file.Type, ct);
            return new JsonResult(new { success = result.Success, message = result.Message, token = result.Token, canManualReview = result.CanManualReview }) { StatusCode = result.Success || result.CanManualReview ? 200 : 422 };
        }
        catch (ArgumentException) { return new JsonResult(new { success = false, message = "Choose a valid JPG, PNG or PDF no larger than 5 MB." }) { StatusCode = 422 }; }
    }
    public async Task<IActionResult> OnPostAsync(string? idNumber, IFormFile? governmentId, IFormFile? certification, IFormFile? supportingDocument, IFormFile? selfie, bool consent, string? documentType, string? captureToken, string? idCheckToken, bool manualIdReview, CancellationToken ct)
    {
        if (!availability.Enabled) return NotFound();
        if (!IdentityDocumentTypes.Supports(documentType)) { Notice = "Choose a supported government photo ID type. Other images are not accepted as identity documents."; return RedirectToPage(); }
        idNumber = (idNumber ?? "").Trim();
        if (UserId <= 0 || !IdentityDocumentTypes.ValidNumber(idNumber) || governmentId is null) { Notice = "Enter the exact ID number printed on your selected ID, using letters, digits, spaces or hyphens, and choose the matching document."; return RedirectToPage(); }
        idNumber = IdentityDocumentTypes.NormalizeNumber(idNumber);
        var challenge = IdentityCaptureChallenge.Read(captureToken, UserId, protection);
        if (challenge is null) { Notice = "Your camera session expired. Reload and complete the guided face capture again."; return RedirectToPage(); }
        if (!consent || selfie is null || selfie.Length == 0) { Notice = "Take a selfie and consent to manual identity review before submitting."; return RedirectToPage(); }
        try {
            var captures = new List<(byte[] Bytes, string Type)>();
            for (var i = 0; i < challenge.Steps.Length; i++)
            {
                var frame = Request.Form.Files.GetFile($"captureStep{i}");
                if (frame is null || frame.Length > 1024 * 1024) { Notice = "Complete all camera movements before submitting."; return RedirectToPage(); }
                var capture = await ReadFile(frame, ct);
                if (capture.Type is not ("image/jpeg" or "image/png")) throw new ArgumentException();
                captures.Add(capture);
            }
            var selfieFile = await ReadFile(selfie, ct);
            if (selfieFile.Type is not ("image/jpeg" or "image/png")) throw new ArgumentException();
            var idFile = await ReadFile(governmentId, ct); (byte[] Bytes, string Type)? cert = certification is null || certification.Length == 0 ? null : await ReadFile(certification, ct); (byte[] Bytes, string Type)? other = supportingDocument is null || supportingDocument.Length == 0 ? null : await ReadFile(supportingDocument, ct);
            var checkMode = idOcr.ReceiptMode(idCheckToken, UserId, documentType!, idNumber, idFile.Bytes);
            if (checkMode == "manual" && !manualIdReview) { Notice = "Choose manual review or check your ID again before submitting."; return RedirectToPage(); }
            if (checkMode is null)
            {
                var check = await idOcr.CheckAsync(UserId, documentType!, idNumber, idFile.Bytes, idFile.Type, ct);
                if (!check.Success) { Notice = check.Message; return RedirectToPage(); }
                checkMode = "matched";
            }
            // TiDB does not support SERIALIZABLE. Lock the stable owner row to
            // serialize this user's submissions, including their first request.
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var owner = await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {UserId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            if (owner.Count != 1) { Notice = "Your account could not be found. Sign in again before submitting."; return RedirectToPage(); }
            // A locking read sees the latest committed request after waiting for
            // the owner lock, rather than an older repeatable-read snapshot.
            var current = await db.IdentityRequests.FromSqlInterpolated($"SELECT * FROM identity_requests WHERE UserId = {UserId} ORDER BY Id DESC LIMIT 1 FOR UPDATE").AsNoTracking().ToListAsync(ct);
            var latest = current.SingleOrDefault()?.Status;
            if (latest is "pending" or "approved") { Notice = "A request is already pending or approved."; return RedirectToPage(); }
            var protector = protection.CreateProtector("tts.identity.documents.v1");
            var request = new IdentityRequest { UserId = UserId, IdLast4 = idNumber[^4..], Status = "pending", CreatedAt = DateTime.UtcNow };
            db.IdentityRequests.Add(request);
            await db.SaveChangesAsync(ct);
            void AddResource(string kind, (byte[] Bytes, string Type) file) => db.IdentityResources.Add(new IdentityResource { IdentityRequestId = request.Id, Kind = kind, ContentType = file.Type, EncryptedData = protector.Protect(file.Bytes), CreatedAt = DateTime.UtcNow });
            AddResource("id", idFile);
            AddResource("document-type", (System.Text.Encoding.UTF8.GetBytes(documentType!), "text/plain"));
            AddResource("id-number", (System.Text.Encoding.UTF8.GetBytes(idNumber), "text/plain"));
            AddResource("ocr-check", (System.Text.Encoding.UTF8.GetBytes(checkMode), "text/plain"));
            AddResource("capture-sequence", (System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(challenge.Steps)), "text/plain"));
            for (var i = 0; i < captures.Count; i++) AddResource($"capture-{i}", captures[i]);
            AddResource("selfie", selfieFile);
            AddResource("consent", (System.Text.Encoding.UTF8.GetBytes("Identity review consent v2: encrypted ID, full ID number, selfie and guided capture frames for manual review; accepted at submission. Browser movement checks are not certified liveness or document authentication."), "text/plain"));
            if (cert is not null) AddResource("certification", cert.Value);
            if (other is not null) AddResource("supporting", other.Value);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = checkMode == "manual" ? "Submitted for manual review. Your ID number has not been automatically verified; an admin will compare it with your ID." : "Verification request submitted for admin review.";
        } catch (ArgumentException) { Notice = "Each file must be a valid JPG, PNG or PDF no larger than 5 MB."; }
          catch (Exception ex) when (ex is MySqlConnector.MySqlException or DbUpdateException or InvalidOperationException) { logger.LogError(ex, "Unable to save identity request"); Notice = "Could not save the request. Please try again later."; }
        return RedirectToPage();
    }
    private static async Task<(byte[] Bytes, string Type)> ReadFile(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 5 * 1024 * 1024) throw new ArgumentException();
        using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); var bytes = stream.ToArray();
        var type = bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg" : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png" : bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ? "application/pdf" : null;
        return type is null ? throw new ArgumentException() : (bytes, type);
    }
}
