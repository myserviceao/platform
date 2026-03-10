using System.Text.Json;
using MyServiceAO.Data;

namespace MyServiceAO.Services.ServiceTitan;

public class ServiceTitanSyncService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ServiceTitanOAuthService _oauth;
    private readonly ILogger<ServiceTitanSyncService> _logger;

    public ServiceTitanSyncService(AppDbContext db, ServiceTitanClient client, ServiceTitanOAuthService oauth, ILogger<ServiceTitanSyncService> logger)
    {
        _db = db;
        _client = client;
        _oauth = oauth;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAllAsync(int tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StTenantId == null)
            return new SyncResult { Success = false, Error = "Tenant not connected to ServiceTitan" };

        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null)
            return new SyncResult { Success = false, Error = "Could not obtain access token" };

        var result = new SyncResult { Success = true };

        try
        {
            var jobsJson = await _client.GetJobsAsync(token, tenant.StTenantId);
            var jobsDoc = JsonDocument.Parse(jobsJson);
            if (jobsDoc.RootElement.TryGetProperty("data", out var jobsData))
                result.JobsSynced = jobsData.GetArrayLength();

            tenant.LastSyncedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            result.SyncedAt = tenant.LastSyncedAt.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ST Sync] Error syncing tenantId={TenantId}", tenantId);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int JobsSynced { get; set; }
    public DateTime SyncedAt { get; set; }
}