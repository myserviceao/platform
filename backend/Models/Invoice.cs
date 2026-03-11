namespace MyServiceAO.Models;

public class Invoice
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public long StInvoiceId { get; set; }
    public long StCustomerId { get; set; }
    public string CustomerName { get; set; } = "";

    public decimal TotalAmount { get; set; }
    public decimal BalanceRemaining { get; set; }

    public DateTime InvoiceDate { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
