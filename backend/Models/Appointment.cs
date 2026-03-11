namespace MyServiceAO.Models;

public class Appointment
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StAppointmentId { get; set; }
    public long StJobId { get; set; }
    public string JobNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "";

    // Appointment start time in UTC
    public DateTime Start { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<AppointmentTechnician> Technicians { get; set; } = new List<AppointmentTechnician>();
}

public class AppointmentTechnician
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public string TechnicianName { get; set; } = "";
}
