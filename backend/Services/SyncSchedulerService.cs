using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Services.ServiceTitan;

namespace MyServiceAO.Services;

/// <summary>
/// Hosted background service that syncs all connected tenants every 2 hours.
/// Runs as a long-lived service registered in Program.cs via AddHostedService.
/// </summary>
public class SyncSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncSchedulerService> _logger;

    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(2);

    public SyncSchedulerService(IServiceScopeFactory scopeFactory, ILogger<SyncSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Scheduler] ST sync scheduler started. Interval={Interval}h", SyncInterval.TotalHours);

        // Wait 1 minute after startup before first run to let the app warm up
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncForAllTenantsAsync(stoppingToken);
            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task RunSyncForAllTenantsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Scheduler] Running scheduled sync for all connected tenants");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<ServiceTitanSyncService>();

        // Find all tenants that have a connected ST account
        var connectedTenants = await db.Tenants
            .Where(t => t.StAccessToken != null && t.StTenantId != null)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("[Scheduler] Found {Count} connected tenants", connectedTenants.Count);

        foreach (var tenantId in connectedTenants)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var result = await syncService.SyncAllAsync(tenantId);
                if (result.Success)
                    _logger.LogInformation("[Scheduler] Synced tenantId={TenantId} at {Time}", tenantId, result.SyncedAt);
                else
                    _logger.LogWarning("[Scheduler] Sync failed tenantId={TenantId} error={Error}", tenantId, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scheduler] Unhandled error syncing tenantId={TenantId}", tenantId);
            }

            // Small delay between tenants to avoid hammering ST API
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        _logger.LogInformation("[Scheduler] Scheduled sync complete");
    }
}