using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services.ServiceTitan;

/// <summary>
/// Pulls data from ServiceTitan and builds a DashboardSnapshot for the tenant.
/// Called on connect, on-demand, and by the background scheduler every 2 hours.
/// </summary>
public class ServiceTitanSyncService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ServiceTitanOAuthService _oauth;
    private readonly ILogger<ServiceTitanSyncService> _logger;

    public ServiceTitanSyncService(
        AppDbContext db,
        ServiceTitanClient client,
        ServiceTitanOAuthService oauth,
        ILogger<ServiceTitanSyncService> logger)
    {
        _db = db;
        _client = client;
        _oauth = oauth;
        _logger = logger;
    }

    /// <summary>
    /// Syncs all KPIs and upserts the DashboardSnapshot for this tenant.
    /// </summary>
    public async Task<SyncResult> SyncAllAsync(int tenantId)
    {
        _logger.LogInformation("[ST Sync] Starting sync tenantId={TenantId}", tenantId);

        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant?.StTenantId == null)
            return new SyncResult { Success = false, Error = "Tenant not connected to ServiceTitan" };

        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null)
            return new SyncResult { Success = false, Error = "Could not obtain access token" };

        var result = new SyncResult { Success = true };
        var snapshot = await _db.DashboardSnapshots.FirstOrDefaultAsync(d => d.TenantId == tenantId)
                       ?? new DashboardSnapshot { TenantId = tenantId };

        try
        {
            // Pull invoices — start from 2 months ago to cover this + last month revenue
            var twoMonthsAgo = DateTime.UtcNow.AddMonths(-2).ToString("yyyy-MM-dd");
            await SyncInvoicesAsync(token, tenant.StTenantId, snapshot, twoMonthsAgo);

            // Pull jobs — start from 6 months ago to capture all open work orders
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-dd");
            await SyncJobsAsync(token, tenant.StTenantId, snapshot, sixMonthsAgo);

            // Pull recurring service events — start from 3 months ago for overdue PMs
            var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3).ToString("yyyy-MM-dd");
            await SyncRecurringServiceEventsAsync(token, tenant.StTenantId, snapshot, threeMonthsAgo);

            snapshot.SnapshotTakenAt = DateTime.UtcNow;

            if (snapshot.Id == 0)
                _db.DashboardSnapshots.Add(snapshot);

            tenant.LastSyncedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            result.SyncedAt = snapshot.SnapshotTakenAt;
            _logger.LogInformation("[ST Sync] Complete tenantId={TenantId} AR={AR} OpenJobs={Jobs} OverduePMs={PMs}",
                tenantId, snapshot.AccountsReceivable, snapshot.OpenJobCount, snapshot.OverduePmCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ST Sync] Error tenantId={TenantId}", tenantId);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    // ── Invoice sync: Revenue + AR ────────────────────────────────────────────

    private async Task SyncInvoicesAsync(string token, string stTenantId, DashboardSnapshot snapshot, string from)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd = thisMonthStart;

        decimal revenueThisMonth = 0, revenueLastMonth = 0, ar = 0;
        int unpaidCount = 0;
        string? continueFrom = from;

        do
        {
            var json = await _client.GetInvoicesExportAsync(token, stTenantId, continueFrom);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data)) break;

            foreach (var inv in data.EnumerateArray())
            {
                // Only active invoices
                if (inv.TryGetProperty("active", out var active) && !active.GetBoolean()) continue;

                var total = ParseDecimal(inv, "total");
                var balance = ParseDecimal(inv, "balance");
                var invoiceDate = ParseDateTime(inv, "invoiceDate");

                if (invoiceDate == null) continue;

                // Revenue this month
                if (invoiceDate >= thisMonthStart)
                    revenueThisMonth += total;

                // Revenue last month
                if (invoiceDate >= lastMonthStart && invoiceDate < lastMonthEnd)
                    revenueLastMonth += total;

                // AR — any invoice with a balance > 0 is unpaid
                if (balance > 0)
                {
                    ar += balance;
                    unpaidCount++;
                }
            }

            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;

            if (!hasMore) break;

        } while (continueFrom != null);

        snapshot.RevenueThisMonth = revenueThisMonth;
        snapshot.RevenueLastMonth = revenueLastMonth;
        snapshot.AccountsReceivable = ar;
        snapshot.UnpaidInvoiceCount = unpaidCount;
    }

    // ── Job sync: Open Work Orders ────────────────────────────────────────────

    private async Task SyncJobsAsync(string token, string stTenantId, DashboardSnapshot snapshot, string from)
    {
        // ST jobStatus values: Scheduled, InProgress, Hold, Completed, Canceled
        var closedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Completed", "Canceled" };

        int openJobs = 0;
        string? continueFrom = from;

        do
        {
            var json = await _client.GetJobsExportAsync(token, stTenantId, continueFrom);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data)) break;

            foreach (var job in data.EnumerateArray())
            {
                if (job.TryGetProperty("active", out var active) && !active.GetBoolean()) continue;

                var status = job.TryGetProperty("jobStatus", out var s) ? s.GetString() ?? "" : "";
                if (!closedStatuses.Contains(status))
                    openJobs++;
            }

            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;

            if (!hasMore) break;

        } while (continueFrom != null);

        snapshot.OpenJobCount = openJobs;
    }

    // ── Recurring service events: Overdue PMs ─────────────────────────────────

    private async Task SyncRecurringServiceEventsAsync(string token, string stTenantId, DashboardSnapshot snapshot, string from)
    {
        // Overdue PM = active event where dueDate < today AND status is not Completed or Canceled
        var closedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Completed", "Canceled" };

        var today = DateTime.UtcNow.Date;
        int overduePms = 0;
        string? continueFrom = from;

        do
        {
            var json = await _client.GetRecurringServiceEventsExportAsync(token, stTenantId, continueFrom);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data)) break;

            foreach (var evt in data.EnumerateArray())
            {
                if (evt.TryGetProperty("active", out var active) && !active.GetBoolean()) continue;

                var status = evt.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                if (closedStatuses.Contains(status)) continue;

                var dueDate = ParseDateTime(evt, "dueDate");
                if (dueDate.HasValue && dueDate.Value.Date < today)
                    overduePms++;
            }

            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;

            if (!hasMore) break;

        } while (continueFrom != null);

        snapshot.OverduePmCount = overduePms;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal ParseDecimal(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.Number) return val.GetDecimal();
        if (val.ValueKind == JsonValueKind.String)
            return decimal.TryParse(val.GetString(), out var d) ? d : 0;
        return 0;
    }

    private static DateTime? ParseDateTime(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Null) return null;
        var str = val.GetString();
        return DateTime.TryParse(str, out var dt) ? dt.ToUniversalTime() : null;
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime SyncedAt { get; set; }
}