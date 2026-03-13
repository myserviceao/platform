namespace MyServiceAO.Models;

/// <summary>
/// Every ST customer for a tenant. Synced from CRM export on each sync.
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StCustomerId { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
