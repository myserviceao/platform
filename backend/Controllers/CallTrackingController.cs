using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Services.ServiceTitan;
using System.Text.Json;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/calls")]
public class CallTrackingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ServiceTitanSyncService _sync;
    private readonly ILogger<CallTrackingController> _logger;

    public CallTrackingController(AppDbContext db, ServiceTitanClient client, ServiceTitanSyncService sync, ILogger<CallTrackingController> logger)
    {
        _db = db;
        _client = client;
        _sync = sync;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetCallSummary([FromQuery] int days = 30)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        // Demo tenant: return demo data
        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant?.Slug == "demo-hvac")
            return Ok(DemoCallData(days));

        try
        {
            var token = await _sync.GetTokenAsync(tenantId.Value);
            if (token == null || string.IsNullOrEmpty(tenant?.StTenantId))
                return BadRequest(new { error = "Not connected to ServiceTitan" });

            var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");
            
            // Fetch calls from ST - page through all results
            var allCalls = new List<JsonElement>();
            int page = 1;
            bool hasMore = true;
            while (hasMore && page <= 10) // max 10 pages = 2000 calls
            {
                var raw = await _client.GetCallsAsync(token, tenant!.StTenantId!, since, page, 200);
                var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var call in data.EnumerateArray())
                        allCalls.Add(call.Clone());
                }
                hasMore = doc.RootElement.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
                page++;
            }

            return Ok(BuildCallSummary(allCalls, days));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Calls] Failed to fetch call data");
            return Ok(new { error = ex.InnerException?.Message ?? ex.Message });
        }
    }

    private object BuildCallSummary(List<JsonElement> calls, int days)
    {
        var now = DateTime.UtcNow;
        int inbound = 0, outbound = 0, booked = 0, abandoned = 0, unbooked = 0;

        var byAgent = new Dictionary<string, (int Inbound, int Outbound, int Booked, int Total)>();
        var byDay = new Dictionary<string, (int Inbound, int Outbound, int Booked)>();

        foreach (var call in calls)
        {
            var direction = call.TryGetProperty("direction", out var d) ? d.GetString() ?? "" : "";
            var callType = call.TryGetProperty("callType", out var ct) && ct.ValueKind == JsonValueKind.String ? ct.GetString() : null;
            var agentName = "Unknown";
            if (call.TryGetProperty("agent", out var ag) && ag.ValueKind == JsonValueKind.Object)
                agentName = ag.TryGetProperty("name", out var an) ? an.GetString() ?? "Unknown" : "Unknown";

            var createdOn = call.TryGetProperty("createdOn", out var co) && co.ValueKind == JsonValueKind.String
                ? DateTime.Parse(co.GetString()!) : now;
            var dayKey = createdOn.ToString("yyyy-MM-dd");

            var isInbound = direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase);
            var isOutbound = direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase);
            var isBooked = callType?.Equals("Booked", StringComparison.OrdinalIgnoreCase) == true;

            if (isInbound) inbound++;
            if (isOutbound) outbound++;
            if (isBooked) booked++;
            if (callType?.Equals("Abandoned", StringComparison.OrdinalIgnoreCase) == true) abandoned++;
            if (callType?.Equals("Unbooked", StringComparison.OrdinalIgnoreCase) == true) unbooked++;

            // By agent
            if (!byAgent.ContainsKey(agentName)) byAgent[agentName] = (0, 0, 0, 0);
            var a = byAgent[agentName];
            byAgent[agentName] = (
                a.Inbound + (isInbound ? 1 : 0),
                a.Outbound + (isOutbound ? 1 : 0),
                a.Booked + (isBooked ? 1 : 0),
                a.Total + 1
            );

            // By day
            if (!byDay.ContainsKey(dayKey)) byDay[dayKey] = (0, 0, 0);
            var dd = byDay[dayKey];
            byDay[dayKey] = (
                dd.Inbound + (isInbound ? 1 : 0),
                dd.Outbound + (isOutbound ? 1 : 0),
                dd.Booked + (isBooked ? 1 : 0)
            );
        }

        return new
        {
            TotalCalls = calls.Count,
            Inbound = inbound,
            Outbound = outbound,
            Booked = booked,
            Abandoned = abandoned,
            Unbooked = unbooked,
            BookingRate = inbound > 0 ? Math.Round((double)booked / inbound * 100, 1) : 0,
            Days = days,
            ByAgent = byAgent.Select(kv => new
            {
                Agent = kv.Key,
                kv.Value.Inbound,
                kv.Value.Outbound,
                kv.Value.Booked,
                kv.Value.Total,
                BookingRate = kv.Value.Inbound > 0 ? Math.Round((double)kv.Value.Booked / kv.Value.Inbound * 100, 1) : 0
            }).OrderByDescending(x => x.Total).ToList(),
            ByDay = byDay.OrderBy(kv => kv.Key).Select(kv => new
            {
                Date = kv.Key,
                kv.Value.Inbound,
                kv.Value.Outbound,
                kv.Value.Booked
            }).ToList()
        };
    }

    private object DemoCallData(int days)
    {
        var rng = new Random(42);
        var agents = new[] { "Sarah Johnson", "Mike Chen", "Emily Davis", "Brandon Wiederhold" };
        var byAgent = agents.Select(a => new
        {
            Agent = a,
            Inbound = rng.Next(20, 80),
            Outbound = rng.Next(10, 40),
            Booked = rng.Next(15, 50),
            Total = 0,
            BookingRate = 0.0
        }).Select(a => new
        {
            a.Agent, a.Inbound, a.Outbound, a.Booked,
            Total = a.Inbound + a.Outbound,
            BookingRate = a.Inbound > 0 ? Math.Round((double)a.Booked / a.Inbound * 100, 1) : 0
        }).ToList();

        var byDay = Enumerable.Range(0, Math.Min(days, 14)).Select(d =>
        {
            var date = DateTime.UtcNow.AddDays(-d).ToString("yyyy-MM-dd");
            return new { Date = date, Inbound = rng.Next(5, 20), Outbound = rng.Next(2, 10), Booked = rng.Next(3, 12) };
        }).Reverse().ToList();

        var totalInbound = byAgent.Sum(a => a.Inbound);
        var totalOutbound = byAgent.Sum(a => a.Outbound);
        var totalBooked = byAgent.Sum(a => a.Booked);

        return new
        {
            TotalCalls = totalInbound + totalOutbound,
            Inbound = totalInbound,
            Outbound = totalOutbound,
            Booked = totalBooked,
            Abandoned = rng.Next(5, 15),
            Unbooked = rng.Next(10, 30),
            BookingRate = totalInbound > 0 ? Math.Round((double)totalBooked / totalInbound * 100, 1) : 0,
            Days = days,
            ByAgent = byAgent,
            ByDay = byDay
        };
    }
}
