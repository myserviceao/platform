namespace MyServiceAO.Models;

/// <summary>
/// A scheduled appointment for the dashboard schedule strip.
/// Synced for a 3-day rolling window (today, tomorrow, day after).
/// </summary>
public class Appointment
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StAppointmentId { get; set; }
    public long StJobId { get; set; }

    public string JobNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";

    /// <summary>Comma-separated technician names from appointment-assignments.</summary>
    public string TechnicianNames { get; set; } = "";

    public string Status { get; set; } = "";

    /// <summary>Appointment start date/time in UTC.</summary>
    public DateTime Start { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}