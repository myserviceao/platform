using Microsoft.AspNetCore.Mvc;
using MyServiceAO.Data;
using MyServiceAO.Services.ServiceTitan;
using System.Text.Json;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ServiceTitanOAuthService _oauth;

    public DebugController(AppDbContext db, ServiceTitanClient client, ServiceTitanOAuthService oauth)
    {
        _db = db; _client = client; _oauth = oauth;
    }

    /// <summary>
    /// Returns raw export fields for the first 10 completed jobs on or after 2025-11-01
    /// so we can inspect what completedOn/modifiedOn actually look like in export data.
    /// </summary>
    [HttpGet("job-fields")]
    public async Task<IActionResult> JobFields()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant?.StTenantId == null) return BadRequest("no ST tenant");

        var token = await _oauth.GetValidTokenAsync(tenantId.Value);
        if (token == null) return BadRequest("no token");

        var json = await _client.GetJobsExportAsync(token, tenant.StTenantId, "2025-11-01");
        var doc = JsonDocument.Parse(json);
        var jobs = doc.RootElement.GetProperty("data").EnumerateArray()
            .Take(20)
            .Select(j => new {
                id = j.TryGetProperty("id", out var id) ? id.GetRawText() : "null",
                jobNumber = j.TryGetProperty("jobNumber", out var jn) ? jn.GetRawText() : "null",
                jobStatus = j.TryGetProperty("jobStatus", out var js) ? js.GetString() : "null",
                completedOn = j.TryGetProperty("completedOn", out var co) ? co.GetRawText() : "MISSING",
                modifiedOn = j.TryGetProperty("modifiedOn", out var mo) ? mo.GetRawText() : "MISSING",
                jobTypeId = j.TryGetProperty("jobTypeId", out var jt) ? jt.GetRawText() : "null",
                customerId = j.TryGetProperty("customerId", out var cid) ? cid.GetRawText() : "null",
                allKeys = string.Join(",", j.EnumerateObject().Select(p => p.Name))
            })
            .ToList();

        return Ok(jobs);
    }
}
