using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Services.ServiceTitan;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanSyncService _sync;

    public DashboardController(AppDbContext db, ServiceTitanSyncService sync)
    {
        _db = db;
        _sync = sync;
    }

    /// <summary>
    /// GET /api/dashboard/snapshot
    /// Returns the latest cached KPI snapshot for the current tenant.
    /// Returns zeroed data if no sync has run yet.
    /// </summary>
    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var snapshot = await _db.DashboardSnapshots
            .FirstOrDefaultAsync(d => d.TenantId == tenantId.Value);

        if (snapshot == null)
        {
            // No sync yet — return empty state
            return Ok(new
            {
                synced = false,
                revenueThisMonth = 0m,
                revenueLastMonth = 0m,
                accountsReceivable = 0m,
                unpaidInvoiceCount = 0,
                openJobCount = 0,
                overduePmCount = 0,
                snapshotTakenAt = (DateTime?)null
            });
        }

        return Ok(new
        {
            synced = true,
            revenueThisMonth = snapshot.RevenueThisMonth,
            revenueLastMonth = snapshot.RevenueLastMonth,
            accountsReceivable = snapshot.AccountsReceivable,
            unpaidInvoiceCount = snapshot.UnpaidInvoiceCount,
            openJobCount = snapshot.OpenJobCount,
            overduePmCount = snapshot.OverduePmCount,
            snapshotTakenAt = snapshot.SnapshotTakenAt
        });
    }

    /// <summary>
    /// POST /api/dashboard/sync
    /// Triggers an on-demand sync for the current tenant and returns fresh snapshot.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var result = await _sync.SyncAllAsync(tenantId.Value);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        // Return fresh snapshot after sync
        return await GetSnapshot();
    }
}