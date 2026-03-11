using MyServiceAO.Data;
using MyServiceAO.Services.ServiceTitan;

namespace MyServiceAO.Services.ServiceTitan;

/// <summary>
/// Manages ServiceTitan OAuth tokens per tenant.
/// Caches tokens in-memory and refreshes proactively 5 minutes before expiry.
/// </summary>
public class ServiceTitanOAuthService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ILogger<ServiceTitanOAuthService> _logger;

    // In-memory cache: tenantId -> (token, expiresAt)
    private static readonly Dictionary<int, (string Token, DateTime ExpiresAt)> _cache = new();
    private static readonly object _lock = new();

    // Refresh the token this many minutes before it actually expires
    private const int RefreshBufferMinutes = 5;

    public ServiceTitanOAuthService(AppDbContext db, ServiceTitanClient client, ILogger<ServiceTitanOAuthService> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Returns a valid access token for the given tenant, refreshing if needed.
    /// Returns null if credentials are missing or the token request fails.
    /// </summary>
    public async Task<string?> GetValidTokenAsync(int tenantId)
    {
        // Check cache first (with buffer)
        lock (_lock)
        {
            if (_cache.TryGetValue(tenantId, out var cached))
            {
                var expiresWithBuffer = cached.ExpiresAt.AddMinutes(-RefreshBufferMinutes);
                if (DateTime.UtcNow < expiresWithBuffer)
                {
                    _logger.LogInformation("[OAuth] Using cached token tenantId={TenantId} expiresAt={ExpiresAt}", tenantId, cached.ExpiresAt);
                    return cached.Token;
                }
                _logger.LogInformation("[OAuth] Token expiring soon, refreshing tenantId={TenantId}", tenantId);
            }
        }

        // Need a fresh token - fetch credentials from DB
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

        // ST tokens are valid for 1 hour; cache with 5 min buffer
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
