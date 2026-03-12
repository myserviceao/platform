using Microsoft.AspNetCore.Mvc;
using MyServiceAO.Services;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly AppDbContext _db;
    private const string SessionKey = "userId";
    private const string TenantSessionKey = "tenantId";

    public AuthController(AuthService auth, AppDbContext db)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Demo login: seed fresh demo data on each login
        if (req.Email?.ToLower() == DemoSeeder.DemoEmail)
        {
            var demoUser = await DemoSeeder.EnsureDemoTenantAndUserAsync(_db);
            if (!BCrypt.Net.BCrypt.Verify(req.Password, demoUser.PasswordHash))
                return Unauthorized(new { error = "Invalid credentials." });

            // Reset demo data on each login
            await DemoSeeder.ResetDemoDataAsync(_db, demoUser.TenantId);

            HttpContext.Session.SetInt32(SessionKey, demoUser.Id);
            HttpContext.Session.SetInt32(TenantSessionKey, demoUser.TenantId);

            return Ok(new
            {
                demoUser.Id,
                demoUser.Email,
                demoUser.FirstName,
                demoUser.LastName,
                demoUser.Role,
                demoUser.Title,
                Theme = demoUser.Theme ?? demoUser.Tenant.Theme ?? "black",
                Tenant = new
                {
                    demoUser.Tenant.Id,
                    demoUser.Tenant.Name,
                    demoUser.Tenant.Slug,
                    demoUser.Tenant.Theme,
                    demoUser.Tenant.LogoUrl
                }
            });
        }

        var user = await _auth.LoginAsync(req.Email, req.Password);
        if (user == null)
            return Unauthorized(new { error = "Invalid email or password." });

        HttpContext.Session.SetInt32(SessionKey, user.Id);
        HttpContext.Session.SetInt32(TenantSessionKey, user.TenantId);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.Title,
            Theme = user.Theme ?? user.Tenant.Theme ?? "black",
            Tenant = new
            {
                user.Tenant.Id,
                user.Tenant.Name,
                user.Tenant.Slug,
                user.Tenant.Theme,
                user.Tenant.LogoUrl
            }
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            var user = await _auth.RegisterAsync(
                req.CompanyName, req.Email, req.Password, req.FirstName, req.LastName);

            HttpContext.Session.SetInt32(SessionKey, user.Id);
            HttpContext.Session.SetInt32(TenantSessionKey, user.TenantId);

            return Ok(new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
                user.Title,
                Tenant = new
                {
                    user.Tenant.Id,
                    user.Tenant.Name,
                    user.Tenant.Slug,
                    user.Tenant.Theme,
                    user.Tenant.LogoUrl
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out." });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = HttpContext.Session.GetInt32(SessionKey);
        if (userId == null)
            return Unauthorized(new { error = "Not authenticated." });

        var user = await _auth.GetUserByIdAsync(userId.Value);
        if (user == null)
            return Unauthorized(new { error = "User not found." });

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.Title,
            Theme = user.Theme ?? user.Tenant.Theme ?? "black",
            Tenant = new
            {
                user.Tenant.Id,
                user.Tenant.Name,
                user.Tenant.Slug,
                user.Tenant.Theme,
                user.Tenant.LogoUrl
            }
        });
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string CompanyName, string Email, string Password, string FirstName, string LastName);
