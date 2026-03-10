namespace MyServiceAO.Models;

public class Tenant
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";          // e.g. "acme-hvac"
    public string Name { get; set; } = "";          // e.g. "Acme HVAC"
    public string? LogoUrl { get; set; }
    public string? Theme { get; set; } = "dark";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ServiceTitan connection (set during onboarding)
    public string? StClientId { get; set; }
    public string? StClientSecret { get; set; }
    public string? StTenantId { get; set; }
    public string? StAccessToken { get; set; }
    public DateTime? StTokenExpiresAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
