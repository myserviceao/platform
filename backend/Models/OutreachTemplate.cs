namespace MyServiceAO.Models;

public class OutreachTemplate
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // pm_reminder, post_service, win_back, seasonal
    public string Channel { get; set; } = "email"; // email or sms
    public string? Subject { get; set; } // email subject (null for sms)
    public string Body { get; set; } = "";
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
