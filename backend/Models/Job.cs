namespace MyServiceAO.Models;

/// <summary>
/// A lightweight snapshot of a ST job — used for the Open Work Orders list on the dashboard.
/// Only jobs with an "open" status (Scheduled, InProgress, Hold) are kept.
/// </summary>
public class Job
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StJobId { get; set; }
    public string JobNumber { get; set; } = "";
    public long StCustomerId { get; set; }
    public string CustomerName { get; set; } = "";

    /// <summary>ST jobStatus — e.g. "Scheduled", "InProgress", "Hold", "Done", "Canceled"</summary>
    public string Status { get; set; } = "";

    public decimal Total { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}