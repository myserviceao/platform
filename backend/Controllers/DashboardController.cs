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

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var now = DateTime.UtcNow;

        var snapshot = await _db.DashboardSnapshots
            .FirstOrDefaultAsync(d => d.TenantId == tenantId.Value);

        var arRaw = await _db.Invoices
            .Where(i => i.TenantId == tenantId.Value && i.BalanceRemaining > 0)
            .Select(i => new { i.CustomerName, i.StCustomerId, i.BalanceRemaining, i.InvoiceDate })
            .ToListAsync();

        var arAging = new
        {
            Bucket0_30   = arRaw.Where(i => (now - i.InvoiceDate).TotalDays <= 30).Sum(i => i.BalanceRemaining),
            Bucket31_60  = arRaw.Where(i => (now - i.InvoiceDate).TotalDays is > 30 and <= 60).Sum(i => i.BalanceRemaining),
            Bucket61_90  = arRaw.Where(i => (now - i.InvoiceDate).TotalDays is > 60 and <= 90).Sum(i => i.BalanceRemaining),
            Bucket90Plus = arRaw.Where(i => (now - i.InvoiceDate).TotalDays > 90).Sum(i => i.BalanceRemaining),
        };

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

        var openWos = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value
                && (j.Status.ToLower().Contains("inprogress")
                    || j.Status.ToLower().Contains("scheduled")
                    || j.Status.ToLower().Contains("hold")))
            .OrderBy(j => j.CreatedOn)
            .Select(j => new { j.StJobId, j.JobNumber, j.CustomerName, j.Status, j.TotalAmount, j.CreatedOn })
            .ToListAsync();

        var oldestWoDays = openWos.Any(w => w.CreatedOn.HasValue)
            ? (int)(now - openWos.Where(w => w.CreatedOn.HasValue).Min(w => w.CreatedOn!.Value)).TotalDays
            : 0;

        var overduePms = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && p.PmStatus == "Overdue")
            .OrderBy(p => p.LastPmDate)
            .Select(p => new
            {
                p.CustomerName,
                p.LastPmDate,
                DaysSince = p.LastPmDate.HasValue ? (int)(now - p.LastPmDate.Value).TotalDays : 0
            })
            .ToListAsync();

        TimeZoneInfo centralZone;
        try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        catch { try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
                catch { centralZone = TimeZoneInfo.Utc; } }

        var nowCentral    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralZone);
        var todayLocal    = nowCentral.Date;
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

        // AP summary
        var unpaidAp = await _db.ApBills
            .Where(b => b.TenantId == tenantId.Value && !b.IsPaid)
            .SumAsync(b => b.Amount);
        var apNextDue = await _db.ApBills
            .Where(b => b.TenantId == tenantId.Value && !b.IsPaid)
            .OrderBy(b => b.DueDate)
            .Select(b => (DateTime?)b.DueDate)
            .FirstOrDefaultAsync();
        var apNextDueDays = apNextDue.HasValue ? (int)(apNextDue.Value - DateTime.UtcNow).TotalDays : 0;

        return Ok(new
        {
            Synced = snapshot != null,
            SnapshotTakenAt = snapshot?.SnapshotTakenAt,
            TotalAR = totalAr,
            TotalAP = unpaidAp,
            ApNextDueDays = apNextDueDays,
            NetPosition = totalAr - unpaidAp,
            ArOldestDays = arRaw.Any() ? (int)(now - arRaw.Min(i => i.InvoiceDate)).TotalDays : 0,
            ARaging = arAging,
            ARbyCustomer = arByCustomer,
            RevenueThisMonth = revenueThisMonth,
            RevenueLastMonth = revenueLastMonth,
            ForecastedRevenue = now.Day > 0 ? revenueThisMonth / now.Day * DateTime.DaysInMonth(now.Year, now.Month) : 0,
            DaysInMonth = DateTime.DaysInMonth(now.Year, now.Month),
            DaysElapsed = now.Day,
            NeedToSchedule = openWos.Count(w => w.Status == "Hold"),
            OpenWorkOrders = openWos,
            OpenWoCount = openWos.Count,
            OldestWoDays = oldestWoDays,
            OverduePms = overduePms,
            OverduePmCount = overduePms.Count,
            ScheduledToday    = BuildDaySchedule(todayLocal),
            ScheduledTomorrow = BuildDaySchedule(tomorrowLocal),
            ScheduledDayAfter = BuildDaySchedule(dayAfterLocal),
            TodayLabel    = todayLocal.ToString("dddd"),
            TomorrowLabel = tomorrowLocal.ToString("dddd"),
            DayAfterLabel = dayAfterLocal.ToString("dddd"),
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

        return await GetDashboard();
    }

    [HttpGet("pm-tracker")]
    public async Task<IActionResult> GetPmTracker()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.LastPmDate)
            .Select(p => new { p.StCustomerId, p.CustomerName, p.LastPmDate, p.PmStatus, p.UpdatedAt })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("debug-counts")]
    public async Task<IActionResult> DebugCounts()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var invoiceCount  = await _db.Invoices.CountAsync(i => i.TenantId == tenantId.Value);
        var jobCount      = await _db.Jobs.CountAsync(j => j.TenantId == tenantId.Value);
        var apptCount     = await _db.Appointments.CountAsync(a => a.TenantId == tenantId.Value);
        var pmCount       = await _db.PmCustomers.CountAsync(p => p.TenantId == tenantId.Value);
        var customerCount = await _db.Customers.CountAsync(c => c.TenantId == tenantId.Value);

        var jobSample = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value)
            .OrderByDescending(j => j.UpdatedAt)
            .Take(5)
            .Select(j => new { j.JobNumber, j.Status, j.CustomerName, j.CreatedOn })
            .ToListAsync();

        var invoiceSample = await _db.Invoices
            .Where(i => i.TenantId == tenantId.Value)
            .OrderByDescending(i => i.UpdatedAt)
            .Take(5)
            .Select(i => new { i.StInvoiceId, i.CustomerName, i.BalanceRemaining, i.TotalAmount })
            .ToListAsync();

        return Ok(new
        {
            TenantId = tenantId.Value,
            InvoiceCount  = invoiceCount,
            JobCount      = jobCount,
            AppointmentCount = apptCount,
            PmCustomerCount  = pmCount,
            CustomerCount    = customerCount,
            JobSample     = jobSample,
            InvoiceSample = invoiceSample
        });
    }

    [HttpGet("job-history/{stJobId}")]
    public async Task<IActionResult> GetJobHistory(long stJobId)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        var raw = await _sync.GetJobHistoryRawAsync(tenantId.Value, stJobId);
        return Content(raw, "application/json");
    }

    [HttpGet("job-detail/{stJobId}")]
    public async Task<IActionResult> GetJobDetail(long stJobId)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        var raw = await _sync.GetJobRawAsync(tenantId.Value, stJobId);
        return Content(raw, "application/json");
    }

    [HttpGet("raw-job-export")]
    public async Task<IActionResult> GetRawJobExport()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        var raw = await _sync.GetRawJobExportAsync(tenantId.Value);
        return Content(raw, "application/json");
    }

    [HttpGet("appointment/{apptId}")]
    public async Task<IActionResult> GetAppointment(long apptId)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        var raw = await _sync.GetAppointmentRawAsync(tenantId.Value, apptId);
        return Content(raw, "application/json");
    }






}
