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
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentTechnician> AppointmentTechnicians => Set<AppointmentTechnician>();

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

        // Customer - one row per (tenant, ST customer)
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.TenantId, c.StCustomerId }).IsUnique();
            e.HasOne(c => c.Tenant)
             .WithMany()
             .HasForeignKey(c => c.TenantId);
        });

        // PmCustomer - one row per (tenant, ST customer)
        modelBuilder.Entity<PmCustomer>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.TenantId, p.StCustomerId }).IsUnique();
            e.HasOne(p => p.Tenant)
             .WithMany()
             .HasForeignKey(p => p.TenantId);
        });

        // Invoice - one row per (tenant, ST invoice)
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

        // Job - one row per (tenant, ST job)
        modelBuilder.Entity<Job>(e =>
        {
            e.HasKey(j => j.Id);
            e.HasIndex(j => new { j.TenantId, j.StJobId }).IsUnique();
            e.HasOne(j => j.Tenant)
             .WithMany()
             .HasForeignKey(j => j.TenantId);
            e.Property(j => j.TotalAmount).HasColumnType("numeric(18,2)");
        });

        // Appointment - one row per (tenant, ST appointment)
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

        // AppointmentTechnician
        modelBuilder.Entity<AppointmentTechnician>(e =>
        {
            e.HasKey(t => t.Id);
        });
    }
}
