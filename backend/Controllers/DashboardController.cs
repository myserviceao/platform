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

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var snapshot = await _db.DashboardSnapshots
            .FirstOrDefaultAsync(d => d.TenantId == tenantId.Value);

        if (snapshot == null)
        {
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

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var result = await _sync.SyncAllAsync(tenantId.Value);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return await GetSnapshot();
    }

    /// <summary>
    /// GET /api/dashboard/pm-tracker
    /// Returns all PM customers for the current tenant, sorted by LastPmDate ascending (oldest first).
    /// </summary>
    [HttpGet("pm-tracker")]
    public async Task<IActionResult> GetPmTracker()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.LastPmDate)
            .Select(p => new
            {
                p.StCustomerId,
                p.CustomerName,
                p.LastPmDate,
                p.PmStatus,
                p.UpdatedAt
            })
            .ToListAsync();

        return Ok(customers);
    }
}
