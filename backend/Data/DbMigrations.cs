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
        // Ensure tables exist (EF CreateTable handles this via EnsureCreated)
        await db.Database.EnsureCreatedAsync();

        // Add PmCustomers table if it doesn't exist yet
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
    }
}
