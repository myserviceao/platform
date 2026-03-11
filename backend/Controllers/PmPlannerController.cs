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
            .Take(10000) // Census allows up to 10k at once
            .ToListAsync();

        if (ungeocoded.Count == 0)
            return Ok(new { geocoded = 0, failed = 0, remaining = 0 });

        // Build CSV for US Census Batch Geocoder
        // Format: Unique ID, Street, City, State, ZIP
        var csvLines = new System.Text.StringBuilder();
        foreach (var loc in ungeocoded)
        {
            var street = loc.Street.Replace(",", " ").Replace(""", "");
            var city = loc.City.Replace(",", " ").Replace(""", "");
            csvLines.AppendLine($"{loc.Id},"{street}","{city}","{loc.State}","{loc.Zip}"");
        }

        int success = 0;
        int failed = 0;

        try
        {
            using var formContent = new MultipartFormDataContent();
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvLines.ToString());
            formContent.Add(new ByteArrayContent(csvBytes), "addressFile", "addresses.csv");
            formContent.Add(new StringContent("Public_AR_Current"), "benchmark");
            formContent.Add(new StringContent("ACS2023_Current"), "vintage");

            var response = await _http.PostAsync(
                "https://geocoding.geo.census.gov/geocoder/locations/addressbatch",
                formContent
            );

            if (response.IsSuccessStatusCode)
            {
                var resultCsv = await response.Content.ReadAsStringAsync();
                var locMap = ungeocoded.ToDictionary(l => l.Id);

                foreach (var line in resultCsv.Split('
', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        // Parse CSV: ID,"Input Address","Match/No_Match","Exact/Non_Exact","Matched Address",lon/lat,"TIGER Line ID","Side"
                        var parts = ParseCsvLine(line);
                        if (parts.Count < 6) continue;

                        if (!int.TryParse(parts[0].Trim('"'), out var locId)) continue;
                        if (!locMap.TryGetValue(locId, out var loc)) continue;

                        var matchStatus = parts[2].Trim('"');
                        if (matchStatus != "Match") { failed++; continue; }

                        // Coordinates are in field index 5 as "lon,lat"
                        var coords = parts[5].Trim('"').Split(',');
                        if (coords.Length == 2 &&
                            double.TryParse(coords[0], out var lon) &&
                            double.TryParse(coords[1], out var lat))
                        {
                            loc.Longitude = lon;
                            loc.Latitude = lat;
                            loc.IsGeocoded = true;
                            success++;
                        }
                        else { failed++; }
                    }
                    catch { failed++; }
                }
            }
            else
            {
                failed = ungeocoded.Count;
            }
        }
        catch
        {
            failed = ungeocoded.Count;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            geocoded = success,
            failed,
            remaining = await _db.CustomerLocations.CountAsync(l => l.TenantId == tenantId.Value && !l.IsGeocoded && l.Street != "")
        });
    }

    /// <summary>
    /// Parse a CSV line handling quoted fields with commas inside.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; current.Append(ch); }
            else if (ch == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
            else { current.Append(ch); }
        }
        fields.Add(current.ToString());
        return fields;
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

        return clusters;
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
