using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;
using MyServiceAO.Services;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/outreach")]
public class OutreachController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OutreachService _outreach;

    public OutreachController(AppDbContext db, OutreachService outreach)
    {
        _db = db;
        _outreach = outreach;
    }

    private int? TenantId => HttpContext.Session.GetInt32("tenantId");

    // GET /api/outreach?type=pm_reminder&status=pending&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> List(string? type = null, string? status = null, int page = 1, int pageSize = 50)
    {
        if (TenantId == null) return Unauthorized();

        var query = _db.OutreachItems
            .Where(i => i.TenantId == TenantId.Value);

        if (!string.IsNullOrEmpty(type)) query = query.Where(i => i.Type == type);
        if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load customer data for display
        var customerIds = items.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await _db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var result = items.Select(i =>
        {
            customers.TryGetValue(i.CustomerId, out var c);
            return new
            {
                i.Id, i.CustomerId, i.JobId, i.Type, i.Channel, i.Status,
                i.FailureReason, i.Subject, i.Body,
                i.ScheduledFor, i.SentAt, i.DismissedAt, i.CreatedAt, i.UpdatedAt,
                customerName = c?.Name,
                customerPhone = c?.Phone,
                customerEmail = c?.Email,
            };
        });

        return Ok(new { items = result, total, page, pageSize });
    }

    // GET /api/outreach/stats
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        if (TenantId == null) return Unauthorized();

        var items = await _db.OutreachItems
            .Where(i => i.TenantId == TenantId.Value)
            .GroupBy(i => new { i.Type, i.Status })
            .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var sentToday = await _db.OutreachItems
            .CountAsync(i => i.TenantId == TenantId.Value && i.Status == "sent" && i.SentAt >= today);
        var sentThisWeek = await _db.OutreachItems
            .CountAsync(i => i.TenantId == TenantId.Value && i.Status == "sent" && i.SentAt >= weekAgo);

        return Ok(new
        {
            byTypeAndStatus = items,
            sentToday,
            sentThisWeek,
            totalPending = items.Where(i => i.Status == "pending").Sum(i => i.Count),
        });
    }

    // PUT /api/outreach/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] EditOutreachRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var item = await _db.OutreachItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId.Value);
        if (item == null) return NotFound();
        if (item.Status != "pending" && item.Status != "failed") return BadRequest("Can only edit pending or failed items");

        if (req.Subject != null) item.Subject = req.Subject;
        if (req.Body != null) item.Body = req.Body;
        if (req.Channel != null) item.Channel = req.Channel;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    // POST /api/outreach/{id}/send
    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id)
    {
        if (TenantId == null) return Unauthorized();
        var success = await _outreach.SendItemAsync(id, TenantId.Value);
        return Ok(new { success });
    }

    // POST /api/outreach/{id}/mark-sent — marks item as sent (user sent manually via email/sms client)
    [HttpPost("{id:int}/mark-sent")]
    public async Task<IActionResult> MarkSent(int id)
    {
        if (TenantId == null) return Unauthorized();
        var item = await _db.OutreachItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId.Value);
        if (item == null) return NotFound();
        item.Status = "sent";
        item.SentAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // POST /api/outreach/{id}/retry
    [HttpPost("{id:int}/retry")]
    public async Task<IActionResult> Retry(int id)
    {
        if (TenantId == null) return Unauthorized();
        var item = await _db.OutreachItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId.Value);
        if (item == null) return NotFound();
        item.Status = "pending";
        item.FailureReason = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var success = await _outreach.SendItemAsync(id, TenantId.Value);
        return Ok(new { success });
    }

    // POST /api/outreach/bulk-send
    [HttpPost("bulk-send")]
    public async Task<IActionResult> BulkSend([FromBody] BulkRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var (sent, failed) = await _outreach.BulkSendAsync(req.Ids, TenantId.Value);
        return Ok(new { sent, failed });
    }

    // POST /api/outreach/{id}/dismiss
    [HttpPost("{id:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        if (TenantId == null) return Unauthorized();
        await _outreach.DismissItemAsync(id, TenantId.Value);
        return Ok();
    }

    // POST /api/outreach/bulk-dismiss
    [HttpPost("bulk-dismiss")]
    public async Task<IActionResult> BulkDismiss([FromBody] BulkRequest req)
    {
        if (TenantId == null) return Unauthorized();
        await _outreach.BulkDismissAsync(req.Ids, TenantId.Value);
        return Ok();
    }

    // ── Templates ──

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates()
    {
        if (TenantId == null) return Unauthorized();
        var templates = await _db.OutreachTemplates
            .Where(t => t.TenantId == TenantId.Value)
            .OrderBy(t => t.Type).ThenBy(t => t.Channel)
            .ToListAsync();
        return Ok(templates);
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var template = new OutreachTemplate
        {
            TenantId = TenantId.Value,
            Name = req.Name,
            Type = req.Type,
            Channel = req.Channel,
            Subject = req.Subject,
            Body = req.Body,
        };
        _db.OutreachTemplates.Add(template);
        await _db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> EditTemplate(int id, [FromBody] TemplateRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var template = await _db.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId.Value);
        if (template == null) return NotFound();
        template.Name = req.Name;
        template.Type = req.Type;
        template.Channel = req.Channel;
        template.Subject = req.Subject;
        template.Body = req.Body;
        template.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        if (TenantId == null) return Unauthorized();
        var template = await _db.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId.Value);
        if (template == null) return NotFound();
        if (template.IsDefault) return BadRequest("Cannot delete default templates");
        _db.OutreachTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── Campaigns ──

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CampaignRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var count = await _outreach.GenerateSeasonalCampaignAsync(TenantId.Value, req.TemplateId, req.Segment, req.MonthsThreshold);
        return Ok(new { generated = count });
    }

    // ── Settings ──

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        if (TenantId == null) return Unauthorized();
        var settings = await _db.OutreachSettings.FirstOrDefaultAsync(s => s.TenantId == TenantId.Value);
        if (settings == null)
            return Ok(new { winBackThresholdMonths = 12, postServiceDelayHours = 48, emailConfigured = false, smsConfigured = false });

        return Ok(new
        {
            settings.WinBackThresholdMonths,
            settings.PostServiceDelayHours,
            resendApiKey = OutreachService.MaskSecret(settings.ResendApiKey),
            settings.ResendFromEmail,
            twilioAccountSid = OutreachService.MaskSecret(settings.TwilioAccountSid),
            twilioAuthToken = OutreachService.MaskSecret(settings.TwilioAuthToken),
            settings.TwilioFromPhone,
            emailConfigured = !string.IsNullOrEmpty(settings.ResendApiKey),
            smsConfigured = !string.IsNullOrEmpty(settings.TwilioAccountSid),
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] SettingsRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var settings = await _db.OutreachSettings.FirstOrDefaultAsync(s => s.TenantId == TenantId.Value);
        if (settings == null)
        {
            settings = new OutreachSettings { TenantId = TenantId.Value };
            _db.OutreachSettings.Add(settings);
        }

        if (req.WinBackThresholdMonths.HasValue) settings.WinBackThresholdMonths = req.WinBackThresholdMonths.Value;
        if (req.PostServiceDelayHours.HasValue) settings.PostServiceDelayHours = req.PostServiceDelayHours.Value;

        // Only update credentials if not masked value
        if (req.ResendApiKey != null && !req.ResendApiKey.StartsWith("****"))
            settings.ResendApiKey = req.ResendApiKey;
        if (req.ResendFromEmail != null) settings.ResendFromEmail = req.ResendFromEmail;
        if (req.TwilioAccountSid != null && !req.TwilioAccountSid.StartsWith("****"))
            settings.TwilioAccountSid = req.TwilioAccountSid;
        if (req.TwilioAuthToken != null && !req.TwilioAuthToken.StartsWith("****"))
            settings.TwilioAuthToken = req.TwilioAuthToken;
        if (req.TwilioFromPhone != null) settings.TwilioFromPhone = req.TwilioFromPhone;

        await _db.SaveChangesAsync();
        return Ok();
    }
}

// ── Request DTOs ──

public record EditOutreachRequest(string? Subject, string? Body, string? Channel);
public record BulkRequest(List<int> Ids);
public record TemplateRequest(string Name, string Type, string Channel, string? Subject, string Body);
public record CampaignRequest(int TemplateId, string Segment, int? MonthsThreshold);
public record SettingsRequest(int? WinBackThresholdMonths, int? PostServiceDelayHours, string? ResendApiKey, string? ResendFromEmail, string? TwilioAccountSid, string? TwilioAuthToken, string? TwilioFromPhone);
