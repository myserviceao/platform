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

    /// <summary>
    /// GET /api/wo-board
    /// Returns all open work orders grouped by status for the Kanban board.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBoard()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var openStatuses = new[] { "Scheduled", "Dispatched", "InProgress", "Hold" };

        var jobs = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value && openStatuses.Contains(j.Status))
            .OrderByDescending(j => j.CreatedOn)
            .Select(j => new
            {
                j.Id,
                j.StJobId,
                j.JobNumber,
                j.CustomerName,
                j.Status,
                j.JobTypeName,
                j.TotalAmount,
                j.CreatedOn,
                daysSince = j.CreatedOn.HasValue
                    ? (int)(System.DateTime.UtcNow - j.CreatedOn.Value).TotalDays
                    : 0
            })
            .ToListAsync();

        // Group by status into columns
        var columns = openStatuses.Select(status => new
        {
            status,
            label = status switch
            {
                "Scheduled" => "Scheduled",
                "Dispatched" => "Dispatched",
                "InProgress" => "In Progress",
                "Hold" => "On Hold",
                _ => status
            },
            color = status switch
            {
                "Scheduled" => "info",
                "Dispatched" => "primary",
                "InProgress" => "warning",
                "Hold" => "error",
                _ => "ghost"
            },
            count = jobs.Count(j => j.Status == status),
            jobs = jobs.Where(j => j.Status == status).ToList()
        }).ToList();

        var totalAmount = jobs.Sum(j => j.TotalAmount);

        return Ok(new
        {
            totalJobs = jobs.Count,
            totalAmount,
            columns
        });
    }
}
