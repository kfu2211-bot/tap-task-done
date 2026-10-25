using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;

namespace tts.Pages;

[Authorize]
[RequestSizeLimit(6 * 1024 * 1024)]
public class ProfilePhotoModel(ApplicationDbContext db, IDataProtectionProvider protection,
    IdentityVerificationAvailability availability, IWebHostEnvironment environment, ILogger<ProfilePhotoModel> logger) : PageModel
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private IDataProtector Protector => protection.CreateProtector("TapTaskDone.ProfilePicture.v1", UserId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private void PrivateResponse()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    public static string? ImageType(byte[] data)
    {
        if (data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (data.Length >= 4 && data[0] == 255 && data[1] == 216 && data[2] == 255 && data[^2] == 255 && data[^1] == 217) return "image/jpeg";
        if (data.Length >= 16 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8)
            && (data.AsSpan(12, 4).SequenceEqual("VP8 "u8) || data.AsSpan(12, 4).SequenceEqual("VP8L"u8) || data.AsSpan(12, 4).SequenceEqual("VP8X"u8))) return "image/webp";
        return null;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PrivateResponse();
        if (UserId <= 0) return Challenge();
        var stored = await db.Users.AsNoTracking().Where(x => x.Id == UserId).Select(x => x.ProfilePicture).SingleOrDefaultAsync(ct);
        if (stored is null || stored.Length == 0) return new NoContentResult();
        try
        {
            var bytes = Protector.Unprotect(stored);
            var type = ImageType(bytes);
            if (type is null) return NotFound();
            return File(bytes, type);
        }
        catch (CryptographicException ex)
        { logger.LogWarning(ex, "Unable to decrypt profile picture"); return StatusCode(503, new { message = "Profile photo cannot be opened. Please contact support." }); }
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? photo, CancellationToken ct)
    {
        PrivateResponse();
        if (UserId <= 0) return Challenge();
        if (!environment.IsDevelopment() && !availability.Enabled)
            return StatusCode(503, new { message = "Secure photo storage needs persistent encryption keys configured first." });
        if (photo is null || photo.Length is <= 0 or > MaxBytes) return BadRequest(new { message = "Choose a JPG, PNG or WEBP image up to 5 MB." });
        using var memory = new MemoryStream();
        await using var stream = photo.OpenReadStream();
        await stream.CopyToAsync(memory, ct);
        if (memory.Length > MaxBytes) return BadRequest();
        var bytes = memory.ToArray();
        if (ImageType(bytes) is null) return BadRequest(new { message = "The file is not a supported JPG, PNG or WEBP image." });
        var encrypted = Protector.Protect(bytes);
        var updated = await db.Users.Where(x => x.Id == UserId).ExecuteUpdateAsync(set => set.SetProperty(x => x.ProfilePicture, encrypted), ct);
        if (updated == 0) return NotFound();
        return new JsonResult(new { message = "Profile photo saved." });
    }

    public async Task<IActionResult> OnPostRemoveAsync(CancellationToken ct)
    {
        PrivateResponse();
        if (UserId <= 0) return Challenge();
        var updated = await db.Users.Where(x => x.Id == UserId).ExecuteUpdateAsync(set => set.SetProperty(x => x.ProfilePicture, (byte[]?)null), ct);
        return updated == 0 ? NotFound() : new JsonResult(new { message = "Profile photo removed." });
    }
}
