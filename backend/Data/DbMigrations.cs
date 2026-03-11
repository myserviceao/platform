using Microsoft.EntityFrameworkCore;

namespace MyServiceAO.Data;

/// <summary>
/// Runs idempotent SQL migrations at startup.
/// Use ALTER TABLE ... ADD COLUMN IF NOT EXISTS - never EF migrations.
/// </summary>
public static class DbMigrations
{
    public static async Task RunAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Customers"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StCustomerId"" BIGINT NOT NULL,
                ""Name"" TEXT NOT NULL DEFAULT '',
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StCustomerId"")
            );
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""PmCustomers"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StCustomerId"" BIGINT NOT NULL,
                ""CustomerName"" TEXT NOT NULL DEFAULT '',
                ""LastPmDate"" TIMESTAMP WITH TIME ZONE,
                ""PmStatus"" TEXT NOT NULL DEFAULT 'Current',
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StCustomerId"")
            );
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Invoices"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StInvoiceId"" BIGINT NOT NULL,
                ""StCustomerId"" BIGINT NOT NULL DEFAULT 0,
                ""CustomerName"" TEXT NOT NULL DEFAULT '',
                ""TotalAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""BalanceRemaining"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""InvoiceDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StInvoiceId"")
            );
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Jobs"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StJobId"" BIGINT NOT NULL,
                ""StCustomerId"" BIGINT NOT NULL DEFAULT 0,
                ""CustomerName"" TEXT NOT NULL DEFAULT '',
                ""JobNumber"" TEXT NOT NULL DEFAULT '',
                ""Status"" TEXT NOT NULL DEFAULT '',
                ""JobTypeName"" TEXT,
                ""TotalAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""CreatedOn"" TIMESTAMP WITH TIME ZONE,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StJobId"")
            );
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Appointments"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StAppointmentId"" BIGINT NOT NULL,
                ""StJobId"" BIGINT NOT NULL DEFAULT 0,
                ""JobNumber"" TEXT NOT NULL DEFAULT '',
                ""CustomerName"" TEXT NOT NULL DEFAULT '',
                ""Status"" TEXT NOT NULL DEFAULT '',
                ""Start"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StAppointmentId"")
            );
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""AppointmentTechnicians"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""AppointmentId"" INTEGER NOT NULL REFERENCES ""Appointments""(""Id"") ON DELETE CASCADE,
                ""TechnicianName"" TEXT NOT NULL DEFAULT ''
            );
        ");

        // Vendors table
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Vendors"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""Name"" TEXT NOT NULL DEFAULT '',
                ""ContactName"" TEXT,
                ""Phone"" TEXT,
                ""Email"" TEXT,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
        ");

        // AP Bills table
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""ApBills"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""VendorId"" INTEGER NOT NULL REFERENCES ""Vendors""(""Id"") ON DELETE CASCADE,
                ""InvoiceNumber"" TEXT NOT NULL DEFAULT '',
                ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""DueDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""IsPaid"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""PaidDate"" TIMESTAMP WITH TIME ZONE,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
        ");
    }
}
