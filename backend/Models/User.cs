namespace MyServiceAO.Models;

public class User
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Role { get; set; } = "member";    // "owner" | "admin" | "member"
    public string? Title { get; set; }               // Custom display title (e.g. "CEO", "Operations Manager")
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? Theme { get; set; } // Per-user theme preference

    public Tenant Tenant { get; set; } = null!;
}
