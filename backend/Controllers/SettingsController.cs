using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SettingsController(AppDbContext db) { _db = db; }

    /// <summary>
    /// PUT /api/settings/theme
    /// Saves the user's theme preference to the tenant.
    /// </summary>
    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme([FromBody] ThemeRequest req)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant == null) return NotFound();

        tenant.Theme = req.Theme;
        await _db.SaveChangesAsync();

        return Ok(new { theme = tenant.Theme });
    }

    public class ThemeRequest
    {
        public string Theme { get; set; } = "dark";
    }
}
