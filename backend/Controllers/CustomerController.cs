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

        var customers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.CustomerName)
            .Select(p => new
            {
                id = p.Id.ToString(),
                name = p.CustomerName,
                serviceTitanCustomerId = p.StCustomerId,
                lastPmDate = p.LastPmDate,
                pmStatus = p.PmStatus,
                updatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var customer = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId.Value && p.Id == id)
            .Select(p => new
            {
                id = p.Id.ToString(),
                name = p.CustomerName,
                serviceTitanCustomerId = p.StCustomerId,
                lastPmDate = p.LastPmDate,
                pmStatus = p.PmStatus,
                updatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (customer == null) return NotFound();

        return Ok(customer);
    }
}
