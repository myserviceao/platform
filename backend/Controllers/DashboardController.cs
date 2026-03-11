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
    /// GET /api/dashboard
    /// Returns all dashboard data: AR aging, AR by customer, revenue, open WOs, schedule strip, overdue PMs.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var now = DateTime.UtcNow;

        // ── Check if any data has been synced ───────────────────────────────
        var snapshot = await _db.DashboardSnapshots
            .FirstOrDefaultAsync(d => d.TenantId == tenantId.Value);

        // ── AR: invoices with balance remaining ─────────────────────────────
        var arRaw = await _db.Invoices
            .Where(i => i.TenantId == tenantId.Value && i.BalanceRemaining > 0)
            .Select(i => new
            {
                i.CustomerName,
                i.StCustomerId,
                i.BalanceRemaining,
                i.InvoiceDate
            })
            .ToListAsync();

        // AR aging buckets
        var arAging = new
        {
            Bucket0_30   = arRaw.Where(i => (now - i.InvoiceDate).TotalDays <= 30).Sum(i => i.BalanceRemaining),
            Bucket31_60  = arRaw.Where(i => (now - i.InvoiceDate).TotalDays is > 30 and <= 60).Sum(i => i.BalanceRemaining),
            Bucket61_90  = arRaw.Where(i => (now - i.InvoiceDate).TotalDays is > 60 and <= 90).Sum(i => i.BalanceRemaining),
            Bucket90Plus = arRaw.Where(i => (now - i.InvoiceDate).TotalDays > 90).Sum(i => i.BalanceRemaining),
        };

        // AR by customer — grouped, sorted by total owed desc
        var arByCustomer = arRaw
            .GroupBy(i => new { i.CustomerName, i.StCustomerId })
            .Select(g => new
            {
                g.Key.CustomerName,
                TotalOwed = g.Sum(i => i.BalanceRemaining),
                OldestInvoiceDays = (int)(now - g.Min(i => i.InvoiceDate)).TotalDays
            })
            .OrderByDescending(x => x.TotalOwed)
            .ToList();

        var totalAr = arRaw.Sum(i => i.BalanceRemaining);

        // ── Revenue this month vs last month ────────────────────────────────
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var allInvoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId.Value)
            .Select(i => new { i.InvoiceDate, i.TotalAmount })
            .ToListAsync();

        var revenueThisMonth = allInvoices
            .Where(i => i.InvoiceDate >= thisMonthStart && i.InvoiceDate < now)
            .Sum(i => i.TotalAmount);

        var revenueLastMonth = allInvoices
            .Where(i => i.InvoiceDate >= lastMonthStart && i.InvoiceDate < thisMonthStart)
            .Sum(i => i.TotalAmount);

        // ── Open Work Orders ────────────────────────────────────────────────
        // Jobs with status: Scheduled, InProgress, Hold (case-insensitive contains)
        var openWos = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value
                && (j.Status.ToLower().Contains("inprogress")
                    || j.Status.ToLower().Contains("scheduled")
                    || j.Status.ToLower().Contains("hold")))
            .OrderBy(j => j.CreatedOn)
            .Select(j => new
            {
                j.StJobId,
                j.JobNumber,
                j.CustomerName,
                j.Status,
                j.TotalAmount,
                j.CreatedOn
            })
            .ToListAsync();

        // Oldest open WO in days
        var oldestWoDays = openWos.Any(w => w.CreatedOn.HasValue)
            ? (int)(now - openWos.Where(w => w.CreatedOn.HasValue).Min(w => w.CreatedOn!.Value)).TotalDays
            : 0;

        // ── Overdue PMs ─────────────────────────────────────────────────────
        var overduePms = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && p.PmStatus == "Overdue")
            .OrderBy(p => p.LastPmDate)
            .Select(p => new
            {
                p.CustomerName,
                p.LastPmDate,
                DaysSince = p.LastPmDate.HasValue
                    ? (int)(now - p.LastPmDate.Value).TotalDays
                    : 0
            })
            .ToListAsync();

        // ── Schedule Strip ──────────────────────────────────────────────────
        TimeZoneInfo centralZone;
        try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        catch { try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
                catch { centralZone = TimeZoneInfo.Utc; } }

        var nowCentral   = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralZone);
        var todayLocal   = nowCentral.Date;
        var tomorrowLocal = todayLocal.AddDays(1);
        var dayAfterLocal = todayLocal.AddDays(2);

        var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, centralZone);
        var windowEndUtc   = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddDays(3), centralZone);

        var appts = await _db.Appointments
            .Where(a => a.TenantId == tenantId.Value
                && a.Start >= windowStartUtc
                && a.Start < windowEndUtc
                && a.Status != "Canceled"
                && a.Status != "Cancelled")
            .Include(a => a.Technicians)
            .ToListAsync();

        object BuildDaySchedule(DateTime localDay)
        {
            var dayAppts = appts
                .Where(a => TimeZoneInfo.ConvertTimeFromUtc(a.Start, centralZone).Date == localDay)
                .OrderBy(a => a.Start)
                .Select(a => new
                {
                    a.JobNumber,
                    a.CustomerName,
                    Start = a.Start,
                    Techs = a.Technicians.Select(t => t.TechnicianName).ToList()
                })
                .ToList();

            return new { Count = dayAppts.Count, Items = dayAppts };
        }

        var schedToday    = BuildDaySchedule(todayLocal);
        var schedTomorrow = BuildDaySchedule(tomorrowLocal);
        var schedDayAfter = BuildDaySchedule(dayAfterLocal);

        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var daysElapsed = now.Day;

        return Ok(new
        {
            Synced = snapshot != null,
            SnapshotTakenAt = snapshot?.SnapshotTakenAt,

            TotalAR = totalAr,
            ARaging = arAging,
            ARbyCustomer = arByCustomer,

            RevenueThisMonth = revenueThisMonth,
            RevenueLastMonth = revenueLastMonth,
            DaysInMonth = daysInMonth,
            DaysElapsed = daysElapsed,

            OpenWorkOrders = openWos,
            OpenWoCount = openWos.Count,
            OldestWoDays = oldestWoDays,

            OverduePms = overduePms,
            OverduePmCount = overduePms.Count,

            ScheduledToday    = schedToday,
            ScheduledTomorrow = schedTomorrow,
            ScheduledDayAfter = schedDayAfter,

            TodayLabel    = todayLocal.ToString("dddd"),
            TomorrowLabel = tomorrowLocal.ToString("dddd"),
            DayAfterLabel = dayAfterLocal.ToString("dddd"),
        });
    }

    /// <summary>
    /// POST /api/dashboard/sync
    /// Triggers a full ST sync and returns fresh dashboard data.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var result = await _sync.SyncAllAsync(tenantId.Value);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return await GetDashboard();
    }

    /// <summary>
    /// GET /api/dashboard/pm-tracker
    /// Returns all PM customers for the current tenant, sorted by LastPmDate ascending.
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
