using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/wo-board")]
public class WoBoardController : ControllerBase
{
    private readonly AppDbContext _db;

    public WoBoardController(AppDbContext db) { _db = db; }

    private class BoardJob
    {
        public int Id { get; set; }
        public long StJobId { get; set; }
        public string JobNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Status { get; set; } = "";
        public string? JobTypeName { get; set; }
        public string? HoldReasonName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? CreatedOn { get; set; }
        public int DaysSince { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetBoard()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var openStatuses = new[] { "Scheduled", "Dispatched", "InProgress", "Hold" };

        var jobs = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value && openStatuses.Contains(j.Status))
            .OrderByDescending(j => j.CreatedOn)
            .Select(j => new BoardJob
            {
                Id = j.Id,
                StJobId = j.StJobId,
                JobNumber = j.JobNumber,
                CustomerName = j.CustomerName,
                Status = j.Status,
                JobTypeName = j.JobTypeName,
                HoldReasonName = j.HoldReasonName,
                TotalAmount = j.TotalAmount,
                CreatedOn = j.CreatedOn,
                DaysSince = j.CreatedOn.HasValue
                    ? (int)(System.DateTime.UtcNow - j.CreatedOn.Value).TotalDays
                    : 0
            })
            .ToListAsync();

        var holdReasons = await _db.HoldReasons
            .Where(h => h.TenantId == tenantId.Value && h.Active)
            .OrderBy(h => h.Name)
            .Select(h => h.Name)
            .ToListAsync();

        var resultColumns = new List<object>();

        // Status columns
        var statusDefs = new[] {
            ("Scheduled", "Scheduled", "info"),
            ("Dispatched", "Dispatched", "primary"),
            ("InProgress", "In Progress", "warning"),
        };

        foreach (var (status, label, color) in statusDefs)
        {
            var colJobs = jobs.Where(j => j.Status == status).ToList();
            resultColumns.Add(new { key = status, label, color, isHold = false, count = colJobs.Count, jobs = colJobs });
        }

        // Hold columns
        var holdJobs = jobs.Where(j => j.Status == "Hold").ToList();
        var assignedJobIds = new HashSet<int>();

        if (holdReasons.Count > 0)
        {
            foreach (var reason in holdReasons)
            {
                var reasonJobs = holdJobs
                    .Where(j => string.Equals(j.HoldReasonName, reason, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var rj in reasonJobs) assignedJobIds.Add(rj.Id);

                resultColumns.Add(new { key = $"hold-{reason}", label = reason, color = "error", isHold = true, count = reasonJobs.Count, jobs = reasonJobs });
            }

            var unmatched = holdJobs.Where(j => !assignedJobIds.Contains(j.Id)).ToList();
            if (unmatched.Count > 0)
            {
                resultColumns.Add(new { key = "hold-other", label = "On Hold (Other)", color = "error", isHold = true, count = unmatched.Count, jobs = unmatched });
            }
        }
        else
        {
            resultColumns.Add(new { key = "hold", label = "On Hold", color = "error", isHold = true, count = holdJobs.Count, jobs = holdJobs });
        }

        return Ok(new
        {
            totalJobs = jobs.Count,
            totalAmount = jobs.Sum(j => j.TotalAmount),
            holdReasonCount = holdReasons.Count,
            columns = resultColumns
        });
    }

    /// <summary>
    /// GET /api/wo-board/holds
    /// Returns on-hold jobs grouped by hold reason for the Hold Board.
    /// Each tenant's hold reasons from ServiceTitan become columns.
    /// </summary>
    [HttpGet("holds")]
    public async Task<IActionResult> GetHoldBoard()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        // Get all on-hold jobs
        var holdJobs = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value && j.Status == "Hold")
            .OrderByDescending(j => j.CreatedOn)
            .Select(j => new BoardJob
            {
                Id = j.Id,
                StJobId = j.StJobId,
                JobNumber = j.JobNumber,
                CustomerName = j.CustomerName,
                Status = j.Status,
                JobTypeName = j.JobTypeName,
                HoldReasonName = j.HoldReasonName,
                TotalAmount = j.TotalAmount,
                CreatedOn = j.CreatedOn,
                DaysSince = j.CreatedOn.HasValue
                    ? (int)(System.DateTime.UtcNow - j.CreatedOn.Value).TotalDays
                    : 0
            })
            .ToListAsync();

        // Get hold reasons for this tenant
        var holdReasons = await _db.HoldReasons
            .Where(h => h.TenantId == tenantId.Value && h.Active)
            .OrderBy(h => h.Name)
            .Select(h => h.Name)
            .ToListAsync();

        var columns = new List<object>();
        var assignedIds = new HashSet<int>();

        if (holdReasons.Count > 0)
        {
            foreach (var reason in holdReasons)
            {
                var reasonJobs = holdJobs
                    .Where(j => string.Equals(j.HoldReasonName, reason, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var rj in reasonJobs) assignedIds.Add(rj.Id);
                columns.Add(new { key = reason, label = reason, count = reasonJobs.Count, jobs = reasonJobs });
            }

            var unmatched = holdJobs.Where(j => !assignedIds.Contains(j.Id)).ToList();
            if (unmatched.Count > 0)
            {
                columns.Add(new { key = "unassigned", label = "Unassigned", count = unmatched.Count, jobs = unmatched });
            }
        }
        else
        {
            // No hold reasons synced - show all in one column
            columns.Add(new { key = "all", label = "On Hold", count = holdJobs.Count, jobs = holdJobs });
        }

        return Ok(new
        {
            totalHolds = holdJobs.Count,
            holdReasonCount = holdReasons.Count,
            totalAmount = holdJobs.Sum(j => j.TotalAmount),
            columns
        });
    
    /// <summary>
    /// GET /api/wo-board/debug-hold/{stJobId}
    /// Debug: shows the raw ST job history for a hold job
    /// </summary>
    [HttpGet("debug-hold/{stJobId}")]
    public async Task<IActionResult> DebugHoldReason(long stJobId)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        if (tenant == null) return NotFound();

        try
        {
            var stClient = HttpContext.RequestServices.GetRequiredService<MyServiceAO.Services.ServiceTitan.ServiceTitanClient>();
            var authService = HttpContext.RequestServices.GetRequiredService<MyServiceAO.Services.ServiceTitan.ServiceTitanAuthService>();
            var token = await authService.GetAccessTokenAsync(tenant.StClientId, tenant.StClientSecret);
            var raw = await stClient.GetJobHistoryAsync(token, tenant.StTenantId, stJobId);
            return Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }

}

}
