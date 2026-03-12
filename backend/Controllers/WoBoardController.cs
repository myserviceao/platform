using MyServiceAO.Services.ServiceTitan;
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
    /// Returns on-hold jobs with hold reasons for the Hold Board.
    /// </summary>
    [HttpGet("holds")]
    public async Task<IActionResult> GetHoldBoard()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

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

        var holdReasons = await _db.HoldReasons
            .Where(h => h.TenantId == tenantId.Value && h.Active)
            .OrderBy(h => h.Name)
            .Select(h => h.Name)
            .ToListAsync();

        // Build columns from hold reasons
        var columns = new List<object>();
        var assignedIds = new HashSet<int>();

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
            columns.Add(new { key = "unassigned", label = "Unassigned", count = unmatched.Count, jobs = unmatched });

        return Ok(new
        {
            totalHolds = holdJobs.Count,
            holdReasonCount = holdReasons.Count,
            totalAmount = holdJobs.Sum(j => j.TotalAmount),
            holdReasons,
            columns
        });
    }

    /// <summary>
    /// PUT /api/wo-board/holds/{jobId}/reason
    /// Manually assign a hold reason to a job.
    /// </summary>
    [HttpPut("holds/{jobId}/reason")]
    public async Task<IActionResult> SetHoldReason(int jobId, [FromBody] SetHoldReasonRequest req,
        [FromServices] ServiceTitanClient stClient, [FromServices] ServiceTitanOAuthService oauth)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId && j.TenantId == tenantId.Value);
        if (job == null) return NotFound();

        job.HoldReasonName = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason;
        await _db.SaveChangesAsync();

        // Push tag to ST (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var token = await oauth.GetValidTokenAsync(tenantId.Value);
                if (token == null) return;
                var tenant = await _db.Tenants.FindAsync(tenantId.Value);
                if (tenant?.StTenantId == null) return;

                // Get all hold reason tag mappings
                var holdReasons = await _db.HoldReasons
                    .Where(h => h.TenantId == tenantId.Value && h.StTagTypeId != null)
                    .ToListAsync();
                var allBoardTagIds = holdReasons.Select(h => h.StTagTypeId!.Value).ToHashSet();
                if (allBoardTagIds.Count == 0) return;

                // Find the target tag for the assigned reason
                long? newTagId = null;
                if (!string.IsNullOrWhiteSpace(req.Reason))
                {
                    var matched = holdReasons.FirstOrDefault(h =>
                        h.Name.Equals(req.Reason, StringComparison.OrdinalIgnoreCase));
                    newTagId = matched?.StTagTypeId;
                }

                // Get current job tags from ST
                var jobRaw = await stClient.GetJobAsync(token, tenant.StTenantId, job.StJobId);
                var jobDoc = System.Text.Json.JsonDocument.Parse(jobRaw);
                var mergedIds = new List<long>();
                if (jobDoc.RootElement.TryGetProperty("tagTypeIds", out var existingTags) &&
                    existingTags.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var t in existingTags.EnumerateArray())
                    {
                        var id = t.GetInt64();
                        if (!allBoardTagIds.Contains(id)) mergedIds.Add(id); // keep non-board tags
                    }
                }
                if (newTagId.HasValue) mergedIds.Add(newTagId.Value);

                await stClient.UpdateJobTagsAsync(token, tenant.StTenantId, job.StJobId, mergedIds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ST Tag Sync] Failed for job {job.JobNumber}: {ex.Message}");
            }
        });

        return Ok(new { job.Id, job.JobNumber, holdReasonName = job.HoldReasonName });
    }

    public class SetHoldReasonRequest
    {
        public string? Reason { get; set; }
    }

}
