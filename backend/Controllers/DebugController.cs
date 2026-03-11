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
    /// Fetch all PM jobs for a specific ST customer ID from the export 
    /// and return all date fields raw so we can see exactly what completedOn contains.
    /// </summary>
    [HttpGet("customer-pm-jobs")]
    public async Task<IActionResult> CustomerPmJobs([FromQuery] long customerId)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant?.StTenantId == null) return BadRequest("no ST tenant");

        var token = await _oauth.GetValidTokenAsync(tenantId.Value);
        if (token == null) return BadRequest("no token");

        // Fetch 18 months of jobs
        var allJobs = new List<object>();
        string? continueFrom = DateTime.UtcNow.AddMonths(-18).ToString("yyyy-MM-dd");
        int pages = 0;

        do
        {
            var json = await _client.GetJobsExportAsync(token, tenant.StTenantId, continueFrom);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) break;

            foreach (var job in data.EnumerateArray())
            {
                // Filter to this customer
                if (!job.TryGetProperty("customerId", out var cid)) continue;
                if (cid.ValueKind != JsonValueKind.Number) continue;
                if (cid.GetInt64() != customerId) continue;

                // Return ALL date/status fields raw
                allJobs.Add(new {
                    id = SafeGet(job, "id"),
                    jobNumber = SafeGet(job, "jobNumber"),
                    jobStatus = SafeGet(job, "jobStatus"),
                    jobTypeId = SafeGet(job, "jobTypeId"),
                    completedOn_raw = SafeGet(job, "completedOn"),
                    completedOn_kind = job.TryGetProperty("completedOn", out var co) ? co.ValueKind.ToString() : "MISSING",
                    modifiedOn_raw = SafeGet(job, "modifiedOn"),
                    createdOn_raw = SafeGet(job, "createdOn"),
                    actualStart = SafeGet(job, "actualStart"),
                    actualEnd = SafeGet(job, "actualEnd"),
                });
            }

            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;
            pages++;
            if (!hasMore || pages > 10) break;
        } while (continueFrom != null);

        return Ok(new { customerId, totalJobsFound = allJobs.Count, jobs = allJobs });
    }

    private static string SafeGet(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) ? v.GetRawText() : "null";
    }
}
