namespace MyServiceAO.Models;

public class ArStatus
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int CustomerId { get; set; }

    public string Status { get; set; } = "active"; // active, payment_plan, sent_to_collections, written_off
    public decimal? PaymentPlanAmount { get; set; }
    public string? PaymentPlanNote { get; set; }

    public DateTime StatusChangedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
