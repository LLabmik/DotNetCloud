using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Drift guard for the AI module's EF model, which became schema-neutral in Track C.
/// </summary>
/// <remarks>
/// A sandboxed module owns the schema the core provisioned for it, so neither the model nor the
/// migrations may name a schema: the unqualified DDL lands wherever the connection's
/// <c>search_path</c> (PostgreSQL) or default schema (SQL Server) points. Naming one would also need
/// <c>CREATE</c> on the database, which the module principal deliberately does not have (42501).
/// </remarks>
[TestClass]
public class AiDbContextModelTests
{
    private const string PostgreSqlConnection = "Host=localhost;Database=dotnetcloud;Username=dotnetcloud";
    private const string SqlServerConnection =
        "Server=localhost;Database=dotnetcloud;User Id=sa;Password=not-a-real-password;TrustServerCertificate=True";

    /// <summary>
    /// The model and the checked-in snapshot must agree, otherwise the next migration silently
    /// re-creates columns the module already has.
    /// </summary>
    [TestMethod]
    public void Model_WithDesignTimeNaming_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new AiDbContext(options, new PostgreSqlNamingStrategy());

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The AI model and AiDbContextModelSnapshot have drifted; add an EF migration before shipping.");
    }

    /// <summary>
    /// The generated PostgreSQL script must not create or name a schema, and must still create both
    /// tables.
    /// </summary>
    [TestMethod]
    public void MigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new AiDbContext(options, new PostgreSqlNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The migration must not create a schema; the store provisions it and the module principal cannot (42501).");
        Assert.IsFalse(
            script.Contains("\"ai\"", StringComparison.OrdinalIgnoreCase),
            "The migration must not name the logical schema; the connection's search_path decides where tables land.");
        StringAssert.Contains(script, "Conversations", "the Conversations table must still be created");
        StringAssert.Contains(script, "ConversationMessages", "the ConversationMessages table must still be created");
    }

    /// <summary>
    /// SQL Server migrations are checked the same way, since the module must run on both engines.
    /// </summary>
    [TestMethod]
    public void SqlServerMigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseSqlServer(SqlServerConnection, sql =>
                sql.MigrationsAssembly("DotNetCloud.Modules.AI.Data.SqlServer"))
            .Options;

        using var context = new AiDbContext(options, new SqlServerNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The SQL Server migration must not create a schema either.");
        Assert.IsFalse(
            script.Contains("[ai]", StringComparison.OrdinalIgnoreCase),
            "The SQL Server migration must not name the logical schema.");
        StringAssert.Contains(script, "Conversations", "the Conversations table must still be created");
    }
}
