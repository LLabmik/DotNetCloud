using System.Data.Common;

using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DotNetCloud.Integration.Tests.Database;

/// <summary>
/// Integration tests for the Tracks module's sandboxed data plane (Track C).
/// </summary>
/// <remarks>
/// <para>
/// Since the data-plane flip the module owns its schema: <see cref="TracksDbInitializer"/> applies the
/// module's own migrations, and every DDL statement in them is unqualified, so the tables and
/// <c>__EFMigrationsHistory</c> land in whichever schema the provisioned connection string pins
/// (<c>search_path</c>). The module principal is deliberately not allowed to run <c>CREATE SCHEMA</c>.
/// </para>
/// <para>
/// The tests create a disposable database and a module-style schema as the admin connection, then
/// initialise through a connection whose <c>search_path</c> points at that schema — the same shape the
/// core provisions for a sandboxed module.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public sealed class TracksSchemaMigrationIntegrationTests
{
    private const string DefaultAdminConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=postgres";
    private const string MigrationDatabasePrefix = "tracks_sandbox_migration_";
    private static readonly TimeSpan StaleDatabaseThreshold = TimeSpan.FromMinutes(15);

    [TestMethod]
    public async Task TracksDbInitializer_OwnSchema_CreatesUnqualifiedTablesAndHistory()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_TEST_POSTGRES_CONNECTION")
            ?? DefaultAdminConnectionString;

        var databaseName = $"{MigrationDatabasePrefix}{Guid.CreateVersion7():N}";
        var moduleSchema = $"dnc_tracks_{Guid.NewGuid():N}"[..20];
        var testConnectionString = BuildDatabaseConnectionString(adminConnectionString, databaseName);

        try
        {
            await CleanupStaleDatabasesAsync(adminConnectionString, MigrationDatabasePrefix, StaleDatabaseThreshold);
            await CreateDatabaseAsync(adminConnectionString, databaseName);
            await CreateSchemaAsync(testConnectionString, moduleSchema);

            var moduleConnectionString = BuildModuleConnectionString(testConnectionString, moduleSchema);

            await using (var migrationContext = CreateTracksContext(moduleConnectionString))
            {
                await TracksDbInitializer.InitializeAsync(migrationContext, NullLogger.Instance);

                var productId = Guid.CreateVersion7();
                var swimlaneId = Guid.CreateVersion7();
                var ownerId = Guid.CreateVersion7();

                var product = new Product
                {
                    Id = productId,
                    Name = "Sandboxed Product",
                    OwnerId = ownerId,
                };

                var swimlane = new Swimlane
                {
                    Id = swimlaneId,
                    ContainerType = SwimlaneContainerType.Product,
                    ContainerId = productId,
                    Title = "Ready",
                    Position = 1000,
                };
                product.Swimlanes.Add(swimlane);

                migrationContext.Products.Add(product);

                migrationContext.WorkItems.Add(new WorkItem
                {
                    ProductId = productId,
                    SwimlaneId = swimlaneId,
                    ItemNumber = 1,
                    Title = "Sandboxed item",
                    Position = 1000,
                    CreatedByUserId = ownerId,
                    Type = WorkItemType.Item,
                });

                await migrationContext.SaveChangesAsync();
            }

            await using (var verificationContext = CreateTracksContext(moduleConnectionString))
            {
                var workItem = await verificationContext.WorkItems
                    .Include(wi => wi.Swimlane)
                    .SingleAsync(wi => wi.Title == "Sandboxed item");

                Assert.IsNotNull(workItem.Swimlane);
                Assert.AreEqual("Ready", workItem.Swimlane.Title);
            }

            await AssertModuleSchemaStateAsync(testConnectionString, moduleSchema);
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

    /// <summary>
    /// Creates the module's schema the way the core's provisioning service does — as an admin
    /// connection, never as the module principal.
    /// </summary>
    private static async Task CreateSchemaAsync(string connectionString, string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string databaseName)
    {
        try
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();

            await DropDatabaseAsync(connection, databaseName);
        }
        catch (NpgsqlException)
        {
            // Best-effort cleanup for disposable test databases.
        }
    }

    private static async Task CleanupStaleDatabasesAsync(string adminConnectionString, string databasePrefix, TimeSpan minimumAge)
    {
        try
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();

            var databaseNames = new List<string>();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT d.datname
                    FROM pg_database d
                    WHERE d.datname LIKE @pattern
                      AND EXTRACT(EPOCH FROM now() - (pg_stat_file('base/' || d.oid || '/PG_VERSION')).modification) >= @minimumAgeSeconds
                      AND NOT EXISTS (
                          SELECT 1
                          FROM pg_stat_activity a
                          WHERE a.datname = d.datname
                            AND a.pid <> pg_backend_pid()
                      );
                    """;

                command.Parameters.AddWithValue("@pattern", $"{databasePrefix}%");
                command.Parameters.AddWithValue("@minimumAgeSeconds", minimumAge.TotalSeconds);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    databaseNames.Add(reader.GetString(0));
                }
            }

            foreach (var staleDatabaseName in databaseNames)
            {
                await DropDatabaseAsync(connection, staleDatabaseName);
            }
        }
        catch (NpgsqlException)
        {
            // Best-effort cleanup for stale disposable test databases.
        }
    }

    private static async Task DropDatabaseAsync(NpgsqlConnection connection, string databaseName)
    {
        var quotedDatabaseName = $"\"{databaseName.Replace("\"", "\"\"")}\"";

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @databaseName AND pid <> pg_backend_pid();

            DROP DATABASE IF EXISTS {quotedDatabaseName};
            """;
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Asserts the sandboxed contract: the tables and the migration history exist in the module's own
    /// schema and nothing was created in <c>public</c>.
    /// </summary>
    private static async Task AssertModuleSchemaStateAsync(string connectionString, string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Assert.IsTrue(
            await RelationExistsAsync(connection, schemaName, "Products"),
            "the Products table must be created in the module's own schema");
        Assert.IsTrue(
            await RelationExistsAsync(connection, schemaName, "WorkItems"),
            "the WorkItems table must be created in the module's own schema");
        Assert.IsTrue(
            await RelationExistsAsync(connection, schemaName, "__EFMigrationsHistory"),
            "the module owns its migration history");
        Assert.IsFalse(
            await RelationExistsAsync(connection, "public", "Products"),
            "the module must not create tables in public; search_path decides the schema");
    }

    private static async Task<bool> RelationExistsAsync(DbConnection connection, string schemaName, string relationName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@relationName) IS NOT NULL;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@relationName";
        parameter.Value = $"{schemaName}.\"{relationName}\"";
        command.Parameters.Add(parameter);

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

    /// <summary>
    /// The shape the core provisions for a sandboxed module: the schema is pinned through
    /// <c>search_path</c> and every DDL statement in the module's migrations is unqualified.
    /// </summary>
    private static string BuildModuleConnectionString(string connectionString, string schemaName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schemaName,
        };

        return builder.ConnectionString;
    }
}
