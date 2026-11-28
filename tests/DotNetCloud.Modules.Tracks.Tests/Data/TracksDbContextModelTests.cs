using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Tracks.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Tests.Data;

/// <summary>
/// Drift and schema-neutrality guard for the Tracks module's EF model, which became schema-neutral
/// in Track C (see <c>docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md</c> §7).
/// </summary>
/// <remarks>
/// <para>
/// A sandboxed module owns the schema the core provisioned for it, so neither the model nor the
/// migrations may name a schema: the unqualified DDL lands wherever the connection's
/// <c>search_path</c> (PostgreSQL) or default schema (SQL Server) points. Naming one would also need
/// <c>CREATE</c> on the database, which the module principal deliberately does not have (42501).
/// </para>
/// <para>
/// Drift here means an entity or configuration changed without a matching migration, which would make
/// the module fail its next <c>MigrateAsync</c> (or, worse, silently differ from its migrations).
/// </para>
/// </remarks>
[TestClass]
public class TracksDbContextModelTests
{
    private const string PostgreSqlConnection = "Host=localhost;Database=dotnetcloud;Username=dotnetcloud";
    private const string SqlServerConnection =
        "Server=localhost;Database=dotnetcloud;User Id=sa;Password=not-a-real-password;TrustServerCertificate=True";

    /// <summary>
    /// The model and the checked-in snapshot must agree.
    /// </summary>
    [TestMethod]
    public void Model_WithDesignTimeNaming_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new TracksDbContext(options, new PostgreSqlNamingStrategy());

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The Tracks model and TracksDbContextModelSnapshot have drifted; add an EF migration before shipping.");
    }

    /// <summary>
    /// The generated PostgreSQL script must not create or name a schema, and must still create the
    /// Tracks tables.
    /// </summary>
    [TestMethod]
    public void MigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new TracksDbContext(options, new PostgreSqlNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The migration must not create a schema; the store provisions it and the module principal cannot (42501).");
        Assert.IsFalse(
            script.Contains("\"tracks\".", StringComparison.Ordinal),
            "The migration must not schema-qualify its tables; the connection's search_path decides where they land.");

        StringAssert.Contains(script, "\"Products\"", "the Products table must still be created");
        StringAssert.Contains(script, "\"WorkItems\"", "the WorkItems table must still be created");
        StringAssert.Contains(script, "\"SprintDiscussions\"", "the SprintDiscussions table must still be created");
    }

    /// <summary>
    /// SQL Server migrations are checked the same way, since the module must run on both engines.
    /// </summary>
    [TestMethod]
    public void SqlServerMigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<TracksDbContext>()
            .UseSqlServer(SqlServerConnection, sql =>
                sql.MigrationsAssembly("DotNetCloud.Modules.Tracks.Data.SqlServer"))
            .Options;

        using var context = new TracksDbContext(options, new SqlServerNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The SQL Server migration must not create a schema either.");
        Assert.IsFalse(
            script.Contains("[tracks].", StringComparison.Ordinal),
            "The SQL Server migration must not schema-qualify its tables.");
        StringAssert.Contains(script, "[Products]", "the Products table must still be created");
        StringAssert.Contains(script, "[WorkItems]", "the WorkItems table must still be created");
    }
}
