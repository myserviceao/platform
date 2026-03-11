using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/pm-planner")]
public class PmPlannerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;

    public PmPlannerController(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _http = httpFactory.CreateClient();
    }

    /// <summary>
    /// GET /api/pm-planner
    /// Returns PM customers grouped by proximity clusters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPlannerData([FromQuery] int radiusMinutes = 10)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        // Get all PM customers that need service (overdue + coming due)
        var pmCustomers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && (p.PmStatus == "Overdue" || p.PmStatus == "ComingDue"))
            .ToListAsync();

        var pmCustomerIds = pmCustomers.Select(p => p.StCustomerId).ToHashSet();

        // Get locations for these customers
        var locations = await _db.CustomerLocations
            .Where(l => l.TenantId == tenantId.Value && l.IsGeocoded && pmCustomerIds.Contains(l.StCustomerId))
            .ToListAsync();

        var pmMap = pmCustomers.ToDictionary(p => p.StCustomerId);

        // Build customer points
        var points = locations
            .Where(l => l.Latitude.HasValue && l.Longitude.HasValue)
            .Select(l =>
            {
                pmMap.TryGetValue(l.StCustomerId, out var pm);
                return new
                {
                    stCustomerId = l.StCustomerId,
                    customerName = l.CustomerName,
                    locationName = l.LocationName,
                    address = $"{l.Street}, {l.City}, {l.State} {l.Zip}",
                    lat = l.Latitude!.Value,
                    lng = l.Longitude!.Value,
                    pmStatus = pm?.PmStatus ?? "Unknown",
                    lastPmDate = pm?.LastPmDate,
                    daysSince = pm?.LastPmDate.HasValue == true
                        ? (int)(DateTime.UtcNow - pm.LastPmDate.Value).TotalDays
                        : 0
                };
            })
            .ToList();

        // Cluster by proximity
        // radiusMinutes: 10 min ~ 8 miles, 30 min ~ 25 miles, 60 min ~ 50 miles
        double radiusMiles = radiusMinutes switch
        {
            <= 10 => 8,
            <= 30 => 25,
            _ => 50
        };

        var clusters = ClusterByProximity(points, radiusMiles);

        return Ok(new
        {
            totalCustomers = points.Count,
            totalClusters = clusters.Count,
            notGeocoded = pmCustomers.Count - points.Count,
            radiusMinutes,
            radiusMiles,
            clusters
        });
    }

    /// <summary>
    /// POST /api/pm-planner/geocode
    /// Geocodes all un-geocoded customer locations.
    /// </summary>
    [HttpPost("geocode")]
    public async Task<IActionResult> GeocodeLocations()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var ungeocoded = await _db.CustomerLocations
            .Where(l => l.TenantId == tenantId.Value && !l.IsGeocoded && l.Street != "")
            .Take(50) // Batch to avoid rate limits
            .ToListAsync();

        int success = 0;
        int failed = 0;

        foreach (var loc in ungeocoded)
        {
            try
            {
                var address = Uri.EscapeDataString($"{loc.Street}, {loc.City}, {loc.State} {loc.Zip}");
                var url = $"https://nominatim.openstreetmap.org/search?q={address}&format=json&limit=1";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "MyServiceAO/1.0");
                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    var arr = doc.RootElement;

                    if (arr.GetArrayLength() > 0)
                    {
                        var first = arr[0];
                        if (first.TryGetProperty("lat", out var latProp) && first.TryGetProperty("lon", out var lonProp))
                        {
                            loc.Latitude = double.Parse(latProp.GetString()!);
                            loc.Longitude = double.Parse(lonProp.GetString()!);
                            loc.IsGeocoded = true;
                            success++;
                        }
                    }
                }

                // Rate limit: 1 request per second for Nominatim
                await Task.Delay(1100);
            }
            catch
            {
                failed++;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            geocoded = success,
            failed,
            remaining = await _db.CustomerLocations.CountAsync(l => l.TenantId == tenantId.Value && !l.IsGeocoded && l.Street != "")
        });
    }

    private static List<object> ClusterByProximity(List<dynamic> points, double radiusMiles)
    {
        var used = new HashSet<int>();
        var clusters = new List<object>();
        int clusterId = 0;

        for (int i = 0; i < points.Count; i++)
        {
            if (used.Contains(i)) continue;
            used.Add(i);
            clusterId++;

            var cluster = new List<dynamic> { points[i] };

            for (int j = i + 1; j < points.Count; j++)
            {
                if (used.Contains(j)) continue;

                double dist = HaversineDistance(
                    (double)points[i].lat, (double)points[i].lng,
                    (double)points[j].lat, (double)points[j].lng
                );

                if (dist <= radiusMiles)
                {
                    cluster.Add(points[j]);
                    used.Add(j);
                }
            }

            clusters.Add(new
            {
                id = clusterId,
                count = cluster.Count,
                customers = cluster,
                centerLat = cluster.Average(c => (double)c.lat),
                centerLng = cluster.Average(c => (double)c.lng)
            });
        }

        return clusters.OrderByDescending(c => ((dynamic)c).count).ToList();
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3959; // Earth radius in miles
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
