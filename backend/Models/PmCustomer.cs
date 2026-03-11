namespace MyServiceAO.Models;

/// <summary>
/// Per-customer PM status record. One row per ST customer per tenant.
/// Upserted on every sync based on completed PM job history.
/// </summary>
public class PmCustomer
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StCustomerId { get; set; }
    public string CustomerName { get; set; } = "";

    /// <summary>Date of the most recent completed PM job for this customer.</summary>
    public DateTime? LastPmDate { get; set; }

    /// <summary>Overdue = 6+ months, ComingDue = 4-6 months, Current = under 4 months</summary>
    public string PmStatus { get; set; } = "Current";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
