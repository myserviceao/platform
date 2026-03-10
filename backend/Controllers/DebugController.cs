using Microsoft.AspNetCore.Mvc;
using MyServiceAO.Services.ServiceTitan;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly ServiceTitanOAuthService _oauth;
    private readonly ServiceTitanClient _client;

    public DebugController(ServiceTitanOAuthService oauth, ServiceTitanClient client)
    {
        _oauth = oauth;
        _client = client;
    }

    [HttpGet("st-raw")]
    public async Task<IActionResult> StRaw()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var token = await _oauth.GetValidTokenAsync(tenantId.Value);
        if (token == null) return BadRequest(new { error = "no token" });

        var tenant = await HttpContext.RequestServices
            .GetRequiredService<MyServiceAO.Data.AppDbContext>()
            .Tenants.FindAsync(tenantId.Value);

        if (tenant?.StTenantId == null) return BadRequest(new { error = "no st tenant id" });

        var from = DateTime.UtcNow.AddMonths(-2).ToString("yyyy-MM-dd");
        var invoiceRaw = await _client.GetInvoicesExportAsync(token, tenant.StTenantId, from);

        return Ok(new { stTenantId = tenant.StTenantId, invoiceRaw });
    }
}
