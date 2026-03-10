using MyServiceAO.Data;
using Microsoft.EntityFrameworkCore;

namespace MyServiceAO.Services.ServiceTitan;

public class ServiceTitanOAuthService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ILogger<ServiceTitanOAuthService> _logger;

    public ServiceTitanOAuthService(AppDbContext db, ServiceTitanClient client, ILogger<ServiceTitanOAuthService> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(int tenantId, string clientId, string clientSecret, string stTenantId)
    {
        var token = await _client.GetAccessTokenAsync(clientId, clientSecret);
        if (token == null) return false;

        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return false;

        tenant.StClientId = clientId;
        tenant.StClientSecret = clientSecret;
        tenant.StTenantId = stTenantId;
        tenant.StAccessToken = token;
        tenant.StTokenExpiresAt = DateTime.UtcNow.AddMinutes(55);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetValidTokenAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StClientId == null || tenant.StClientSecret == null) return null;

        if (tenant.StAccessToken == null ||
            tenant.StTokenExpiresAt == null ||
            tenant.StTokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            var newToken = await _client.GetAccessTokenAsync(tenant.StClientId, tenant.StClientSecret);
            if (newToken == null) return null;
            tenant.StAccessToken = newToken;
            tenant.StTokenExpiresAt = DateTime.UtcNow.AddMinutes(55);
            await _db.SaveChangesAsync();
        }

        return tenant.StAccessToken;
    }

    public async Task DisconnectAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return;
        tenant.StClientId = null;
        tenant.StClientSecret = null;
        tenant.StTenantId = null;
        tenant.StAccessToken = null;
        tenant.StTokenExpiresAt = null;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> IsConnectedAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        return tenant?.StAccessToken != null;
    }
}