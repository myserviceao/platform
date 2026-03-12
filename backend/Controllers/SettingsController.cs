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
        var userId = HttpContext.Session.GetInt32("userId");
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        user.Theme = req.Theme;
        await _db.SaveChangesAsync();

        return Ok(new { theme = user.Theme });
    }

    public class ThemeRequest
    {
        public string Theme { get; set; } = "dark";
    }
}
