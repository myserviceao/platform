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
        _logger.LogInformation("[ST Sync] Starting sync tenantId={TenantId}", tenantId);

        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StTenantId == null)
            return new SyncResult { Success = false, Error = "Tenant not connected to ServiceTitan" };

        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null)
            return new SyncResult { Success = false, Error = "Could not obtain access token" };

        var snapshot = await _db.DashboardSnapshots.FirstOrDefaultAsync(d => d.TenantId == tenantId)
                       ?? new DashboardSnapshot { TenantId = tenantId };

        var errors = new List<string>();

        // customerId -> latest completed PM date
        var lastPmByCustomer = new Dictionary<string, DateTime>();

        try
        {
            var from = DateTime.UtcNow.AddMonths(-2).ToString("yyyy-MM-dd");
            await SyncInvoicesAsync(token, tenant.StTenantId, snapshot, from);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ST Sync] Invoices failed");
            errors.Add("invoices: " + ex.Message);
        }

        try
        {
            // 18 months covers anyone overdue by 6 months with room to spare
            var from = DateTime.UtcNow.AddMonths(-18).ToString("yyyy-MM-dd");
            await SyncJobsAsync(token, tenant.StTenantId, snapshot, from, lastPmByCustomer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ST Sync] Jobs failed");
            errors.Add("jobs: " + ex.Message);
        }

        // Overdue = last PM was more than 6 months ago
        var overdueDate = DateTime.UtcNow.AddMonths(-6);
        snapshot.OverduePmCount = lastPmByCustomer.Values.Count(d => d < overdueDate);

        _logger.LogInformation("[ST Sync] PM customers={Total} overdue(6mo+)={Overdue}",
            lastPmByCustomer.Count, snapshot.OverduePmCount);

        snapshot.SnapshotTakenAt = DateTime.UtcNow;
        if (snapshot.Id == 0) _db.DashboardSnapshots.Add(snapshot);
        tenant.LastSyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[ST Sync] Complete tenantId={TenantId} RevThis={RevThis} RevLast={RevLast} AR={AR} Jobs={Jobs} PMs={PMs} Errors={Err}",
            tenantId, snapshot.RevenueThisMonth, snapshot.RevenueLastMonth, snapshot.AccountsReceivable, snapshot.OpenJobCount, snapshot.OverduePmCount, errors.Count);

        return new SyncResult { Success = true, SyncedAt = snapshot.SnapshotTakenAt, Error = errors.Count > 0 ? string.Join("; ", errors) : null };
    }

    private async Task SyncInvoicesAsync(string token, string stTenantId, DashboardSnapshot snapshot, string from)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd = thisMonthStart;

        decimal revenueThisMonth = 0, revenueLastMonth = 0, ar = 0;
        int unpaidCount = 0, totalRecords = 0;
        string? continueFrom = from;

        do
        {
            var json = await _client.GetInvoicesExportAsync(token, stTenantId, continueFrom);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) break;

            var count = 0;
            foreach (var inv in data.EnumerateArray())
            {
                count++;
                var total = ParseDecimal(inv, "total");
                var balance = ParseDecimal(inv, "balance");
                var invoiceDate = ParseDateTime(inv, "invoiceDate");
                if (invoiceDate == null) continue;
                if (invoiceDate >= thisMonthStart) revenueThisMonth += total;
                if (invoiceDate >= lastMonthStart && invoiceDate < lastMonthEnd) revenueLastMonth += total;
                if (balance > 0) { ar += balance; unpaidCount++; }
            }
            totalRecords += count;

            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;
            if (!hasMore) break;
        } while (continueFrom != null);

        _logger.LogInformation("[ST Sync] Invoices processed={Total} revThis={RevThis} revLast={RevLast} ar={AR}", totalRecords, revenueThisMonth, revenueLastMonth, ar);
        snapshot.RevenueThisMonth = revenueThisMonth;
        snapshot.RevenueLastMonth = revenueLastMonth;
        snapshot.AccountsReceivable = ar;
        snapshot.UnpaidInvoiceCount = unpaidCount;
    }

    private async Task SyncJobsAsync(string token, string stTenantId, DashboardSnapshot snapshot, string from, Dictionary<string, DateTime> lastPmByCustomer)
    {
        var closedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Completed", "Canceled" };
        int openJobs = 0, totalRecords = 0;
        string? continueFrom = from;

        do
        {
            var json = await _client.GetJobsExportAsync(token, stTenantId, continueFrom);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) break;

            var count = 0;
            foreach (var job in data.EnumerateArray())
            {
                count++;

                var status = job.TryGetProperty("jobStatus", out var s) ? s.GetString() ?? "" : "";
                if (!closedStatuses.Contains(status)) openJobs++;

                // Check if this is a PM job by jobType name
                var jobTypeName = "";
                if (job.TryGetProperty("jobType", out var jt) && jt.ValueKind == JsonValueKind.Object)
                    jt.TryGetProperty("name", out var jtn);
                if (job.TryGetProperty("type", out var tp) && tp.ValueKind == JsonValueKind.String)
                    jobTypeName = tp.GetString() ?? "";
                if (string.IsNullOrEmpty(jobTypeName) && job.TryGetProperty("jobTypeName", out var jtn2))
                    jobTypeName = jtn2.GetString() ?? "";

                var isPm = PmKeywords.Any(k => jobTypeName.ToLower().Contains(k));
                if (!isPm) continue;

                // Only count completed PM jobs
                if (!status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) continue;

                // Get customer ID
                var customerId = "";
                if (job.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
                {
                    if (cust.TryGetProperty("id", out var cid))
                        customerId = cid.ToString();
                }
                if (string.IsNullOrEmpty(customerId)) continue;

                // Track the most recent completed PM date per customer
                var completedOn = ParseDateTime(job, "completedOn") ?? ParseDateTime(job, "modifiedOn");
                if (completedOn == null) continue;

                if (!lastPmByCustomer.TryGetValue(customerId, out var existing) || completedOn > existing)
                    lastPmByCustomer[customerId] = completedOn.Value;
            }
            totalRecords += count;

            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;
            if (!hasMore) break;
        } while (continueFrom != null);

        _logger.LogInformation("[ST Sync] Jobs processed={Total} openJobs={Open}", totalRecords, openJobs);
        snapshot.OpenJobCount = openJobs;
    }

    private static decimal ParseDecimal(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.Number) return val.GetDecimal();
        if (val.ValueKind == JsonValueKind.String) return decimal.TryParse(val.GetString(), out var d) ? d : 0;
        return 0;
    }

    private static DateTime? ParseDateTime(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Null) return null;
        return DateTime.TryParse(val.GetString(), out var dt) ? dt.ToUniversalTime() : null;
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime SyncedAt { get; set; }
}
