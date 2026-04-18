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

        await MigrateSwimlaneSchemaAsync(db, logger, cancellationToken);

        // Schema migration: add CardNumber column if missing (EnsureCreated won't alter existing tables)
        await MigrateCardNumberAsync(db, logger, cancellationToken);

        // Schema migration: add LockSwimlanes column if missing
        await MigrateLockSwimlanesAsync(db, logger, cancellationToken);

        // Schema migration: add IsDone column to BoardSwimlanes if missing
        await MigrateSwimlaneIsDoneAsync(db, logger, cancellationToken);

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

    private static async Task MigrateSwimlaneSchemaAsync(
        TracksDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var providerName = db.Database.ProviderName;
        if (!string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            logger?.LogDebug("Skipping swimlane schema migration for provider {ProviderName}", providerName);
            return;
        }

        logger?.LogInformation("Checking Tracks swimlane schema compatibility...");

        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF to_regclass('public."BoardLists"') IS NOT NULL AND to_regclass('public."BoardSwimlanes"') IS NULL THEN
                    ALTER TABLE "BoardLists" RENAME TO "BoardSwimlanes";
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'Cards' AND column_name = 'ListId'
                ) AND NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'Cards' AND column_name = 'SwimlaneId'
                ) THEN
                    ALTER TABLE "Cards" RENAME COLUMN "ListId" TO "SwimlaneId";
                END IF;

                IF to_regclass('public."ix_board_lists_board_position"') IS NOT NULL
                    AND to_regclass('public."ix_board_swimlanes_board_position"') IS NULL THEN
                    ALTER INDEX "ix_board_lists_board_position" RENAME TO "ix_board_swimlanes_board_position";
                END IF;

                IF to_regclass('public."ix_board_lists_is_archived"') IS NOT NULL
                    AND to_regclass('public."ix_board_swimlanes_is_archived"') IS NULL THEN
                    ALTER INDEX "ix_board_lists_is_archived" RENAME TO "ix_board_swimlanes_is_archived";
                END IF;

                IF to_regclass('public."ix_cards_list_position"') IS NOT NULL
                    AND to_regclass('public."ix_cards_swimlane_position"') IS NULL THEN
                    ALTER INDEX "ix_cards_list_position" RENAME TO "ix_cards_swimlane_position";
                END IF;
            END $$;
            """, cancellationToken);

        logger?.LogInformation("Tracks swimlane schema compatibility check complete");
    }

    private static async Task MigrateLockSwimlanesAsync(
        TracksDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(cancellationToken);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_name = 'Boards' AND column_name = 'LockSwimlanes';
                """;
            var exists = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
            if (exists)
                return;
        }
        finally
        {
            await conn.CloseAsync();
        }

        logger?.LogInformation("Adding LockSwimlanes column to Boards table...");

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Boards" ADD COLUMN "LockSwimlanes" boolean NOT NULL DEFAULT false;
            """, cancellationToken);

        logger?.LogInformation("LockSwimlanes migration complete");
    }

    private static async Task MigrateSwimlaneIsDoneAsync(
        TracksDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(cancellationToken);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_name = 'BoardSwimlanes' AND column_name = 'IsDone';
                """;
            var exists = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
            if (exists)
                return;
        }
        finally
        {
            await conn.CloseAsync();
        }

        logger?.LogInformation("Adding IsDone column to BoardSwimlanes table...");

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "BoardSwimlanes" ADD COLUMN "IsDone" boolean NOT NULL DEFAULT false;
            UPDATE "BoardSwimlanes" SET "IsDone" = true WHERE "Title" IN ('Done', 'Closed');
            """, cancellationToken);

        logger?.LogInformation("IsDone migration complete");
    }
}
