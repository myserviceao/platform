namespace MyServiceAO.Models;

public class Vendor
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
