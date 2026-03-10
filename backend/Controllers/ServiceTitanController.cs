using Microsoft.AspNetCore.Mvc;
using MyServiceAO.Services.ServiceTitan;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/servicetitan")]
public class ServiceTitanController : ControllerBase
{
    private readonly ServiceTitanOAuthService _oauth;
    private readonly ServiceTitanSyncService _sync;

    public ServiceTitanController(ServiceTitanOAuthService oauth, ServiceTitanSyncService sync)
    {
        _oauth = oauth;
        _sync = sync;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        return Ok(new { connected = await _oauth.IsConnectedAsync(tenantId.Value) });
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var connected = await _oauth.ConnectAsync(tenantId.Value, req.ClientId, req.ClientSecret, req.StTenantId);
        if (!connected)
            return BadRequest(new { error = "Could not authenticate with ServiceTitan. Check your credentials." });

        var syncResult = await _sync.SyncAllAsync(tenantId.Value);
        return Ok(new { connected = true, sync = syncResult });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        var result = await _sync.SyncAllAsync(tenantId.Value);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result);
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        await _oauth.DisconnectAsync(tenantId.Value);
        return Ok(new { disconnected = true });
    }
}

public record ConnectRequest(string ClientId, string ClientSecret, string StTenantId);