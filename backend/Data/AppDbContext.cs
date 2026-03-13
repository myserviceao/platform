using Microsoft.EntityFrameworkCore;
using MyServiceAO.Models;

namespace MyServiceAO.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();
    public DbSet<PmCustomer> PmCustomers => Set<PmCustomer>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; } = null!;
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentTechnician> AppointmentTechnicians => Set<AppointmentTechnician>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<ApBill> ApBills => Set<ApBill>();
    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();
    public DbSet<HoldReason> HoldReasons => Set<HoldReason>();
    public DbSet<OutreachTemplate> OutreachTemplates => Set<OutreachTemplate>();
    public DbSet<OutreachItem> OutreachItems => Set<OutreachItem>();
    public DbSet<OutreachSettings> OutreachSettings => Set<OutreachSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tenant
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(300);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Tenant)
             .WithMany(t => t.Users)
             .HasForeignKey(u => u.TenantId);
        });

        // DashboardSnapshot - one per tenant
        modelBuilder.Entity<DashboardSnapshot>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.TenantId).IsUnique();
            e.HasOne(d => d.Tenant)
             .WithMany()
             .HasForeignKey(d => d.TenantId);
        });

        // Customer
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.TenantId, c.StCustomerId }).IsUnique();
            e.HasOne(c => c.Tenant)
             .WithMany()
             .HasForeignKey(c => c.TenantId);
        });

        // PmCustomer
        modelBuilder.Entity<PmCustomer>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.TenantId, p.StCustomerId }).IsUnique();
            e.HasOne(p => p.Tenant)
             .WithMany()
             .HasForeignKey(p => p.TenantId);
        });

        // Invoice (AR - from ServiceTitan)
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => new { i.TenantId, i.StInvoiceId }).IsUnique();
            e.HasOne(i => i.Tenant)
             .WithMany()
             .HasForeignKey(i => i.TenantId);
            e.Property(i => i.TotalAmount).HasColumnType("numeric(18,2)");
            e.Property(i => i.BalanceRemaining).HasColumnType("numeric(18,2)");
        });

        // Job
        modelBuilder.Entity<Job>(e =>
        {
            e.HasKey(j => j.Id);
            e.HasIndex(j => new { j.TenantId, j.StJobId }).IsUnique();
            e.HasOne(j => j.Tenant)
             .WithMany()
             .HasForeignKey(j => j.TenantId);
            e.Property(j => j.TotalAmount).HasColumnType("numeric(18,2)");
        });

        // Appointment
        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.TenantId, a.StAppointmentId }).IsUnique();
            e.HasOne(a => a.Tenant)
             .WithMany()
             .HasForeignKey(a => a.TenantId);
            e.HasMany(a => a.Technicians)
             .WithOne(t => t.Appointment)
             .HasForeignKey(t => t.AppointmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppointmentTechnician>(e =>
        {
            e.HasKey(t => t.Id);
        });

        // Vendor
        modelBuilder.Entity<Vendor>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasOne(v => v.Tenant)
             .WithMany()
             .HasForeignKey(v => v.TenantId);
            e.Property(v => v.Name).IsRequired().HasMaxLength(200);
        });

        // HoldReason
        modelBuilder.Entity<HoldReason>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => new { h.TenantId, h.StHoldReasonId }).IsUnique();
            e.HasOne(h => h.Tenant)
             .WithMany()
             .HasForeignKey(h => h.TenantId);
        });

        // CustomerLocation
        modelBuilder.Entity<CustomerLocation>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => new { l.TenantId, l.StLocationId }).IsUnique();
            e.HasOne(l => l.Tenant)
             .WithMany()
             .HasForeignKey(l => l.TenantId);
        });

        // PurchaseOrder
        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.TenantId, p.StPurchaseOrderId }).IsUnique();
            e.HasOne(p => p.Tenant)
             .WithMany()
             .HasForeignKey(p => p.TenantId);
            e.HasMany(p => p.Items)
             .WithOne(i => i.PurchaseOrder)
             .HasForeignKey(i => i.PurchaseOrderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Total).HasColumnType("numeric(18,2)");
            e.Property(p => p.Tax).HasColumnType("numeric(18,2)");
            e.Property(p => p.Shipping).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<PurchaseOrderItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Quantity).HasColumnType("numeric(18,2)");
            e.Property(i => i.QuantityReceived).HasColumnType("numeric(18,2)");
            e.Property(i => i.Cost).HasColumnType("numeric(18,2)");
            e.Property(i => i.Total).HasColumnType("numeric(18,2)");
        });

        // OutreachTemplate
        modelBuilder.Entity<OutreachTemplate>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId);
        });

        // OutreachItem
        modelBuilder.Entity<OutreachItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasOne(i => i.Tenant).WithMany().HasForeignKey(i => i.TenantId);
        });

        // OutreachSettings
        modelBuilder.Entity<OutreachSettings>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.TenantId).IsUnique();
            e.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId);
        });

        // ApBill (AP invoices)
        modelBuilder.Entity<ApBill>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasOne(b => b.Tenant)
             .WithMany()
             .HasForeignKey(b => b.TenantId);
            e.HasOne(b => b.Vendor)
             .WithMany()
             .HasForeignKey(b => b.VendorId)
             .IsRequired(false);
            e.Property(b => b.Amount).HasColumnType("numeric(18,2)");
        });
    }
}
