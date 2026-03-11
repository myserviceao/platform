using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/customers
    /// Returns all customers for the current tenant with balance and open WO summary.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value)
            .OrderBy(c => c.Name)
            .ToListAsync();

        // Pre-load invoice balances per customer
        var balanceMap = await _db.Invoices
            .Where(i => i.TenantId == tenantId.Value && i.BalanceRemaining > 0)
            .GroupBy(i => i.StCustomerId)
            .Select(g => new { StCustomerId = g.Key, Total = g.Sum(i => i.BalanceRemaining) })
            .ToDictionaryAsync(x => x.StCustomerId, x => x.Total);

        // Pre-load open WO counts per customer
        var openStatuses = new[] { "InProgress", "Scheduled", "Hold", "Dispatched" };
        var openWoMap = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value && openStatuses.Contains(j.Status))
            .GroupBy(j => j.StCustomerId)
            .Select(g => new { StCustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StCustomerId, x => x.Count);

        var result = customers.Select(c =>
        {
            balanceMap.TryGetValue(c.StCustomerId, out var balance);
            openWoMap.TryGetValue(c.StCustomerId, out var openWos);
            return new
            {
                id = c.Id.ToString(),
                name = c.Name,
                serviceTitanCustomerId = c.StCustomerId,
                totalBalance = balance,
                openWoCount = openWos,
                updatedAt = c.UpdatedAt
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// GET /api/customers/{id}
    /// Returns full customer detail with jobs, invoices, and summary stats.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customer = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value && c.Id == id)
            .FirstOrDefaultAsync();

        if (customer == null) return NotFound();

        // PM status
        var pm = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && p.StCustomerId == customer.StCustomerId)
            .FirstOrDefaultAsync();

        // Jobs for this customer
        var jobs = await _db.Jobs
            .Where(j => j.TenantId == tenantId.Value && j.StCustomerId == customer.StCustomerId)
            .OrderByDescending(j => j.CreatedOn)
            .ToListAsync();

        // Invoices for this customer
        var invoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId.Value && i.StCustomerId == customer.StCustomerId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        var totalBalance = invoices.Sum(i => i.BalanceRemaining);
        var openStatuses = new[] { "InProgress", "Scheduled", "Hold", "Dispatched" };
        var openWoCount = jobs.Count(j => openStatuses.Contains(j.Status));

        return Ok(new
        {
            id = customer.Id.ToString(),
            name = customer.Name,
            serviceTitanCustomerId = customer.StCustomerId,
            totalBalance,
            openWoCount,
            jobCount = jobs.Count,
            lastPmDate = pm?.LastPmDate,
            pmStatus = pm?.PmStatus ?? "NoPm",
            jobs = jobs.Select(j => new
            {
                jobNumber = j.JobNumber,
                jobTypeName = j.JobTypeName,
                status = j.Status,
                createdOn = j.CreatedOn,
                totalAmount = j.TotalAmount
            }),
            invoices = invoices.Select(i => new
            {
                stInvoiceId = i.StInvoiceId,
                invoiceDate = i.InvoiceDate,
                totalAmount = i.TotalAmount,
                balanceRemaining = i.BalanceRemaining
            })
        });
    }
}