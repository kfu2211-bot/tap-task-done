using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize(Policy = "AdminOnly")]
public class AdminIdentityModel(ApplicationDbContext db, IDataProtectionProvider protection, IWebHostEnvironment environment) : PageModel
{
    public record Item(long Id, string Name, string Status, string IdLast4, DateTime CreatedAt, bool HasCertification, bool HasSupporting);
    public List<Item> Items { get; private set; } = [];
    public bool DatabaseUnavailable { get; private set; }
    [BindProperty(SupportsGet = true)] public bool Embedded { get; set; }
    [TempData] public string? Notice { get; set; }
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound(); Response.Headers.CacheControl = "no-store";
        try { Items = await (from r in db.IdentityRequests.AsNoTracking() join u in db.Users.AsNoTracking() on r.UserId equals u.Id orderby r.Id descending select new Item(r.Id, ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(), r.Status, r.IdLast4, r.CreatedAt, r.Certification != null, r.SupportingDocument != null)).Take(100).ToListAsync(ct); }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { DatabaseUnavailable = true; Notice = "Verification database is not available. Check the local database setup and try again."; }
        return Page();
    }
    public async Task<IActionResult> OnPostReviewAsync(long id, string? decision, string? note, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound(); note = (note ?? "").Trim();
        if (AdminId <= 0 || decision is not ("approved" or "rejected") || note.Length > 500) return BadRequest();
        var changed = await db.IdentityRequests.Where(x => x.Id == id && x.Status == "pending").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, decision).SetProperty(x => x.ReviewNote, note).SetProperty(x => x.ReviewedAt, DateTime.UtcNow).SetProperty(x => x.ReviewedById, AdminId), ct);
        Notice = changed == 1 ? "Decision saved." : "Request was already reviewed or is unavailable."; return RedirectToPage(new { embedded = Embedded });
    }
    public async Task<IActionResult> OnGetDocumentAsync(long id, string kind, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound(); var r = await db.IdentityRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (r is null) return NotFound();
        var (data, type) = kind switch { "id" => (r.GovernmentId, r.GovernmentType), "certification" => (r.Certification, r.CertificationType), "supporting" => (r.SupportingDocument, r.SupportingType), _ => (null, null) };
        if (data is null || type is null) return NotFound();
        try { var bytes = protection.CreateProtector("tts.identity.documents.v1").Unprotect(data); Response.Headers.CacheControl = "no-store"; Response.Headers.XContentTypeOptions = "nosniff"; return File(bytes, "application/octet-stream", "verification-document" + (type == "application/pdf" ? ".pdf" : type == "image/png" ? ".png" : ".jpg")); }
        catch (System.Security.Cryptography.CryptographicException) { return StatusCode(503); }
    }
}
