namespace MyServiceAO.Models;

/// <summary>
/// Service locations for customers. Synced from ST location export.
/// Geocoded to lat/lng for proximity-based PM planning.
/// </summary>
public class CustomerLocation
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StLocationId { get; set; }
    public long StCustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string LocationName { get; set; } = "";

    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";

    // Geocoded coordinates (nullable until geocoded)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsGeocoded { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
