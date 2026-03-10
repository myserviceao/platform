using MyServiceAO.Data;
using MyServiceAO.Models;
using Microsoft.EntityFrameworkCore;

namespace MyServiceAO.Services.ServiceTitan;

/// <summary>
/// Handles the ServiceTitan OAuth2 client_credentials flow.
/// ST uses client_credentials (not authorization_code) — tenants provide
/// their own Client ID + Secret from the ST developer portal.
/// </summary>
public class ServiceTitanOAuthService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ILogger<ServiceTitanOAuthService> _logger;

    public ServiceTitanOAuthService(
        AppDbContext db,
        ServiceTitanClient client,
        ILogger<ServiceTitanOAuthService> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Exchanges a tenant's ST credentials for an access token and stores it.
    /// Called when tenant first connects or when token is expired.
    /// </summary>
    public async Task<bool> ConnectAsync(int tenantId, string clientId, string clientSecret, string stTenantId)
    {
        _logger.LogInformation("[ST OAuth] Connecting tenantId={TenantId}", tenantId);

        var token = await _client.GetAccessTokenAsync(clientId, clientSecret);

        if (token == null)
        {
            _logger.LogWarning("[ST OAuth] Failed to get token for tenantId={TenantId}", tenantId);
            return false;
        }

        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return false;

        tenant.StClientId = clientId;
        tenant.StClientSecret = clientSecret;
        tenant.StTenantId = stTenantId;
        tenant.StAccessToken = token;
        tenant.StTokenExpiresAt = DateTime.UtcNow.AddMinutes(55); // ST tokens last 60min

        await _db.SaveChangesAsync();

        _logger.LogInformation("[ST OAuth] Token stored for tenantId={TenantId}", tenantId);
        return true;
    }

    /// <summary>
    /// Returns a valid access token for a tenant, refreshing if needed.
    /// </summary>
    public async Task<string?> GetValidTokenAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StClientId == null || tenant.StClientSecret == null)
            return null;

        // Refresh if expired or expiring in the next 5 minutes
        if (tenant.StAccessToken == null ||
            tenant.StTokenExpiresAt == null ||
            tenant.StTokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("[ST OAuth] Refreshing token for tenantId={TenantId}", tenantId);
            var newToken = await _client.GetAccessTokenAsync(tenant.StClientId, tenant.StClientSecret);
            if (newToken == null) return null;

            tenant.StAccessToken = newToken;
            tenant.StTokenExpiresAt = DateTime.UtcNow.AddMinutes(55);
            await _db.SaveChangesAsync();
        }

        return tenant.StAccessToken;
    }

    /// <summary>
    /// Disconnects a tenant from ServiceTitan by clearing stored credentials.
    /// </summary>
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
        _logger.LogInformation("[ST OAuth] Disconnected tenantId={TenantId}", tenantId);
    }

    public async Task<bool> IsConnectedAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        return tenant?.StAccessToken != null;
    }
}