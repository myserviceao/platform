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

        // Example future migration pattern:
        // cmd.CommandText = @"
        //     ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Theme"" VARCHAR(50) DEFAULT 'dark';
        //     ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoUrl"" TEXT;
        // ";
        // await cmd.ExecuteNonQueryAsync();

        // Placeholder — no extra columns yet
        cmd.CommandText = "SELECT 1";
        await cmd.ExecuteNonQueryAsync();

        await conn.CloseAsync();
    }
}
