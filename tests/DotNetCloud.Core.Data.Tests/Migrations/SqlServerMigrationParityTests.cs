using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DotNetCloud.Core.Data.Tests.Migrations;

/// <summary>
/// Drift and parity guards for the SQL Server half of the CoreDbContext provider pair.
/// </summary>
/// <remarks>
/// <para>
/// Core migrations are generated twice — once against PostgreSQL (<c>DotNetCloud.Core.Data</c>) and
/// once against SQL Server (<c>DotNetCloud.Core.Data.SqlServer</c>). Nothing in CI runs a SQL Server
/// database, so a change migrated only for PostgreSQL passes every other test and then fails on the
/// production engine.
/// </para>
/// <para>
/// That is exactly what happened in 2026-11 with <c>AddModuleCapabilityDecisions</c>: the SQL Server
/// migration shipped with an empty <c>Up</c> and an un-updated snapshot, so the entity wrote
/// <c>RejectedAtUtc</c>/<c>RejectedByUserId</c>/<c>Status</c> into columns that never existed. Every
/// capability-grant insert failed, <c>ModuleCapabilityGrants</c> stayed empty and no module could use
/// a Restricted or Privileged capability.
/// </para>
/// </remarks>
[TestClass]
public class SqlServerMigrationParityTests
{
    private const string SqlServerConnection =
        "Server=localhost;Database=dotnetcloud;User Id=sa;Password=not-a-real-password;TrustServerCertificate=True";

    private const string SqlServerMigrationsAssembly = "DotNetCloud.Core.Data.SqlServer";

    /// <summary>
    /// Migrations that legitimately carry no operations (a model-only or documentation change).
    /// Anything not listed here must emit DDL on both providers.
    /// </summary>
    private static readonly HashSet<string> IntentionallyEmpty = new(StringComparer.Ordinal)
    {
        // The column itself was added by the PostgreSQL-only migration; the SQL Server side is a
        // no-op because the column already existed there.
        "AddMfaSetupRequiredColumn_SqlServer",
        "AddSequentialGuidDefaults",
    };

    /// <summary>
    /// The SQL Server model and its checked-in snapshot must agree. Drift here means a model change
    /// was migrated for PostgreSQL only (or not at all) and will surface as "Invalid column name"
    /// at runtime on SQL Server.
    /// </summary>
    [TestMethod]
    public void Model_WithSqlServerNaming_HasNoPendingModelChanges()
    {
        using var context = CreateSqlServerContext();

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The CoreDbContext model and the SQL Server CoreDbContextModelSnapshot have drifted; " +
            "add the SQL Server migration (dotnet ef migrations add <Name>_SqlServer --project " +
            "src/Core/DotNetCloud.Core.Data.SqlServer) before shipping.");
    }

    /// <summary>
    /// Every SQL Server migration must emit at least one operation, so a migration generated for
    /// PostgreSQL can never be mirrored by an empty stub. This is the guard that would have caught
    /// the empty <c>AddModuleCapabilityDecisions_SqlServer</c>.
    /// </summary>
    [TestMethod]
    public void Migrations_AllEmitOperations_ExceptTheIntentionallyEmptyOnes()
    {
        var migrationTypes = System.Reflection.Assembly
            .Load(SqlServerMigrationsAssembly)
            .GetTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

        Assert.IsTrue(
            migrationTypes.Count > 0,
            $"No migrations were found in the '{SqlServerMigrationsAssembly}' assembly.");

        List<string> empty = [];

        foreach (var migrationType in migrationTypes)
        {
            var migration = (Migration)Activator.CreateInstance(migrationType)!;

            if (migration.UpOperations.Count == 0 && !IntentionallyEmpty.Contains(migrationType.Name))
            {
                empty.Add(migrationType.Name);
            }
        }

        Assert.AreEqual(
            0,
            empty.Count,
            $"These SQL Server migrations emit no operations: {string.Join(", ", empty)}. " +
            "Regenerate them against SQL Server (dotnet ef migrations add) instead of copying the " +
            "PostgreSQL migration name, or add them to the intentionally-empty list with a reason.");
    }

    private static CoreDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer(
                SqlServerConnection,
                sqlServer => sqlServer.MigrationsAssembly(SqlServerMigrationsAssembly))
            .Options;

        return new CoreDbContext(options, new SqlServerNamingStrategy());
    }
}
