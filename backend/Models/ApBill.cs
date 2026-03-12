namespace MyServiceAO.Models;

public class ApBill
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;

    public string InvoiceNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long StApBillId { get; set; }
    public long? StPurchaseOrderId { get; set; }
    public string? Status { get; set; }       // Unreconciled, Reconciled, Discrepancy, Canceled
    public string? Source { get; set; }       // Standalone, Purchasing, API, OCR
    public string? ReferenceNumber { get; set; }
    public string? Summary { get; set; }
    public DateTime? BillDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
