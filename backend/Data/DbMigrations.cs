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

        // Add Title column to Users
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Title"" TEXT;
        ");


        // Add Phone/Email to Customers for PM outreach
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""Phone"" TEXT;
        ");
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""Email"" TEXT;
        ");


        // CustomerLocations table
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""CustomerLocations"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StLocationId"" BIGINT NOT NULL,
                ""StCustomerId"" BIGINT NOT NULL DEFAULT 0,
                ""CustomerName"" TEXT NOT NULL DEFAULT '',
                ""LocationName"" TEXT NOT NULL DEFAULT '',
                ""Street"" TEXT NOT NULL DEFAULT '',
                ""City"" TEXT NOT NULL DEFAULT '',
                ""State"" TEXT NOT NULL DEFAULT '',
                ""Zip"" TEXT NOT NULL DEFAULT '',
                ""Latitude"" DOUBLE PRECISION,
                ""Longitude"" DOUBLE PRECISION,
                ""IsGeocoded"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StLocationId"")
            );
        ");


        // HoldReasons table
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""HoldReasons"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
                ""StHoldReasonId"" BIGINT NOT NULL,
                ""Name"" TEXT NOT NULL DEFAULT '',
                ""Active"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                UNIQUE(""TenantId"", ""StHoldReasonId"")
            );
        ");

        // Add HoldReasonName to Jobs
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Jobs"" ADD COLUMN IF NOT EXISTS ""HoldReasonName"" TEXT;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Jobs"" ADD COLUMN IF NOT EXISTS ""TagTypeIds"" text NULL;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""HoldReasons"" ADD COLUMN IF NOT EXISTS ""StTagTypeId"" bigint NULL;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Theme"" text NULL;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""LocationName"" text NULL;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoData"" text NULL;
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoContentType"" text NULL;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""PurchaseOrders"" (
                ""Id"" serial PRIMARY KEY,
                ""TenantId"" integer NOT NULL REFERENCES ""Tenants""(""Id""),
                ""StPurchaseOrderId"" bigint NOT NULL DEFAULT 0,
                ""Number"" text NOT NULL DEFAULT '',
                ""Status"" text NOT NULL DEFAULT '',
                ""VendorName"" text NOT NULL DEFAULT '',
                ""StVendorId"" bigint NOT NULL DEFAULT 0,
                ""StJobId"" bigint,
                ""JobNumber"" text,
                ""Total"" numeric NOT NULL DEFAULT 0,
                ""Tax"" numeric NOT NULL DEFAULT 0,
                ""Shipping"" numeric NOT NULL DEFAULT 0,
                ""Summary"" text,
                ""Date"" timestamp NOT NULL DEFAULT NOW(),
                ""RequiredOn"" timestamp,
                ""SentOn"" timestamp,
                ""ReceivedOn"" timestamp,
                ""UpdatedAt"" timestamp NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS ""PurchaseOrderItems"" (
                ""Id"" serial PRIMARY KEY,
                ""PurchaseOrderId"" integer NOT NULL REFERENCES ""PurchaseOrders""(""Id"") ON DELETE CASCADE,
                ""StItemId"" bigint NOT NULL DEFAULT 0,
                ""SkuName"" text NOT NULL DEFAULT '',
                ""SkuCode"" text NOT NULL DEFAULT '',
                ""Description"" text,
                ""Quantity"" numeric NOT NULL DEFAULT 0,
                ""QuantityReceived"" numeric NOT NULL DEFAULT 0,
                ""Cost"" numeric NOT NULL DEFAULT 0,
                ""Total"" numeric NOT NULL DEFAULT 0,
                ""Status"" text NOT NULL DEFAULT ''
            );
        ");

        // Also enhance existing ApBills with ST fields
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""StApBillId"" bigint NOT NULL DEFAULT 0;
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""StPurchaseOrderId"" bigint;
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""Status"" text;
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""Source"" text;
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""ReferenceNumber"" text;
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""Summary"" text;
            ALTER TABLE ""ApBills"" ADD COLUMN IF NOT EXISTS ""BillDate"" timestamp;
            ALTER TABLE ""Vendors"" ADD COLUMN IF NOT EXISTS ""StVendorId"" bigint NOT NULL DEFAULT 0;
        ");

        // Make VendorId nullable for AP bills from ST that may not have a matched vendor
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""ApBills"" ALTER COLUMN ""VendorId"" DROP NOT NULL;
        ");

    }
}
