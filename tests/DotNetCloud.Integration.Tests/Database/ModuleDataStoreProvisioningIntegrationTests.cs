using System.Data.Common;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DotNetCloud.Integration.Tests.Database;

/// <summary>
/// Live provisioning, adoption and teardown tests against a real database container (F10 / §9).
/// </summary>
/// <remarks>
/// <para>
/// The plan's required negative test lives here: the module principal must be denied read **and**
/// write access to the <c>core</c> schema and to any other module's schema. Adoption is verified
/// to move ownership — including <c>__EFMigrationsHistory</c> — so the module's next self-migration
/// works without re-applying anything.
/// </para>
/// <para>
/// Tests are skipped when Docker is unavailable, and the SQL Server variant also prepares
/// contained-database authentication in the container before provisioning.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[TestCategory("Docker")]
public class ModuleDataStoreProvisioningIntegrationTests
{
    private const string ProvisionModule = "org.example.itest.provision";
    private const string OtherModule = "org.example.itest.other";
    private const string AdoptModule = "org.example.itest.adopt";
    private const string DestroyModule = "org.example.itest.destroy";
    private const string RetryModule = "org.example.itest.retry";
    private const string LegacyModule = "org.example.itest.legacy";

    /// <summary>The schema a bundled module used before it was sandboxed (Track C conversion).</summary>
    private const string LegacySchema = "itest_legacy";

    private static DatabaseContainerFixture? s_fixture;
    private static bool s_available;
    private static DatabaseProvider s_provider;
    private static string? s_connectionString;
    private static ServiceProvider? s_services;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
#if SQLSERVER_INTEGRATION
        s_provider = DatabaseProvider.SqlServer;
        s_fixture = new DatabaseContainerFixture(DatabaseContainerConfig.SqlServer());
#else
        s_provider = DatabaseProvider.PostgreSQL;
        s_fixture = new DatabaseContainerFixture(DatabaseContainerConfig.PostgreSql());
#endif
        s_available = await s_fixture.StartAsync();
        if (!s_available)
        {
            return;
        }

        s_connectionString = s_fixture.ConnectionString;
        if (s_connectionString is null)
        {
            s_available = false;
            return;
        }

        if (s_provider == DatabaseProvider.SqlServer)
        {
            await EnsureSqlServerDatabaseAsync(s_connectionString);
            await PrepareSqlServerContainmentAsync(s_connectionString);
        }

        s_services = BuildServices(s_connectionString);
        using var scope = s_services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_services is not null)
        {
            var provisioning = s_services.GetRequiredService<IModuleDataStoreProvisioningService>();
            foreach (var moduleId in new[] { ProvisionModule, OtherModule, AdoptModule, DestroyModule, RetryModule, LegacyModule })
            {
                try
                {
                    await provisioning.DestroyAsync(moduleId);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }

            if (s_connectionString is not null)
            {
                try
                {
                    await ExecuteAsync(s_connectionString, s_provider == DatabaseProvider.SqlServer
                        ? $"IF SCHEMA_ID('{LegacySchema}') IS NOT NULL EXEC('DROP SCHEMA {Quote(LegacySchema)}')"
                        : $"DROP SCHEMA IF EXISTS {Quote(LegacySchema)} CASCADE");
                }
                catch
                {
                    // Best effort cleanup.
                }
            }

            await s_services.DisposeAsync();
        }

        if (s_fixture is not null)
        {
            await s_fixture.DisposeAsync();
        }
    }

    private static ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoreDbContext>(options =>
        {
            if (s_provider == DatabaseProvider.SqlServer)
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseNpgsql(connectionString);
            }

            // Mirror production: AddDotNetCloudDbContext sets QueryTrackingBehavior.NoTracking
            // globally. A fixture that tracks by default would hide the whole class of
            // "mutate a detached row then SaveChanges" bugs the services are written against.
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddSingleton<ITableNamingStrategy>(s_provider == DatabaseProvider.SqlServer
            ? new SqlServerNamingStrategy()
            : new PostgreSqlNamingStrategy());
        services.AddOptions<ModuleQuotasOptions>();
        services.AddOptions<ProcessSupervisorOptions>();
        services.AddSingleton<ModuleQuotaResolver>();
        services.AddSingleton(new CoreDatabaseOptions(connectionString, s_provider));
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IModuleDataStoreProvisioningService, ModuleDataStoreProvisioningService>();

        return services.BuildServiceProvider();
    }

    private static IModuleDataStoreProvisioningService Provisioning =>
        s_services!.GetRequiredService<IModuleDataStoreProvisioningService>();

    private static void SkipIfUnavailable()
    {
        if (!s_available)
        {
            Assert.Inconclusive("Docker is not available; live module data-store tests were skipped.");
        }
    }

    /// <summary>
    /// Inserts the <c>InstalledModule</c> row the data-store row references (the supervisor does
    /// this before provisioning in production; the store table has a real FK in live databases).
    /// </summary>
    private static async Task EnsureInstalledModuleAsync(string moduleId)
    {
        using var scope = s_services!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        if (await dbContext.InstalledModules.AnyAsync(m => m.ModuleId == moduleId))
        {
            return;
        }

        dbContext.InstalledModules.Add(new InstalledModule
        {
            ModuleId = moduleId,
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow,
            IsRequired = false,
            TrustTier = ModuleTrustTier.Bundled,
            DataStoreMode = ModuleDataStoreMode.Isolated,
            SandboxStatus = ModuleSandboxStatus.PendingProvisioning,
        });

        await dbContext.SaveChangesAsync();
    }

    private static (string Schema, string Principal) NamesFor(string moduleId)
    {
        Assert.IsTrue(ModuleDataStoreNaming.TryCreate(moduleId, out var schema, out var principal, out var error), error);
        return (schema, principal);
    }

    private static string Quote(string identifier) => s_provider == DatabaseProvider.SqlServer
            ? $"[{identifier}]"
            : $"\"{identifier}\"";

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using DbConnection connection = CreateConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using DbConnection connection = CreateConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static DbConnection CreateConnection(string connectionString) =>
        s_provider == DatabaseProvider.SqlServer
            ? new SqlConnection(connectionString)
            : new NpgsqlConnection(connectionString);

    private static async Task EnsureSqlServerDatabaseAsync(string connectionString)
    {
        // The mssql image creates only `master`; the fixture's connection string targets
        // dotnetcloud_test, so create it (EF would also create it, but containment needs it first).
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await ExecuteAsync(master.ConnectionString,
            "IF DB_ID('dotnetcloud_test') IS NULL CREATE DATABASE [dotnetcloud_test];");
    }

    private static async Task PrepareSqlServerContainmentAsync(string connectionString)
    {
        // Required before module contained users can exist; the container runs as `sa`.
        await ExecuteAsync(connectionString, "EXEC sp_configure 'show advanced options', 1; RECONFIGURE;");
        await ExecuteAsync(connectionString, "EXEC sp_configure 'contained database authentication', 1; RECONFIGURE;");
        await ExecuteAsync(connectionString, "ALTER DATABASE [dotnetcloud_test] SET CONTAINMENT = PARTIAL;");
    }

    private static async Task AssertPermissionDeniedAsync(Func<Task> action)
    {
#if SQLSERVER_INTEGRATION
        var ex = await Assert.ThrowsExactlyAsync<SqlException>(action);
        Assert.IsTrue(ex.Number is 229 or 230 or 262 or 2760,
            $"Expected a permission error, got {ex.Number}: {ex.Message}");
#else
        var ex = await Assert.ThrowsExactlyAsync<PostgresException>(action);
        Assert.AreEqual("42501", ex.SqlState, $"Expected insufficient_privilege, got {ex.SqlState}: {ex.Message}");
#endif
    }

    [TestMethod]
    public async Task Provision_ConfinesModulePrincipalToItsOwnSchema()
    {
        SkipIfUnavailable();

        await EnsureInstalledModuleAsync(ProvisionModule);
        await EnsureInstalledModuleAsync(OtherModule);

        var provisioned = await Provisioning.EnsureProvisionedAsync(ProvisionModule);
        Assert.IsTrue(provisioned.Success, provisioned.Error);
        Assert.IsTrue((await Provisioning.EnsureProvisionedAsync(OtherModule)).Success);

        var (schema, _) = NamesFor(ProvisionModule);
        var (otherSchema, _) = NamesFor(OtherModule);

        // A table in the sibling module's schema, created by the core role.
        await ExecuteAsync(s_connectionString!,
            $"CREATE TABLE {Quote(otherSchema)}.{Quote("secret")} (id int)");

        // A core-schema table with a deterministic name (EF table names differ per naming strategy).
        if (s_provider == DatabaseProvider.SqlServer)
        {
            await ExecuteAsync(s_connectionString!,
                "IF SCHEMA_ID('core') IS NULL EXEC('CREATE SCHEMA [core]');");
        }
        else
        {
            await ExecuteAsync(s_connectionString!, "CREATE SCHEMA IF NOT EXISTS core");
        }

        await ExecuteAsync(s_connectionString!, $"CREATE TABLE {Quote("core")}.{Quote("core_secret")} (id int)");

        var moduleConnectionString = await Provisioning.GetConnectionStringAsync(ProvisionModule);
        Assert.IsNotNull(moduleConnectionString);

        if (s_provider == DatabaseProvider.SqlServer)
        {
            var (_, provisionPrincipal) = NamesFor(ProvisionModule);
            var createTablePermission = await ScalarAsync(s_connectionString!,
                $"SELECT COUNT(*) FROM fn_my_permissions('{provisionPrincipal}', 'DATABASE') WHERE permission_name = 'CREATE TABLE'");
            Assert.AreEqual(1, Convert.ToInt32(createTablePermission),
                "The module principal must be granted CREATE TABLE");
        }

        // The module principal can work in its own schema...
        await ExecuteAsync(moduleConnectionString,
            $"CREATE TABLE {Quote(schema)}.{Quote("widgets")} (id int primary key)");

        // ...but cannot read or write a sibling module's schema, or the core schema.
        await AssertPermissionDeniedAsync(() => ScalarAsync(moduleConnectionString,
            $"SELECT count(*) FROM {Quote(otherSchema)}.{Quote("secret")}"));
        await AssertPermissionDeniedAsync(() => ExecuteAsync(moduleConnectionString,
            $"CREATE TABLE {Quote(otherSchema)}.{Quote("intruder")} (id int)"));
        await AssertPermissionDeniedAsync(() => ScalarAsync(moduleConnectionString,
            $"SELECT count(*) FROM {Quote("core")}.{Quote("core_secret")}"));
    }

    /// <summary>
    /// Regression: a first provisioning attempt that fails the prerequisite check writes an
    /// <c>Error</c> row. The retry must persist <c>Active</c> plus the encrypted connection string.
    /// <c>CoreDbContext</c> defaults to no-tracking queries, so the detached row was mutated and then
    /// silently discarded by <c>SaveChanges</c>, leaving the module permanently unstartable with
    /// "the module data store is provisioned but its connection string could not be read".
    /// </summary>
    [TestMethod]
    public async Task Provision_AfterFailedAttempt_PersistsActiveStatusAndConnectionString()
    {
        SkipIfUnavailable();

        using (var clearScope = s_services!.CreateScope())
        {
            var clearContext = clearScope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var leftover = await clearContext.ModuleDataStores
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ModuleId == RetryModule);

            if (leftover is not null)
            {
                clearContext.ModuleDataStores.Remove(leftover);
                await clearContext.SaveChangesAsync();
            }
        }

        using (var seedScope = s_services!.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var (schema, principal) = NamesFor(RetryModule);

            seedContext.ModuleDataStores.Add(new ModuleDataStore
            {
                ModuleId = RetryModule,
                Provider = s_provider.ToString(),
                SchemaName = schema,
                PrincipalName = principal,
                EncryptedConnectionString = null,
                Status = ModuleDataStoreStatus.Error,
                LastError = "The DotNetCloud database role lacks CREATEROLE.",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });

            await seedContext.SaveChangesAsync();
        }

        var provisioned = await Provisioning.EnsureProvisionedAsync(RetryModule);
        Assert.IsTrue(provisioned.Success, provisioned.Error);

        using (var assertScope = s_services!.CreateScope())
        {
            var assertContext = assertScope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var row = await assertContext.ModuleDataStores
                .AsNoTracking()
                .SingleAsync(d => d.ModuleId == RetryModule);

            Assert.AreEqual(ModuleDataStoreStatus.Active, row.Status, "the retry must clear the failed status");
            Assert.IsNull(row.LastError);
            Assert.IsFalse(string.IsNullOrEmpty(row.EncryptedConnectionString));
        }

        Assert.IsNotNull(await Provisioning.GetConnectionStringAsync(RetryModule));
    }

    [TestMethod]
    public async Task Adopt_TransfersSchemaAndMigrationHistoryOwnership()
    {
        SkipIfUnavailable();

        await EnsureInstalledModuleAsync(AdoptModule);
        var (schema, principal) = NamesFor(AdoptModule);

        // Simulate a core-migrated module: schema plus EF history and a data table, all core-owned.
        if (s_provider == DatabaseProvider.SqlServer)
        {
            await ExecuteAsync(s_connectionString!, $"CREATE SCHEMA {Quote(schema)}");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(schema)}.{Quote("__EFMigrationsHistory")} (MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL)");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(schema)}.{Quote("items")} (id int NOT NULL PRIMARY KEY)");
        }
        else
        {
            await ExecuteAsync(s_connectionString!, $"CREATE SCHEMA {Quote(schema)}");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(schema)}.\"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL)");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(schema)}.{Quote("items")} (id int PRIMARY KEY)");
        }

        var adopted = await Provisioning.AdoptExistingStoreAsync(AdoptModule);
        Assert.IsTrue(adopted.Success, adopted.Error);

        // Ownership moved to the module principal, including the history table.
        if (s_provider == DatabaseProvider.SqlServer)
        {
            var schemaOwner = await ScalarAsync(s_connectionString!,
                $"SELECT USER_NAME(principal_id) FROM sys.schemas WHERE name = '{schema}'");
            Assert.AreEqual(principal, schemaOwner as string);

            var tableOwner = await ScalarAsync(s_connectionString!,
                $"SELECT USER_NAME(t.principal_id) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = '{schema}' AND t.name = '__EFMigrationsHistory'");
            Assert.AreEqual(principal, tableOwner as string);
        }
        else
        {
            var schemaOwner = await ScalarAsync(s_connectionString!,
                $"SELECT r.rolname FROM pg_namespace n JOIN pg_roles r ON r.oid = n.nspowner WHERE n.nspname = '{schema}'");
            Assert.AreEqual(principal, schemaOwner as string);

            var historyOwner = await ScalarAsync(s_connectionString!,
                $"SELECT tableowner FROM pg_tables WHERE schemaname = '{schema}' AND tablename = '__EFMigrationsHistory'");
            Assert.AreEqual(principal, historyOwner as string);
        }

        // The module principal can write its migration history, so its next self-migration works.
        var moduleConnectionString = await Provisioning.GetConnectionStringAsync(AdoptModule);
        Assert.IsNotNull(moduleConnectionString);
        await ExecuteAsync(moduleConnectionString,
            s_provider == DatabaseProvider.SqlServer
                ? $"INSERT INTO {Quote(schema)}.{Quote("__EFMigrationsHistory")} (MigrationId, ProductVersion) VALUES ('20260101000000_Init', '10.0.0')"
                : $"INSERT INTO {Quote(schema)}.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20260101000000_Init', '10.0.0')");
    }

    /// <summary>
    /// Track C: a bundled module keeps its data. Adoption provisions the module's own schema and
    /// moves the objects the core created earlier into it — rows included — then transfers ownership
    /// so the module's next self-migration sees its history and can write to it.
    /// </summary>
    [TestMethod]
    public async Task Adopt_WithLegacySchema_MovesObjectsRowsAndOwnership()
    {
        SkipIfUnavailable();

        await EnsureInstalledModuleAsync(LegacyModule);
        var (schema, principal) = NamesFor(LegacyModule);

        // Pre-sandbox state: a core-created schema named after the module, with its migration
        // history and a data table holding a row, all owned by the core role.
        if (s_provider == DatabaseProvider.SqlServer)
        {
            await ExecuteAsync(s_connectionString!,
                $"IF SCHEMA_ID('{LegacySchema}') IS NULL EXEC('CREATE SCHEMA {Quote(LegacySchema)}')");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(LegacySchema)}.{Quote("__EFMigrationsHistory")} (MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL)");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(LegacySchema)}.{Quote("items")} (id int NOT NULL PRIMARY KEY)");
            await ExecuteAsync(s_connectionString!, $"INSERT INTO {Quote(LegacySchema)}.{Quote("items")} (id) VALUES (42)");
        }
        else
        {
            await ExecuteAsync(s_connectionString!, $"CREATE SCHEMA IF NOT EXISTS {Quote(LegacySchema)}");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(LegacySchema)}.\"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL)");
            await ExecuteAsync(s_connectionString!,
                $"CREATE TABLE {Quote(LegacySchema)}.{Quote("items")} (id int PRIMARY KEY)");
            await ExecuteAsync(s_connectionString!, $"INSERT INTO {Quote(LegacySchema)}.{Quote("items")} (id) VALUES (42)");
        }

        var adopted = await Provisioning.AdoptExistingStoreAsync(LegacyModule, LegacySchema);
        Assert.IsTrue(adopted.Success, adopted.Error);

        // No data movement loss: the row travelled with its table.
        var rows = await ScalarAsync(s_connectionString!, $"SELECT COUNT(*) FROM {Quote(schema)}.{Quote("items")}");
        Assert.AreEqual(1L, Convert.ToInt64(rows, System.Globalization.CultureInfo.InvariantCulture));

        // Nothing is left behind in the historical schema.
        var leftBehind = await ScalarAsync(s_connectionString!, s_provider == DatabaseProvider.SqlServer
            ? $"SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = '{LegacySchema}'"
            : $"SELECT COUNT(*) FROM pg_tables WHERE schemaname = '{LegacySchema}'");
        Assert.AreEqual(0L, Convert.ToInt64(leftBehind, System.Globalization.CultureInfo.InvariantCulture));

        // Ownership followed the objects, including the migration history table.
        var historyOwner = await ScalarAsync(s_connectionString!, s_provider == DatabaseProvider.SqlServer
            ? $"SELECT USER_NAME(t.principal_id) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = '{schema}' AND t.name = '__EFMigrationsHistory'"
            : $"SELECT tableowner FROM pg_tables WHERE schemaname = '{schema}' AND tablename = '__EFMigrationsHistory'");
        Assert.AreEqual(principal, historyOwner as string);

        // The module principal reaches the moved data through its own scoped connection string.
        var moduleConnectionString = await Provisioning.GetConnectionStringAsync(LegacyModule);
        Assert.IsNotNull(moduleConnectionString);
        var visible = await ScalarAsync(moduleConnectionString, $"SELECT id FROM {Quote("items")}");
        Assert.AreEqual(42, Convert.ToInt32(visible, System.Globalization.CultureInfo.InvariantCulture));

        // Idempotent: a second adoption on an already-converted store is a no-op that still succeeds.
        var again = await Provisioning.AdoptExistingStoreAsync(LegacyModule, LegacySchema);
        Assert.IsTrue(again.Success, again.Error);
    }

    [TestMethod]
    public async Task Destroy_RemovesSchemaAndPrincipal()
    {
        SkipIfUnavailable();

        await EnsureInstalledModuleAsync(DestroyModule);
        var (schema, principal) = NamesFor(DestroyModule);
        Assert.IsTrue((await Provisioning.EnsureProvisionedAsync(DestroyModule)).Success);

        await Provisioning.DestroyAsync(DestroyModule);

        if (s_provider == DatabaseProvider.SqlServer)
        {
            Assert.IsTrue(await ScalarAsync(s_connectionString!, $"SELECT SCHEMA_ID('{schema}')") is null or DBNull);
            Assert.IsTrue(await ScalarAsync(s_connectionString!, $"SELECT USER_ID('{principal}')") is null or DBNull);
        }
        else
        {
            Assert.IsNull(await ScalarAsync(s_connectionString!,
                $"SELECT nspname FROM pg_namespace WHERE nspname = '{schema}'"));
            Assert.IsNull(await ScalarAsync(s_connectionString!,
                $"SELECT rolname FROM pg_roles WHERE rolname = '{principal}'"));
        }
    }

    [TestMethod]
    public async Task Prerequisites_AreSatisfiedOnTheTestContainer()
    {
        SkipIfUnavailable();

        var prerequisites = await Provisioning.CheckPrerequisitesAsync();

        Assert.IsTrue(prerequisites.IsSatisfied, string.Join(" ", prerequisites.Errors));
    }
}
