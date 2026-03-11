using MyServiceAO.Data;
using MyServiceAO.Models;
using Microsoft.EntityFrameworkCore;

namespace MyServiceAO.Services.ServiceTitan;

/// <summary>
/// Manages ServiceTitan OAuth tokens and credentials per tenant.
/// Caches tokens in-memory and refreshes proactively 5 minutes before expiry.
/// </summary>
public class ServiceTitanOAuthService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ILogger<ServiceTitanOAuthService> _logger;

    // In-memory token cache: tenantId -> (token, expiresAt)
    private static readonly Dictionary<int, (string Token, DateTime ExpiresAt)> _cache = new();
    private static readonly object _lock = new();

    // Refresh this many minutes before actual expiry to avoid 401s
    private const int RefreshBufferMinutes = 5;

    public ServiceTitanOAuthService(AppDbContext db, ServiceTitanClient client, ILogger<ServiceTitanOAuthService> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    /// <summary>Returns true if the tenant has ST credentials stored and a valid token can be obtained.</summary>
    public async Task<bool> IsConnectedAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StClientId == null || tenant.StClientSecret == null)
            return false;

        var token = await GetValidTokenAsync(tenantId);
        return token != null;
    }

    /// <summary>
    /// Stores ST credentials for the tenant and verifies them by getting a token.
    /// Returns true on success.
    /// </summary>
    public async Task<bool> ConnectAsync(int tenantId, string clientId, string clientSecret, string stTenantId)
    {
        // Test the credentials first
        var token = await _client.GetAccessTokenAsync(clientId, clientSecret);
        if (token == null)
        {
            _logger.LogWarning("[OAuth] ConnectAsync: invalid credentials for tenantId={TenantId}", tenantId);
            return false;
        }

        // Save credentials to DB
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            _logger.LogError("[OAuth] ConnectAsync: tenant not found tenantId={TenantId}", tenantId);
            return false;
        }

        tenant.StClientId = clientId;
        tenant.StClientSecret = clientSecret;
        tenant.StTenantId = stTenantId;
        await _db.SaveChangesAsync();

        // Cache the token
        var expiresAt = DateTime.UtcNow.AddMinutes(55);
        lock (_lock)
        {
            _cache[tenantId] = (token, expiresAt);
        }

        _logger.LogInformation("[OAuth] ConnectAsync: tenant {TenantId} connected to ST", tenantId);
        return true;
    }

    /// <summary>Removes stored ST credentials and clears the token cache for the tenant.</summary>
    public async Task DisconnectAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant != null)
        {
            tenant.StClientId = null;
            tenant.StClientSecret = null;
            tenant.StTenantId = null;
            await _db.SaveChangesAsync();
        }

        lock (_lock)
        {
            _cache.Remove(tenantId);
        }

        _logger.LogInformation("[OAuth] DisconnectAsync: tenant {TenantId} disconnected", tenantId);
    }

    /// <summary>
    /// Returns a valid access token for the given tenant, refreshing if near expiry.
    /// Returns null if credentials are missing or the token request fails.
    /// </summary>
    public async Task<string?> GetValidTokenAsync(int tenantId)
    {
        // Check cache with buffer
        lock (_lock)
        {
            if (_cache.TryGetValue(tenantId, out var cached))
            {
                var expiresWithBuffer = cached.ExpiresAt.AddMinutes(-RefreshBufferMinutes);
                if (DateTime.UtcNow < expiresWithBuffer)
                {
                    return cached.Token;
                }
                _logger.LogInformation("[OAuth] Token expiring soon, refreshing tenantId={TenantId}", tenantId);
            }
        }

        // Fetch fresh credentials from DB
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StClientId == null || tenant.StClientSecret == null)
        {
            _logger.LogWarning("[OAuth] No ST credentials for tenantId={TenantId}", tenantId);
            return null;
        }

        var token = await _client.GetAccessTokenAsync(tenant.StClientId, tenant.StClientSecret);
        if (token == null)
        {
            _logger.LogWarning("[OAuth] Failed to get access token for tenantId={TenantId}", tenantId);
            return null;
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(55);
        lock (_lock)
        {
            _cache[tenantId] = (token, expiresAt);
        }

        _logger.LogInformation("[OAuth] Token refreshed tenantId={TenantId} expiresAt={ExpiresAt}", tenantId, expiresAt);
        return token;
    }

    /// <summary>Force-clears the cached token for a tenant (e.g. after credential change).</summary>
    public void InvalidateCache(int tenantId)
    {
        lock (_lock)
        {
            _cache.Remove(tenantId);
        }
    }
}
