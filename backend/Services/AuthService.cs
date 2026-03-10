using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<User> RegisterAsync(string tenantName, string email, string password, string firstName, string lastName)
    {
        var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
        if (emailExists)
            throw new Exception("An account with that email already exists.");

        var slug = GenerateSlug(tenantName);

        var existing = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
        if (existing != null)
            slug = $"{slug}-{DateTime.UtcNow.Ticks % 10000}";

        var tenant = new Tenant { Name = tenantName, Slug = slug };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            Role = "owner"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        user.Tenant = tenant;
        return user;
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace("&", "and")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace(",", "")
            .Replace(".", "");
    }
}
