namespace MyServiceAO.Models;

public class HoldReason
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StHoldReasonId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public long? StTagTypeId { get; set; } // ST tag type ID mapped to this hold reason

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
