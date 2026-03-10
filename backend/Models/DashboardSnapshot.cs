namespace MyServiceAO.Models;

/// <summary>
/// Stores the latest synced KPI values per tenant.
/// One row per tenant — upserted on every sync.
/// </summary>
public class DashboardSnapshot
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    // Revenue
    public decimal RevenueThisMonth { get; set; }
    public decimal RevenueLastMonth { get; set; }

    // Accounts Receivable — sum of all outstanding invoice balances
    public decimal AccountsReceivable { get; set; }
    public int UnpaidInvoiceCount { get; set; }

    // Open Work Orders
    public int OpenJobCount { get; set; }

    // Overdue PMs — recurring service events past due date, not completed
    public int OverduePmCount { get; set; }

    public DateTime SnapshotTakenAt { get; set; } = DateTime.UtcNow;
}