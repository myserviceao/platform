using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProfileController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>
    /// PUT /api/profile/title - Update the user's display title
    /// </summary>
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

    /// <summary>
    /// POST /api/profile/logo - Upload a company logo image
    /// </summary>
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

        // Store in wwwroot/uploads/logos/
        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "logos");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"tenant-{tenantId}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var logoUrl = $"/uploads/logos/{fileName}?v={DateTime.UtcNow.Ticks}";

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant != null)
        {
            tenant.LogoUrl = logoUrl;
            await _db.SaveChangesAsync();
        }

        return Ok(new { logoUrl });
    }

    /// <summary>
    /// DELETE /api/profile/logo - Remove the company logo
    /// </summary>
    [HttpDelete("logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant == null) return NotFound();

        tenant.LogoUrl = null;
        await _db.SaveChangesAsync();

        return Ok(new { logoUrl = (string?)null });
    }
}

public class UpdateTitleRequest
{
    public string? Title { get; set; }
}
