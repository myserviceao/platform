using Microsoft.EntityFrameworkCore;

namespace MyServiceAO.Data;

/// <summary>
/// Runs idempotent SQL migrations at startup.
/// Use ALTER TABLE ... ADD COLUMN IF NOT EXISTS — never EF migration files.
/// </summary>
public static class DbMigrations
{
    public static async Task RunAsync(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            -- Tenant: add ServiceTitan + sync columns if not present
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""StClientId"" TEXT;
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""StClientSecret"" TEXT;
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""StTenantId"" TEXT;
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""StAccessToken"" TEXT;
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""StTokenExpiresAt"" TIMESTAMP WITH TIME ZONE;
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""LastSyncedAt"" TIMESTAMP WITH TIME ZONE;

            -- DashboardSnapshots table — one row per tenant, upserted on sync
            CREATE TABLE IF NOT EXISTS ""DashboardSnapshots"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TenantId"" INTEGER NOT NULL UNIQUE REFERENCES ""Tenants""(""Id""),
                ""RevenueThisMonth"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""RevenueLastMonth"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""AccountsReceivable"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                ""UnpaidInvoiceCount"" INTEGER NOT NULL DEFAULT 0,
                ""OpenJobCount"" INTEGER NOT NULL DEFAULT 0,
                ""OverduePmCount"" INTEGER NOT NULL DEFAULT 0,
                ""SnapshotTakenAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
        ";

        await cmd.ExecuteNonQueryAsync();
        await conn.CloseAsync();
    }
}