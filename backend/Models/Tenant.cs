namespace MyServiceAO.Models;

public class Tenant
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Theme { get; set; } = "dark";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ServiceTitan connection
    public string? StClientId { get; set; }
    public string? StClientSecret { get; set; }
    public string? StTenantId { get; set; }
    public string? StAccessToken { get; set; }
    public DateTime? StTokenExpiresAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}