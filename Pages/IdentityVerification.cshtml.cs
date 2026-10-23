using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize]
[RequestSizeLimit(16 * 1024 * 1024)]
public class IdentityVerificationModel(ApplicationDbContext db, IDataProtectionProvider protection, IdentityVerificationAvailability availability, IWebHostEnvironment environment, ILogger<IdentityVerificationModel> logger) : PageModel
{
    public bool IsDevelopment => environment.IsDevelopment();
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public string Status { get; private set; } = "not verified";
    public string? ReviewNote { get; private set; }
    [TempData] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!availability.Enabled) return NotFound();
        try { var latest = await db.IdentityRequests.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.Id).Select(x => new { x.Status, x.ReviewNote }).FirstOrDefaultAsync(ct); Status = latest?.Status ?? "not verified"; ReviewNote = latest?.ReviewNote; }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { logger.LogWarning(ex, "Identity verification unavailable"); Notice = "Verification is temporarily unavailable. Please try again later."; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string? idNumber, IFormFile? governmentId, IFormFile? certification, IFormFile? supportingDocument, CancellationToken ct)
    {
        if (!availability.Enabled) return NotFound();
        idNumber = (idNumber ?? "").Trim();
        if (UserId <= 0 || idNumber.Length is < 4 or > 50 || governmentId is null) { Notice = "Enter an ID number and choose a government ID file."; return RedirectToPage(); }
        try {
            var idFile = await ReadFile(governmentId, ct); (byte[] Bytes, string Type)? cert = certification is null || certification.Length == 0 ? null : await ReadFile(certification, ct); (byte[] Bytes, string Type)? other = supportingDocument is null || supportingDocument.Length == 0 ? null : await ReadFile(supportingDocument, ct);
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var latest = await db.IdentityRequests.Where(x => x.UserId == UserId).OrderByDescending(x => x.Id).Select(x => x.Status).FirstOrDefaultAsync(ct);
            if (latest is "pending" or "approved") { Notice = "A request is already pending or approved."; return RedirectToPage(); }
            var protector = protection.CreateProtector("tts.identity.documents.v1");
            var request = new IdentityRequest { UserId = UserId, IdLast4 = idNumber[^4..], Status = "pending", CreatedAt = DateTime.UtcNow };
            db.IdentityRequests.Add(request);
            await db.SaveChangesAsync(ct);
            void AddResource(string kind, (byte[] Bytes, string Type) file) => db.IdentityResources.Add(new IdentityResource { IdentityRequestId = request.Id, Kind = kind, ContentType = file.Type, EncryptedData = protector.Protect(file.Bytes), CreatedAt = DateTime.UtcNow });
            AddResource("id", idFile);
            if (cert is not null) AddResource("certification", cert.Value);
            if (other is not null) AddResource("supporting", other.Value);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); Notice = "Verification request submitted for admin review.";
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
