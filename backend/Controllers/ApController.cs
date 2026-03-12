using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/ap")]
public class ApController : ControllerBase
{
    private readonly AppDbContext _db;
    public ApController(AppDbContext db) { _db = db; }

    // ── Vendors ────────────────────────────────────────────────

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var vendors = await _db.Vendors
            .Where(v => v.TenantId == tenantId.Value)
            .OrderBy(v => v.Name)
            .Select(v => new { v.Id, v.Name, v.ContactName, v.Phone, v.Email })
            .ToListAsync();

        return Ok(vendors);
    }

    [HttpPost("vendors")]
    public async Task<IActionResult> CreateVendor([FromBody] CreateVendorRequest req)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required" });

        var vendor = new Vendor
        {
            TenantId = tenantId.Value,
            Name = req.Name.Trim(),
            ContactName = req.ContactName?.Trim(),
            Phone = req.Phone?.Trim(),
            Email = req.Email?.Trim()
        };

        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();

        return Ok(new { vendor.Id, vendor.Name, vendor.ContactName, vendor.Phone, vendor.Email });
    }

    [HttpDelete("vendors/{id:int}")]
    public async Task<IActionResult> DeleteVendor(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.TenantId == tenantId.Value && v.Id == id);
        if (vendor == null) return NotFound();

        // Check for associated bills
        var hasBills = await _db.ApBills.AnyAsync(b => b.VendorId == id);
        if (hasBills) return BadRequest(new { error = "Cannot delete vendor with existing bills. Mark bills as paid first." });

        _db.Vendors.Remove(vendor);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }

    // ── Bills ──────────────────────────────────────────────────

    [HttpGet("bills")]
    public async Task<IActionResult> GetBills([FromQuery] bool? unpaidOnly)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var query = _db.ApBills
            .Where(b => b.TenantId == tenantId.Value)
            .Include(b => b.Vendor)
            .AsQueryable();

        if (unpaidOnly == true)
            query = query.Where(b => !b.IsPaid);

        var bills = await query
            .OrderBy(b => b.DueDate)
            .Select(b => new
            {
                b.Id,
                vendorId = b.VendorId,
                vendorName = b.Vendor.Name,
                b.InvoiceNumber,
                b.Amount,
                b.DueDate,
                b.IsPaid,
                b.PaidDate
            })
            .ToListAsync();

        return Ok(bills);
    }

    [HttpPost("bills")]
    public async Task<IActionResult> CreateBill([FromBody] CreateBillRequest req)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.TenantId == tenantId.Value && v.Id == req.VendorId);
        if (vendor == null) return BadRequest(new { error = "Vendor not found" });
        if (req.Amount <= 0) return BadRequest(new { error = "Amount must be positive" });

        var bill = new ApBill
        {
            TenantId = tenantId.Value,
            VendorId = req.VendorId,
            InvoiceNumber = req.InvoiceNumber?.Trim() ?? "",
            Amount = req.Amount,
            DueDate = req.DueDate.ToUniversalTime()
        };

        _db.ApBills.Add(bill);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            bill.Id,
            vendorId = bill.VendorId,
            vendorName = vendor.Name,
            bill.InvoiceNumber,
            bill.Amount,
            bill.DueDate,
            bill.IsPaid
        });
    }

    [HttpPut("bills/{id:int}/pay")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var bill = await _db.ApBills.FirstOrDefaultAsync(b => b.TenantId == tenantId.Value && b.Id == id);
        if (bill == null) return NotFound();

        bill.IsPaid = true;
        bill.PaidDate = DateTime.UtcNow;
        bill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { bill.Id, bill.IsPaid, bill.PaidDate });
    }

    [HttpPut("bills/{id:int}/unpay")]
    public async Task<IActionResult> MarkUnpaid(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var bill = await _db.ApBills.FirstOrDefaultAsync(b => b.TenantId == tenantId.Value && b.Id == id);
        if (bill == null) return NotFound();

        bill.IsPaid = false;
        bill.PaidDate = null;
        bill.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { bill.Id, bill.IsPaid });
    }

    [HttpDelete("bills/{id:int}")]
    public async Task<IActionResult> DeleteBill(int id)
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var bill = await _db.ApBills.FirstOrDefaultAsync(b => b.TenantId == tenantId.Value && b.Id == id);
        if (bill == null) return NotFound();

        _db.ApBills.Remove(bill);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }

    // ── Dashboard summary ──────────────────────────────────────

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var unpaidBills = await _db.ApBills
            .Where(b => b.TenantId == tenantId.Value && !b.IsPaid)
            .Include(b => b.Vendor)
            .ToListAsync();

        var totalAp = unpaidBills.Sum(b => b.Amount);
        var nextDueDate = unpaidBills.Any() ? unpaidBills.Min(b => b.DueDate) : (DateTime?)null;
        var nextDueDays = nextDueDate.HasValue ? (int)(nextDueDate.Value - DateTime.UtcNow).TotalDays : 0;

        var byVendor = unpaidBills
            .GroupBy(b => new { b.VendorId, b.Vendor.Name })
            .Select(g => new
            {
                vendorName = g.Key.Name,
                totalOwed = g.Sum(b => b.Amount),
                invoiceCount = g.Count(),
                nextDue = g.Min(b => b.DueDate)
            })
            .OrderByDescending(v => v.totalOwed)
            .ToList();

        return Ok(new
        {
            totalAp,
            nextDueDate,
            nextDueDays,
            vendorCount = byVendor.Count,
            byVendor
        });
    }
}

public class CreateVendorRequest
{
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class CreateBillRequest
{
    public int VendorId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }

    // ── Purchase Orders ────────────────────────────────────────

    [HttpGet("purchase-orders")]
    public async Task<IActionResult> GetPurchaseOrders()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var pos = await _db.PurchaseOrders
            .Where(p => p.TenantId == tenantId.Value)
            .OrderByDescending(p => p.Date)
            .Include(p => p.Items)
            .Select(p => new
            {
                p.Id, p.StPurchaseOrderId, p.Number, p.Status, p.VendorName,
                p.JobNumber, p.Total, p.Tax, p.Shipping, p.Summary,
                p.Date, p.RequiredOn, p.SentOn, p.ReceivedOn,
                ItemCount = p.Items.Count,
                Items = p.Items.Select(i => new
                {
                    i.SkuName, i.SkuCode, i.Description,
                    i.Quantity, i.QuantityReceived, i.Cost, i.Total, i.Status
                })
            })
            .ToListAsync();

        return Ok(pos);
    }

    // ── Enhanced Bills ─────────────────────────────────────────

    [HttpGet("bills-enhanced")]
    public async Task<IActionResult> GetBillsEnhanced()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var bills = await _db.ApBills
            .Where(b => b.TenantId == tenantId.Value)
            .Include(b => b.Vendor)
            .OrderByDescending(b => b.DueDate)
            .Select(b => new
            {
                b.Id, b.StApBillId, b.InvoiceNumber, b.Amount, b.DueDate,
                b.IsPaid, b.PaidDate, b.StPurchaseOrderId,
                b.Status, b.Source, b.ReferenceNumber, b.Summary, b.BillDate,
                VendorName = b.Vendor != null ? b.Vendor.Name : ""
            })
            .ToListAsync();

        return Ok(bills);
    }

    // ── AP Summary ─────────────────────────────────────────────

    [HttpGet("summary")]
    public async Task<IActionResult> GetApSummary()
    {
        var tenantId = HttpContext.Session.GetInt32("tenantId");
        if (tenantId == null) return Unauthorized();

        var unpaid = await _db.ApBills
            .Where(b => b.TenantId == tenantId.Value && !b.IsPaid)
            .ToListAsync();

        var pos = await _db.PurchaseOrders
            .Where(p => p.TenantId == tenantId.Value)
            .ToListAsync();

        var now = DateTime.UtcNow;

        return Ok(new
        {
            TotalUnpaid = unpaid.Sum(b => b.Amount),
            BillCount = unpaid.Count,
            OverdueCount = unpaid.Count(b => b.DueDate < now),
            OverdueAmount = unpaid.Where(b => b.DueDate < now).Sum(b => b.Amount),
            DueThisWeek = unpaid.Where(b => b.DueDate >= now && b.DueDate < now.AddDays(7)).Sum(b => b.Amount),
            DueThisMonth = unpaid.Where(b => b.DueDate >= now && b.DueDate < now.AddDays(30)).Sum(b => b.Amount),
            PoCount = pos.Count,
            OpenPoCount = pos.Count(p => p.Status != "Closed" && p.Status != "Canceled"),
            OpenPoTotal = pos.Where(p => p.Status != "Closed" && p.Status != "Canceled").Sum(p => p.Total),
            ByVendor = unpaid.GroupBy(b => b.Vendor?.Name ?? "Unknown")
                .Select(g => new { Vendor = g.Key, Total = g.Sum(b => b.Amount), Count = g.Count() })
                .OrderByDescending(g => g.Total)
                .ToList()
        });
    }

}
