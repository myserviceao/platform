using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/pm-outreach")]
public class PmOutreachController : ControllerBase
{
    private readonly AppDbContext _db;

    public PmOutreachController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/pm-outreach
    /// Returns all PM customers with contact info and generated outreach messages.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOutreachList()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value);
        var companyName = tenant?.Name ?? "our company";

        // Get all PM customers
        var pmCustomers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.LastPmDate)
            .ToListAsync();

        // Get all customers (for contact info)
        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value)
            .ToDictionaryAsync(c => c.StCustomerId);

        // Get customers who have NEVER had a PM (not in PmCustomers table)
        var pmCustomerIds = pmCustomers.Select(p => p.StCustomerId).ToHashSet();
        var noPmCustomers = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value && !pmCustomerIds.Contains(c.StCustomerId))
            .ToListAsync();

        var results = new List<object>();

        // Overdue and Coming Due from PmCustomers
        foreach (var pm in pmCustomers.Where(p => p.PmStatus == "Overdue" || p.PmStatus == "ComingDue"))
        {
            customers.TryGetValue(pm.StCustomerId, out var cust);
            var daysSince = pm.LastPmDate.HasValue
                ? (int)(DateTime.UtcNow - pm.LastPmDate.Value).TotalDays
                : 0;

            results.Add(new
            {
                stCustomerId = pm.StCustomerId,
                customerName = pm.CustomerName,
                phone = cust?.Phone,
                email = cust?.Email,
                pmStatus = pm.PmStatus,
                lastPmDate = pm.LastPmDate,
                daysSince,
                message = GenerateMessage(pm.PmStatus, pm.CustomerName, daysSince, companyName),
                subject = GenerateSubject(pm.PmStatus, companyName)
            });
        }

        // No PM customers
        foreach (var cust in noPmCustomers)
        {
            results.Add(new
            {
                stCustomerId = cust.StCustomerId,
                customerName = cust.Name,
                phone = cust.Phone,
                email = cust.Email,
                pmStatus = "NoPm",
                lastPmDate = (DateTime?)null,
                daysSince = 0,
                message = GenerateMessage("NoPm", cust.Name, 0, companyName),
                subject = GenerateSubject("NoPm", companyName)
            });
        }

        return Ok(new
        {
            companyName,
            overdueCount = results.Count(r => ((dynamic)r).pmStatus == "Overdue"),
            comingDueCount = results.Count(r => ((dynamic)r).pmStatus == "ComingDue"),
            noPmCount = results.Count(r => ((dynamic)r).pmStatus == "NoPm"),
            customers = results
        });
    }

    private static string GenerateMessage(string status, string customerName, int daysSince, string companyName)
    {
        var firstName = customerName.Split(' ')[0];

        return status switch
        {
            "Overdue" => $"Hi {firstName},\n\nThis is {companyName}. We wanted to reach out because it has been {daysSince} days since your last preventive maintenance service. Regular maintenance is essential for keeping your HVAC system running efficiently, preventing costly breakdowns, and extending the life of your equipment.\n\nWe recommend scheduling a maintenance visit as soon as possible to ensure everything is operating at peak performance. Our team is ready to get you on the schedule at a time that works for you.\n\nPlease give us a call or reply to this message to set up your appointment. We look forward to hearing from you!",

            "ComingDue" => $"Hi {firstName},\n\nThis is {companyName}. We wanted to let you know that your preventive maintenance is coming due. It has been {daysSince} days since your last service, and we recommend scheduling your next PM visit soon to keep your system in top shape.\n\nRegular maintenance helps prevent unexpected breakdowns, improves energy efficiency, and keeps your warranty intact. We have availability opening up and would love to get you scheduled before your system goes too long without service.\n\nReply to this message or give us a call to book your appointment!",

            "NoPm" => $"Hi {firstName},\n\nThis is {companyName}. We noticed that we don't have a preventive maintenance visit on record for your HVAC system. Regular maintenance is one of the best investments you can make for your comfort and your wallet.\n\nA routine PM visit includes a thorough inspection, cleaning, and tune-up of your system. This helps catch small issues before they become expensive repairs, improves energy efficiency, and extends the life of your equipment.\n\nWe'd love to get you started with a maintenance plan. Reply to this message or give us a call to schedule your first visit!",

            _ => ""
        };
    }

    private static string GenerateSubject(string status, string companyName)
    {
        return status switch
        {
            "Overdue" => $"Your HVAC Maintenance is Overdue - {companyName}",
            "ComingDue" => $"Your HVAC Maintenance is Coming Due - {companyName}",
            "NoPm" => $"Schedule Your First HVAC Maintenance - {companyName}",
            _ => $"HVAC Maintenance Reminder - {companyName}"
        };
    }
}
