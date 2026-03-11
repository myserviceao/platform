using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services.ServiceTitan;

public class ServiceTitanSyncService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ServiceTitanOAuthService _oauth;
    private readonly ILogger<ServiceTitanSyncService> _logger;

    private static readonly string[] PmKeywords = { "maintenance", "tune up", "tune-up", "pm" };

    public ServiceTitanSyncService(AppDbContext db, ServiceTitanClient client, ServiceTitanOAuthService oauth, ILogger<ServiceTitanSyncService> logger)
    {
        _db = db; _client = client; _oauth = oauth; _logger = logger;
    }

    public async Task<SyncResult> SyncAllAsync(int tenantId)
    {
        try
        {
            var token = await _oauth.GetValidTokenAsync(tenantId);
            if (token == null)
                return new SyncResult { Success = false, Error = "Not connected to ServiceTitan" };

            var tenant = await _db.Tenants.FindAsync(tenantId);
            if (tenant?.StTenantId == null)
                return new SyncResult { Success = false, Error = "ST Tenant ID not configured" };

            var stTenantId = tenant.StTenantId;

            // 1. Load job type name map (id -> name)
            var jobTypeMap = await _client.GetJobTypeMapAsync(token, stTenantId);
            _logger.LogInformation("[Sync] tenantId={TenantId} jobTypes={Count}", tenantId, jobTypeMap.Count);

            // 2. Load customer name map (stCustomerId -> name)
            var customerNameMap = await _client.GetCustomerNameMapAsync(token, stTenantId);
            _logger.LogInformation("[Sync] tenantId={TenantId} customers={Count}", tenantId, customerNameMap.Count);

            // 3. Export all jobs and find PM jobs
            // Use completedOn ?? createdOn — same logic as original PatriotMechanical app
            var pmDates = new Dictionary<long, DateTime>();
            int pmFound = 0;

            string? continueFrom = null;
            bool hasMore = true;
            while (hasMore)
            {
                var raw = await _client.GetJobsExportAsync(token, stTenantId, continueFrom);
                var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
                continueFrom = root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;

                if (!root.TryGetProperty("data", out var data)) break;

                foreach (var job in data.EnumerateArray())
                {
                    // Resolve job type name
                    string? jobTypeName = null;
                    if (job.TryGetProperty("jobTypeId", out var jtProp) && jtProp.ValueKind == JsonValueKind.Number)
                        jobTypeMap.TryGetValue(jtProp.GetInt64(), out jobTypeName);

                    if (jobTypeName == null) continue;
                    if (!PmKeywords.Any(k => jobTypeName.ToLower().Contains(k))) continue;

                    if (!job.TryGetProperty("customerId", out var custProp)) continue;
                    var customerId = custProp.GetInt64();

                    // completedOn ?? createdOn (same as old app: CompletedAt ?? CreatedAt)
                    DateTime? completedOn = null;
                    if (job.TryGetProperty("completedOn", out var coProp) && coProp.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(coProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var pc))
                            completedOn = DateTime.SpecifyKind(pc, DateTimeKind.Utc);
                    }

                    DateTime? createdOn = null;
                    if (job.TryGetProperty("createdOn", out var crProp) && crProp.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(crProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var pc2))
                            createdOn = DateTime.SpecifyKind(pc2, DateTimeKind.Utc);
                    }

                    var pmDate = completedOn ?? createdOn;
                    if (pmDate == null) continue;

                    pmFound++;

                    if (!pmDates.TryGetValue(customerId, out var existing) || pmDate > existing)
                        pmDates[customerId] = pmDate.Value;
                }

                if (!hasMore || continueFrom == null) break;
            }

            _logger.LogInformation("[Sync] tenantId={TenantId} pmFound={PmFound} uniqueCustomers={Customers}", tenantId, pmFound, pmDates.Count);

            // 4. Upsert PmCustomers
            foreach (var (stCustomerId, lastPmDate) in pmDates)
            {
                customerNameMap.TryGetValue(stCustomerId, out var customerName);

                var daysSince = (DateTime.UtcNow - lastPmDate).Days;
                var status = daysSince >= 180 ? "Overdue"
                           : daysSince >= 120 ? "ComingDue"
                           : "Current";

                var existing = await _db.PmCustomers
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.StCustomerId == stCustomerId);

                if (existing == null)
                {
                    _db.PmCustomers.Add(new PmCustomer
                    {
                        TenantId = tenantId,
                        StCustomerId = stCustomerId,
                        CustomerName = customerName ?? $"Customer {stCustomerId}",
                        LastPmDate = lastPmDate,
                        PmStatus = status,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.CustomerName = customerName ?? existing.CustomerName;
                    existing.LastPmDate = lastPmDate;
                    existing.PmStatus = status;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();

            // Update snapshot timestamp
            var snapshot = await _db.DashboardSnapshots.FirstOrDefaultAsync(s => s.TenantId == tenantId);
            if (snapshot == null)
            {
                snapshot = new DashboardSnapshot { TenantId = tenantId };
                _db.DashboardSnapshots.Add(snapshot);
            }
            snapshot.SnapshotTakenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new SyncResult { Success = true, SyncedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sync] Failed for tenantId={TenantId}", tenantId);
            return new SyncResult { Success = false, Error = ex.Message };
        }
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? Error { get; set; }
}
