using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize(Policy = "AdminOnly")]
public class AdminIdentityModel(ApplicationDbContext db, IDataProtectionProvider protection, IdentityVerificationAvailability availability) : PageModel
{
    public record Item(long Id, string Name, string Status, string IdLast4, DateTime CreatedAt, bool HasCertification, bool HasSupporting, bool IsLatest) { public bool HasSelfie { get; init; } public string? GovernmentType { get; init; } }
    public List<Item> Items { get; private set; } = [];
    public string Queue { get; private set; } = "pending";
    public bool DatabaseUnavailable { get; private set; }
    public Dictionary<long, string> DocumentTypes { get; private set; } = [];
    public Dictionary<long, string[]> CaptureSequences { get; private set; } = [];
    public HashSet<long> ManualNumberReviews { get; private set; } = [];
    public bool ReviewEnabled => availability.Enabled;
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [TempData(Key = "AdminIdentity.Notice")] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<IActionResult> OnGetAsync(CancellationToken ct, long? recordId = null, string? queue = null)
    {
        Response.Headers.CacheControl = "no-store";
        if (!availability.Enabled) Notice = "Document review is paused until secure document storage is configured. Existing requests are listed below.";
        Queue = queue is "approved" or "rejected" ? queue : "pending";
        try { Items = await (from r in db.IdentityRequests.AsNoTracking() join u in db.Users.AsNoTracking() on r.UserId equals u.Id where recordId.HasValue ? r.Id == recordId.Value : Queue == "rejected" ? (r.Status == "rejected" || r.Status == "resubmit") : r.Status == Queue orderby r.Id descending select new Item(r.Id, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(), r.Status, r.IdLast4, r.CreatedAt, r.Certification != null, r.SupportingDocument != null, !db.IdentityRequests.Any(newer => newer.UserId == r.UserId && newer.Id > r.Id))).Take(100).ToListAsync(ct); }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1146 && !availability.Enabled)
        { Notice = "No verification requests have been set up. Document submissions and review are paused until secure document storage is configured."; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { DatabaseUnavailable = true; Notice = "Verification requests could not be loaded. Please check the database setup and try again."; }
        if (Items.Count > 0)
        {
            try
            {
                var ids = Items.Select(x => x.Id).ToList();
                var ocrChecks = await db.IdentityResources.AsNoTracking().Where(x => ids.Contains(x.IdentityRequestId) && x.Kind == "ocr-check").Select(x => new { x.IdentityRequestId, x.EncryptedData }).ToListAsync(ct);
                foreach (var check in ocrChecks)
                {
                    try { if (System.Text.Encoding.UTF8.GetString(protection.CreateProtector("tts.identity.documents.v1").Unprotect(check.EncryptedData)) == "manual") ManualNumberReviews.Add(check.IdentityRequestId); }
                    catch (System.Security.Cryptography.CryptographicException) { ManualNumberReviews.Add(check.IdentityRequestId); }
                }
                var types = await db.IdentityResources.AsNoTracking().Where(x => ids.Contains(x.IdentityRequestId) && x.Kind == "document-type").Select(x => new { x.IdentityRequestId, x.EncryptedData }).ToListAsync(ct);
                foreach (var type in types)
                {
                    try { DocumentTypes[type.IdentityRequestId] = System.Text.Encoding.UTF8.GetString(protection.CreateProtector("tts.identity.documents.v1").Unprotect(type.EncryptedData)); }
                    catch (System.Security.Cryptography.CryptographicException) { DocumentTypes[type.IdentityRequestId] = "Unavailable — inspect the submitted document"; }
                }
                var sequences = await db.IdentityResources.AsNoTracking().Where(x => ids.Contains(x.IdentityRequestId) && x.Kind == "capture-sequence").ToListAsync(ct);
                foreach (var sequence in sequences)
                {
                    try { CaptureSequences[sequence.IdentityRequestId] = System.Text.Json.JsonSerializer.Deserialize<string[]>(protection.CreateProtector("tts.identity.documents.v1").Unprotect(sequence.EncryptedData)) ?? []; }
                    catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException) { }
                }
                var resources = await db.IdentityResources.AsNoTracking().Where(x => ids.Contains(x.IdentityRequestId))
                    .Select(x => new { x.IdentityRequestId, x.Kind, x.ContentType }).ToListAsync(ct);
                Items = Items.Select(item => item with {
                    HasSelfie = resources.Any(x => x.IdentityRequestId == item.Id && x.Kind == "selfie"),
                    GovernmentType = resources.FirstOrDefault(x => x.IdentityRequestId == item.Id && x.Kind == "id")?.ContentType,
                    HasCertification = item.HasCertification || resources.Any(x => x.IdentityRequestId == item.Id && x.Kind == "certification"),
                    HasSupporting = item.HasSupporting || resources.Any(x => x.IdentityRequestId == item.Id && x.Kind == "supporting")
                }).ToList();
            }
            catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException)
            { Notice = "Requests are shown, but supplemental documents could not be loaded. Please check document storage before reviewing attachments."; }
        }
        return Page();
    }
    public async Task<IActionResult> OnPostReviewAsync(long id, string? decision, string? note, bool matchConfirmed, bool faceVisibleConfirmed, bool documentConfirmed, bool movementConfirmed, string? numberOnDocument, string? typeOnDocument, CancellationToken ct)
    {
        IActionResult Result(bool success, string message, int statusCode = 422)
        {
            if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
                return new JsonResult(new { success, message, id, status = success ? decision : null }) { StatusCode = success ? 200 : statusCode };
            Notice = message;
            return success ? RedirectToPage(new { embedded = Embedded }) : RedirectToPage(new { embedded = Embedded, recordId = id });
        }
        if (!availability.Enabled) return NotFound(); note = (note ?? "").Trim();
        if (AdminId <= 0 || decision is not ("approved" or "rejected" or "resubmit") || note.Length > 500 || (decision != "approved" && string.IsNullOrWhiteSpace(note))) { return Result(false, "Add a reason when rejecting or requesting a new submission (up to 500 characters)."); }
        if (decision == "approved" && (!matchConfirmed || !faceVisibleConfirmed)) { return Result(false, "Confirm that the photos show the same person and that the selfie clearly shows the full face without glasses or a face mask."); }
        if (decision == "approved" && !await db.IdentityResources.AnyAsync(x => x.IdentityRequestId == id && x.Kind == "selfie", ct)) { return Result(false, "This request has no selfie. Request a new submission with a selfie before approving."); }
        if (decision == "approved")
        {
            if (!documentConfirmed || !movementConfirmed || !IdentityDocumentTypes.ValidNumber(numberOnDocument) || !IdentityDocumentTypes.Supports(typeOnDocument))
            { return Result(false, "Read the ID type and full number from the document and confirm document and movement checks before approving."); }
            var evidence = await db.IdentityResources.AsNoTracking().Where(x => x.IdentityRequestId == id &&
                (x.Kind == "id" || x.Kind == "id-number" || x.Kind == "document-type" || x.Kind == "capture-sequence" || x.Kind.StartsWith("capture-"))).ToListAsync(ct);
            var protector = protection.CreateProtector("tts.identity.documents.v1");
            try
            {
                string? ReadText(string kind) => evidence.FirstOrDefault(x => x.Kind == kind) is { } resource ? System.Text.Encoding.UTF8.GetString(protector.Unprotect(resource.EncryptedData)) : null;
                if (ReadText("id-number") != IdentityDocumentTypes.NormalizeNumber(numberOnDocument!) || ReadText("document-type") != typeOnDocument)
                { return Result(false, "The ID type or number read from the document does not match the submission. Request correction or reject the request."); }
                var steps = System.Text.Json.JsonSerializer.Deserialize<string[]>(ReadText("capture-sequence") ?? "[]");
                if (steps?.Length != 5 || !evidence.Any(x => x.Kind == "id") || Enumerable.Range(0, 5).Any(i => !evidence.Any(x => x.Kind == $"capture-{i}")))
                { return Result(false, "Complete camera evidence is unavailable. Request a new submission before approving."); }
                foreach (var image in evidence.Where(x => x.Kind == "id" || Enumerable.Range(0, 5).Any(i => x.Kind == $"capture-{i}"))) protector.Unprotect(image.EncryptedData);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
            { return Result(false, "Verification evidence cannot be read. Request resubmission before approval."); }
        }
        var changed = await db.IdentityRequests.Where(x => x.Id == id && x.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, decision).SetProperty(x => x.ReviewNote, note).SetProperty(x => x.ReviewedAt, DateTime.UtcNow).SetProperty(x => x.ReviewReadAt, (DateTime?)null).SetProperty(x => x.ReviewedById, AdminId), ct);
        var message = decision == "resubmit" ? "Resubmission requested. Moved to Rejected / Resubmit. The user will see your reason and can submit new documents."
            : decision == "rejected" ? "Request rejected. Moved to Rejected / Resubmit. The user will see your reason." : "Request approved. Moved to Approved.";
        return Result(changed == 1, changed == 1 ? message : "Request was already reviewed or is unavailable. Refresh the queue to see its current status.", 409);
    }
    public async Task<IActionResult> OnPostRenewAsync(long id, string? note, CancellationToken ct)
    {
        if (!availability.Enabled || AdminId <= 0) return NotFound();
        note = (note ?? "").Trim();
        if (note.Length is 0 or > 500)
        {
            Notice = "Explain which document needs renewal and why (up to 500 characters).";
            return RedirectToPage(new { embedded = Embedded, queue = "approved", recordId = id });
        }

        var ownerId = await db.IdentityRequests.Where(r => r.Id == id).Select(r => (int?)r.UserId).SingleOrDefaultAsync(ct);
        if (ownerId is null) { Notice = "Verification request is unavailable."; return RedirectToPage(new { embedded = Embedded, queue = "approved" }); }
        // Use the same owner-first lock order as submission, without requiring
        // SERIALIZABLE (unsupported by TiDB).
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM tbl_user WHERE Id = {ownerId.Value} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var latest = (await db.IdentityRequests.FromSqlInterpolated($"SELECT * FROM identity_requests WHERE UserId = {ownerId.Value} ORDER BY Id DESC LIMIT 1 FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        if (latest?.Id != id || latest.Status != "approved")
        {
            Notice = "This request is no longer the user's current approved verification.";
            return RedirectToPage(new { embedded = Embedded, queue = "approved" });
        }

        var now = DateTime.UtcNow;
        var changed = await db.IdentityRequests.Where(r => r.Id == id && r.Status == "approved")
            .ExecuteUpdateAsync(update => update
                .SetProperty(r => r.Status, "resubmit")
                .SetProperty(r => r.ReviewNote, note)
                .SetProperty(r => r.ReviewedAt, now)
                .SetProperty(r => r.ReviewReadAt, (DateTime?)null)
                .SetProperty(r => r.ReviewedById, AdminId), ct);
        await transaction.CommitAsync(ct);
        Notice = changed == 1
            ? "Renewal requested. The user will see a notification and can submit updated documents."
            : "This request is no longer the user's current approved verification.";
        return RedirectToPage(new { embedded = Embedded, queue = changed == 1 ? "rejected" : "approved" });
    }

    public async Task<IActionResult> OnGetDocumentAsync(long id, string kind, CancellationToken ct)
    {
        if (!availability.Enabled || kind is not ("id" or "certification" or "supporting" or "selfie" or "capture-0" or "capture-1" or "capture-2" or "capture-3" or "capture-4")) return NotFound();
        var resource = await db.IdentityResources.AsNoTracking().Where(x => x.IdentityRequestId == id && x.Kind == kind).Select(x => new { x.EncryptedData, x.ContentType }).SingleOrDefaultAsync(ct);
        byte[]? data = resource?.EncryptedData; string? type = resource?.ContentType;
        if (resource is null) {
            var r = await db.IdentityRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (r is null) return NotFound();
            (data, type) = kind switch { "id" => (r.GovernmentId, r.GovernmentType), "certification" => (r.Certification, r.CertificationType), "supporting" => (r.SupportingDocument, r.SupportingType), _ => (null, null) };
        }
        if (data is null || type is null) return NotFound();
        try { var bytes = protection.CreateProtector("tts.identity.documents.v1").Unprotect(data); Response.Headers.CacheControl = "no-store"; Response.Headers.XContentTypeOptions = "nosniff"; if (type is "image/jpeg" or "image/png") return File(bytes, type); return File(bytes, "application/octet-stream", "verification-document.pdf"); }
        catch (System.Security.Cryptography.CryptographicException) { return StatusCode(503); }
    }
}
