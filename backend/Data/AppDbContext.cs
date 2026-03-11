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
    }
}
