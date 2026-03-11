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

        // Get PM customers (overdue + coming due)
        var pmCustomers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && (p.PmStatus == "Overdue" || p.PmStatus == "ComingDue"))
            .ToListAsync();

        var pmCustomerIds = pmCustomers.Select(p => p.StCustomerId).ToHashSet();
        var pmMap = pmCustomers.ToDictionary(p => p.StCustomerId);

        // Get ALL customers (to find ones with no PM record)
        var allCustomerIds = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value)
            .Select(c => c.StCustomerId)
            .ToListAsync();

        var allPmCustomerIds = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value)
            .Select(p => p.StCustomerId)
            .ToListAsync();

        var noPmCustomerIds = allCustomerIds.Except(allPmCustomerIds).ToHashSet();

        // Combine: overdue + coming due + no PM
        var targetCustomerIds = new HashSet<long>(pmCustomerIds);
        foreach (var id in noPmCustomerIds) targetCustomerIds.Add(id);

        // Get geocoded locations for all target customers
        var locations = await _db.CustomerLocations
            .Where(l => l.TenantId == tenantId.Value && l.IsGeocoded && targetCustomerIds.Contains(l.StCustomerId))
            .ToListAsync();

        var points = locations
            .Where(l => l.Latitude.HasValue && l.Longitude.HasValue)
            .Select(l =>
            {
                pmMap.TryGetValue(l.StCustomerId, out var pm);
                var isNoPm = noPmCustomerIds.Contains(l.StCustomerId);
                return new CustomerPoint
                {
                    StCustomerId = l.StCustomerId,
                    CustomerName = l.CustomerName,
                    LocationName = l.LocationName,
                    Address = $"{l.Street}, {l.City}, {l.State} {l.Zip}",
                    Lat = l.Latitude!.Value,
                    Lng = l.Longitude!.Value,
                    PmStatus = isNoPm ? "NoPm" : (pm?.PmStatus ?? "Unknown"),
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
            overdueCount = points.Count(p => p.PmStatus == "Overdue"),
            comingDueCount = points.Count(p => p.PmStatus == "ComingDue"),
            noPmCount = points.Count(p => p.PmStatus == "NoPm"),
            totalClusters = clusters.Count,
            notGeocoded = targetCustomerIds.Count - points.Count,
            radiusMinutes,
            radiusMiles,
            clusters
        });
    }

    /// <summary>
    /// POST /api/pm-planner/geocode
    /// Batch geocodes all un-geocoded locations via the US Census Geocoder.
    /// Handles up to 1000 addresses in a single HTTP request.
    /// </summary>
    [HttpPost("geocode")]
    public async Task<IActionResult> GeocodeLocations()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var ungeocoded = await _db.CustomerLocations
            .Where(l => l.TenantId == tenantId.Value && !l.IsGeocoded && l.Street != "")
            .Take(1000)
            .ToListAsync();

        if (ungeocoded.Count == 0)
            return Ok(new { geocoded = 0, failed = 0, remaining = 0 });

        // Build CSV for Census batch geocoder
        // Format per line: UniqueID, Street, City, State, ZIP
        var csv = new System.Text.StringBuilder();
        foreach (var loc in ungeocoded)
        {
            csv.AppendLine($"{loc.Id},\"{loc.Street}\",\"{loc.City}\",\"{loc.State}\",\"{loc.Zip}\"");
        }

        int success = 0;
        int failed = 0;

        try
        {
            var form = new MultipartFormDataContent();
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var csvContent = new ByteArrayContent(csvBytes);
            csvContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(csvContent, "addressFile", "addresses.csv");
            form.Add(new StringContent("Public_AR_Current"), "benchmark");
            form.Add(new StringContent("csv"), "returntype");

            _http.Timeout = TimeSpan.FromMinutes(3);
            var response = await _http.PostAsync(
                "https://geocoding.geo.census.gov/geocoder/locations/addressbatch", form);

            if (!response.IsSuccessStatusCode)
                return BadRequest(new { error = $"Census API returned {response.StatusCode}" });

            var resultCsv = await response.Content.ReadAsStringAsync();
            var lookup = ungeocoded.ToDictionary(l => l.Id);

            foreach (var line in resultCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    // Response format: "ID","Input","Match/No Match","Exact/Non_Exact","Matched Addr","Lng,Lat","TigerID","Side"
                    var parts = SplitCsvLine(line);
                    if (parts.Count < 6) { failed++; continue; }

                    var idStr = parts[0].Trim('"', ' ');
                    if (!int.TryParse(idStr, out var locId)) { failed++; continue; }
                    if (!lookup.TryGetValue(locId, out var loc)) continue;

                    var match = parts[2].Trim('"', ' ');
                    if (match != "Match") { failed++; continue; }

                    // Coordinates field is "lng,lat" (note: longitude first!)
                    var coordStr = parts[5].Trim('"', ' ');
                    var coords = coordStr.Split(',');
                    if (coords.Length == 2 &&
                        double.TryParse(coords[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lng) &&
                        double.TryParse(coords[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                    {
                        loc.Latitude = lat;
                        loc.Longitude = lng;
                        loc.IsGeocoded = true;
                        success++;
                    }
                    else { failed++; }
                }
                catch { failed++; }
            }

            await _db.SaveChangesAsync();
        }
        catch (TaskCanceledException)
        {
            return BadRequest(new { error = "Census API timed out. Try again with fewer addresses." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Geocoding failed: {ex.Message}" });
        }

        return Ok(new
        {
            geocoded = success,
            failed,
            remaining = await _db.CustomerLocations.CountAsync(l => l.TenantId == tenantId.Value && !l.IsGeocoded && l.Street != "")
        });
    }

    /// <summary>Simple CSV line splitter that respects quoted fields.</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; current.Append(c); }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else { current.Append(c); }
        }
        result.Add(current.ToString());
        return result;
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
                    points[j].Lat, points[j].Lng);

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
        const double R = 3959; // Earth radius in miles
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
