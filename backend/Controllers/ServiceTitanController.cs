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

    /// <summary>
    /// GET /api/servicetitan/status
    /// Returns whether the current tenant is connected to ServiceTitan.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var connected = await _oauth.IsConnectedAsync(tenantId.Value);
        return Ok(new { connected });
    }

    /// <summary>
    /// POST /api/servicetitan/connect
    /// Tenant provides their ST credentials. We get a token and immediately sync.
    /// Body: { clientId, clientSecret, stTenantId }
    /// </summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.ClientId) ||
            string.IsNullOrWhiteSpace(req.ClientSecret) ||
            string.IsNullOrWhiteSpace(req.StTenantId))
            return BadRequest(new { error = "clientId, clientSecret, and stTenantId are required" });

        var connected = await _oauth.ConnectAsync(tenantId.Value, req.ClientId, req.ClientSecret, req.StTenantId);
        if (!connected)
            return BadRequest(new { error = "Could not authenticate with ServiceTitan. Check your credentials." });

        // Immediately sync after connecting
        var syncResult = await _sync.SyncAllAsync(tenantId.Value);

        return Ok(new
        {
            connected = true,
            sync = new
            {
                success = syncResult.Success,
                jobsSynced = syncResult.JobsSynced,
                syncedAt = syncResult.SyncedAt,
                error = syncResult.Error
            }
        });
    }

    /// <summary>
    /// POST /api/servicetitan/sync
    /// Manually trigger a data sync for the current tenant.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var result = await _sync.SyncAllAsync(tenantId.Value);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new
        {
            jobsSynced = result.JobsSynced,
            syncedAt = result.SyncedAt
        });
    }

    /// <summary>
    /// POST /api/servicetitan/disconnect
    /// Removes stored ST credentials for the tenant.
    /// </summary>
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