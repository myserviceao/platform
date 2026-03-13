namespace MyServiceAO.Models;

public class OutreachSettings
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int WinBackThresholdMonths { get; set; } = 12;
    public int PostServiceDelayHours { get; set; } = 48;

    // Resend (email)
    public string? ResendApiKey { get; set; }
    public string? ResendFromEmail { get; set; }

    // Twilio (SMS)
    public string? TwilioAccountSid { get; set; }
    public string? TwilioAuthToken { get; set; }
    public string? TwilioFromPhone { get; set; }
}
