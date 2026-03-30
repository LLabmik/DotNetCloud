using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Initializes the Tracks database, ensuring the schema is created.
/// </summary>
public static class TracksDbInitializer
{
    /// <summary>
    /// Ensures the Tracks database schema exists.
    /// </summary>
    public static async Task InitializeAsync(
        TracksDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        // Schema migration: add CardNumber column if missing (EnsureCreated won't alter existing tables)
        await MigrateCardNumberAsync(db, logger, cancellationToken);

        logger?.LogInformation("Tracks database initialized");
    }

    private static async Task MigrateCardNumberAsync(
        TracksDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        // Check if column exists
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(cancellationToken);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_name = 'Cards' AND column_name = 'CardNumber';
                """;
            var exists = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
            if (exists)
                return;
        }
        finally
        {
            await conn.CloseAsync();
        }

        logger?.LogInformation("Adding CardNumber column to Cards table and backfilling...");

        // Add column, backfill ordered by CreatedAt, add unique index
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Cards" ADD COLUMN "CardNumber" integer NOT NULL DEFAULT 0;

            WITH numbered AS (
                SELECT "Id", ROW_NUMBER() OVER (ORDER BY "CreatedAt") AS rn
                FROM "Cards"
            )
            UPDATE "Cards" SET "CardNumber" = numbered.rn
            FROM numbered WHERE "Cards"."Id" = numbered."Id";

            CREATE UNIQUE INDEX ix_cards_card_number ON "Cards" ("CardNumber");
            """, cancellationToken);

        logger?.LogInformation("CardNumber migration complete");
    }
}
