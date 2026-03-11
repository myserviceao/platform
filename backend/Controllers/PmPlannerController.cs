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

    private class CustomerPoint
    {
        public long StCustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string LocationName { get; set; } = "";
        public string Address { get; set; } = "";
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string PmStatus { get; set; } = "";
        public DateTime? LastPmDate { get; set; }
        public int DaysSince { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetPlannerData([FromQuery] int radiusMinutes = 10)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var pmCustomers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && (p.PmStatus == "Overdue" || p.PmStatus == "ComingDue"))
            .ToListAsync();

        var pmCustomerIds = pmCustomers.Select(p => p.StCustomerId).ToHashSet();

        var locations = await _db.CustomerLocations
            .Where(l => l.TenantId == tenantId.Value && l.IsGeocoded && pmCustomerIds.Contains(l.StCustomerId))
            .ToListAsync();

        var pmMap = pmCustomers.ToDictionary(p => p.StCustomerId);

        var points = locations
            .Where(l => l.Latitude.HasValue && l.Longitude.HasValue)
            .Select(l =>
            {
                pmMap.TryGetValue(l.StCustomerId, out var pm);
                return new CustomerPoint
                {
                    StCustomerId = l.StCustomerId,
                    CustomerName = l.CustomerName,
                    LocationName = l.LocationName,
                    Address = $"{l.Street}, {l.City}, {l.State} {l.Zip}",
                    Lat = l.Latitude!.Value,
                    Lng = l.Longitude!.Value,
                    PmStatus = pm?.PmStatus ?? "Unknown",
                    LastPmDate = pm?.LastPmDate,
                    DaysSince = pm?.LastPmDate.HasValue == true
                        ? (int)(DateTime.UtcNow - pm.LastPmDate.Value).TotalDays
                        : 0
                };
            })
            .ToList();

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

    [HttpPost("geocode")]
    public async Task<IActionResult> GeocodeLocations()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var ungeocoded = await _db.CustomerLocations
            .Where(l => l.TenantId == tenantId.Value && !l.IsGeocoded && l.Street != "")
            .Take(50)
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

    private static List<object> ClusterByProximity(List<CustomerPoint> points, double radiusMiles)
    {
        var used = new HashSet<int>();
        var clusters = new List<object>();
        int clusterId = 0;

        for (int i = 0; i < points.Count; i++)
        {
            if (used.Contains(i)) continue;
            used.Add(i);
            clusterId++;

            var cluster = new List<CustomerPoint> { points[i] };

            for (int j = i + 1; j < points.Count; j++)
            {
                if (used.Contains(j)) continue;

                double dist = HaversineDistance(
                    points[i].Lat, points[i].Lng,
                    points[j].Lat, points[j].Lng
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
                customers = cluster.Select(c => new
                {
                    c.StCustomerId,
                    customerName = c.CustomerName,
                    locationName = c.LocationName,
                    address = c.Address,
                    lat = c.Lat,
                    lng = c.Lng,
                    pmStatus = c.PmStatus,
                    lastPmDate = c.LastPmDate,
                    daysSince = c.DaysSince
                }),
                centerLat = cluster.Average(c => c.Lat),
                centerLng = cluster.Average(c => c.Lng)
            });
        }

        return clusters.OrderByDescending(c => ((dynamic)c).count).ToList();
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3959;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
