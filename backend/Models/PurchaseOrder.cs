namespace MyServiceAO.Models;

public class PurchaseOrder
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StPurchaseOrderId { get; set; }
    public string Number { get; set; } = "";
    public string Status { get; set; } = "";         // Pending, Open, Sent, PartiallyReceived, FullyReceived, Closed, Canceled
    public string VendorName { get; set; } = "";
    public long StVendorId { get; set; }
    public long? StJobId { get; set; }
    public string? JobNumber { get; set; }

    public decimal Total { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public string? Summary { get; set; }

    public DateTime Date { get; set; }
    public DateTime? RequiredOn { get; set; }
    public DateTime? SentOn { get; set; }
    public DateTime? ReceivedOn { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public long StItemId { get; set; }
    public string SkuName { get; set; } = "";
    public string SkuCode { get; set; } = "";
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal Cost { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "";
}
