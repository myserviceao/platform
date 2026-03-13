namespace MyServiceAO.Models;

public class Job
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StJobId { get; set; }
    public long StCustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string JobNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public string? JobTypeName { get; set; }
    public string? HoldReasonName { get; set; }
    public string? TagTypeIds { get; set; } // Comma-separated ST tag IDs
    public string? TechnicianName { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime? CreatedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
