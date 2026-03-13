namespace MyServiceAO.Models;

public class OutreachItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int CustomerId { get; set; }
    public int? JobId { get; set; } // set for post_service items

    public string Type { get; set; } = ""; // pm_reminder, post_service, win_back, seasonal
    public string Channel { get; set; } = "email"; // email or sms
    public string Status { get; set; } = "pending"; // pending, sent, dismissed, failed
    public string? FailureReason { get; set; }

    public string? Subject { get; set; } // rendered email subject
    public string Body { get; set; } = ""; // rendered message body

    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
