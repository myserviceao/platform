using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPut("title")]
    public async Task<IActionResult> UpdateTitle([FromBody] UpdateTitleRequest req)
    {
        var userId = HttpContext.Session.GetInt32("userId");
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        user.Title = req.Title?.Trim();
        await _db.SaveChangesAsync();

        return Ok(new { user.Title });
    }

    [HttpPost("logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { error = "File too large (max 2MB)" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".webp" && ext != ".svg")
            return BadRequest(new { error = "Only PNG, JPG, WebP, and SVG are allowed" });

        var contentType = ext switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        // Read file to base64 and store in DB
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant == null) return NotFound();

        tenant.LogoData = base64;
        tenant.LogoContentType = contentType;
        tenant.LogoUrl = $"/api/profile/logo/{tenantId}?v={DateTime.UtcNow.Ticks}";
        await _db.SaveChangesAsync();

        return Ok(new { logoUrl = tenant.LogoUrl });
    }

    /// <summary>
    /// GET /api/profile/logo/{tenantId} - Serve the logo from DB
    /// </summary>
    [HttpGet("logo/{tenantId}")]
    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> GetLogo(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null || string.IsNullOrEmpty(tenant.LogoData))
            return NotFound();

        var bytes = Convert.FromBase64String(tenant.LogoData);
        return File(bytes, tenant.LogoContentType ?? "image/png");
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant == null) return NotFound();

        tenant.LogoUrl = null;
        tenant.LogoData = null;
        tenant.LogoContentType = null;
        await _db.SaveChangesAsync();

        return Ok(new { logoUrl = (string?)null });
    }
}

public class UpdateTitleRequest
{
    public string? Title { get; set; }
}
