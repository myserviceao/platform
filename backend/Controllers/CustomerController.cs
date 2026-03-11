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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var pmMap = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value)
            .ToDictionaryAsync(p => p.StCustomerId);

        var result = customers.Select(c =>
        {
            pmMap.TryGetValue(c.StCustomerId, out var pm);
            return new
            {
                id = c.Id.ToString(),
                name = c.Name,
                serviceTitanCustomerId = c.StCustomerId,
                lastPmDate = pm?.LastPmDate,
                pmStatus = pm?.PmStatus ?? "NoPm",
                updatedAt = c.UpdatedAt
            };
        });

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customer = await _db.Customers
            .Where(c => c.TenantId == tenantId.Value && c.Id == id)
            .FirstOrDefaultAsync();

        if (customer == null) return NotFound();

        var pm = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && p.StCustomerId == customer.StCustomerId)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            id = customer.Id.ToString(),
            name = customer.Name,
            serviceTitanCustomerId = customer.StCustomerId,
            lastPmDate = pm?.LastPmDate,
            pmStatus = pm?.PmStatus ?? "NoPm",
            updatedAt = customer.UpdatedAt
        });
    }
}
