using System.Data.Common;

using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DotNetCloud.Integration.Tests.Database;

/// <summary>
/// Integration tests for the Tracks PostgreSQL schema compatibility migrations.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class TracksSchemaMigrationIntegrationTests
{
    private const string DefaultAdminConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=postgres";

    [TestMethod]
    public async Task TracksDbInitializer_LegacyListSchema_MigratesToSwimlaneSchema()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_TEST_POSTGRES_CONNECTION")
            ?? DefaultAdminConnectionString;

        var databaseName = $"tracks_swimlane_migration_{Guid.NewGuid():N}";
        var testConnectionString = BuildDatabaseConnectionString(adminConnectionString, databaseName);

        try
        {
            await CreateDatabaseAsync(adminConnectionString, databaseName);

            await using (var setupContext = CreateTracksContext(testConnectionString))
            {
                await setupContext.Database.EnsureCreatedAsync();
                await RewindTracksSchemaToLegacyListNamesAsync(setupContext);
            }

            await using (var migrationContext = CreateTracksContext(testConnectionString))
            {
                await TracksDbInitializer.InitializeAsync(migrationContext, NullLogger.Instance);

                await AssertSchemaStateAsync(migrationContext);

                var boardId = Guid.NewGuid();
                var swimlaneId = Guid.NewGuid();
                var ownerId = Guid.NewGuid();

                migrationContext.Boards.Add(new Board
                {
                    Id = boardId,
                    Title = "Migration Validation Board",
                    OwnerId = ownerId,
                });

                migrationContext.BoardSwimlanes.Add(new BoardSwimlane
                {
                    Id = swimlaneId,
                    BoardId = boardId,
                    Title = "Ready",
                    Position = 1000,
                });

                migrationContext.Cards.Add(new Card
                {
                    SwimlaneId = swimlaneId,
                    CardNumber = 1,
                    Title = "Schema migrated",
                    Position = 1000,
                    CreatedByUserId = ownerId,
                });

                await migrationContext.SaveChangesAsync();
            }

            await using (var verificationContext = CreateTracksContext(testConnectionString))
            {
                var card = await verificationContext.Cards
                    .Include(c => c.Swimlane)
                    .SingleAsync(c => c.Title == "Schema migrated");

                Assert.IsNotNull(card.Swimlane);
                Assert.AreEqual("Ready", card.Swimlane.Title);
                Assert.AreNotEqual(Guid.Empty, card.SwimlaneId);
            }
        }
        catch (NpgsqlException ex)
        {
            Assert.Inconclusive($"PostgreSQL integration test unavailable: {ex.Message}");
        }
        finally
        {
            await DropDatabaseAsync(adminConnectionString, databaseName);
        }
    }

    private static TracksDbContext CreateTracksContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TracksDbContext(options);
    }

    private static async Task CreateDatabaseAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
    {
        try
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();

                DROP DATABASE IF EXISTS \"{databaseName}\";
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException)
        {
            // Best-effort cleanup for disposable test databases.
        }
    }

    private static async Task RewindTracksSchemaToLegacyListNamesAsync(TracksDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "BoardSwimlanes" RENAME TO "BoardLists";
            ALTER TABLE "Cards" RENAME COLUMN "SwimlaneId" TO "ListId";
            ALTER INDEX "ix_board_swimlanes_board_position" RENAME TO "ix_board_lists_board_position";
            ALTER INDEX "ix_board_swimlanes_is_archived" RENAME TO "ix_board_lists_is_archived";
            ALTER INDEX "ix_cards_swimlane_position" RENAME TO "ix_cards_list_position";
            """);
    }

    private static async Task AssertSchemaStateAsync(TracksDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            Assert.IsTrue(await RegClassExistsAsync(connection, "public.\"BoardSwimlanes\""));
            Assert.IsFalse(await RegClassExistsAsync(connection, "public.\"BoardLists\""));
            Assert.IsTrue(await RegClassExistsAsync(connection, "public.\"ix_board_swimlanes_board_position\""));
            Assert.IsTrue(await RegClassExistsAsync(connection, "public.\"ix_board_swimlanes_is_archived\""));
            Assert.IsTrue(await RegClassExistsAsync(connection, "public.\"ix_cards_swimlane_position\""));
            Assert.IsFalse(await RegClassExistsAsync(connection, "public.\"ix_board_lists_board_position\""));
            Assert.IsFalse(await RegClassExistsAsync(connection, "public.\"ix_board_lists_is_archived\""));
            Assert.IsFalse(await RegClassExistsAsync(connection, "public.\"ix_cards_list_position\""));
            Assert.IsTrue(await ColumnExistsAsync(connection, "Cards", "SwimlaneId"));
            Assert.IsFalse(await ColumnExistsAsync(connection, "Cards", "ListId"));
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> RegClassExistsAsync(DbConnection connection, string relationName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@relationName) IS NOT NULL;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@relationName";
        parameter.Value = relationName;
        command.Parameters.Add(parameter);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tableName
                  AND column_name = @columnName
            );
            """;

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@columnName";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static string BuildDatabaseConnectionString(string adminConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
        };

        return builder.ConnectionString;
    }
}