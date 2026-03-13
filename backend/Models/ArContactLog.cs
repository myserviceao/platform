namespace MyServiceAO.Models;

public class ArContactLog
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int CustomerId { get; set; }

    public string ContactType { get; set; } = "call"; // call, email, text
    public string Outcome { get; set; } = ""; // left_voicemail, spoke_with, promised_to_pay, disputed, no_answer, wrong_number
    public string? Notes { get; set; }
    public DateTime? FollowUpDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
