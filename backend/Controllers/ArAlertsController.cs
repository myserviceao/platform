using Microsoft.AspNetCore.Mvc;
using MyServiceAO.Services;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/ar-alerts")]
public class ArAlertsController : ControllerBase
{
    private readonly ArAlertsService _arAlerts;

    public ArAlertsController(ArAlertsService arAlerts)
    {
        _arAlerts = arAlerts;
    }

    private int? TenantId => HttpContext.Session.GetInt32("tenantId");

    // GET /api/ar-alerts/summary
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        if (TenantId == null) return Unauthorized();
        var result = await _arAlerts.GetAgingSummaryAsync(TenantId.Value);
        return Ok(result);
    }

    // GET /api/ar-alerts/customers?search=&bucket=30&status=active&page=1&pageSize=25
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(string? search, string? bucket, string? status, int page = 1, int pageSize = 25)
    {
        if (TenantId == null) return Unauthorized();
        var result = await _arAlerts.GetCustomerArListAsync(TenantId.Value, search, bucket, status, page, pageSize);
        return Ok(result);
    }

    // GET /api/ar-alerts/customers/{customerId}
    [HttpGet("customers/{customerId:int}")]
    public async Task<IActionResult> CustomerDetail(int customerId)
    {
        if (TenantId == null) return Unauthorized();
        var result = await _arAlerts.GetCustomerDetailAsync(TenantId.Value, customerId);
        if (result == null) return NotFound();
        return Ok(result);
    }

    // POST /api/ar-alerts/customers/{customerId}/contact
    [HttpPost("customers/{customerId:int}/contact")]
    public async Task<IActionResult> LogContact(int customerId, [FromBody] LogContactRequest req)
    {
        if (TenantId == null) return Unauthorized();
        await _arAlerts.LogContactAsync(TenantId.Value, customerId, req.ContactType, req.Outcome, req.Notes, req.FollowUpDate);
        return Ok(new { success = true });
    }

    // PUT /api/ar-alerts/customers/{customerId}/status
    [HttpPut("customers/{customerId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int customerId, [FromBody] UpdateStatusRequest req)
    {
        if (TenantId == null) return Unauthorized();
        await _arAlerts.UpdateStatusAsync(TenantId.Value, customerId, req.Status, req.PaymentPlanAmount, req.PaymentPlanNote);
        return Ok(new { success = true });
    }

    public class LogContactRequest
    {
        public string ContactType { get; set; } = "call";
        public string Outcome { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime? FollowUpDate { get; set; }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = "active";
        public decimal? PaymentPlanAmount { get; set; }
        public string? PaymentPlanNote { get; set; }
    }
}
