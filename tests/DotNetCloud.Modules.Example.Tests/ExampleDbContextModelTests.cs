using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Example.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Drift guard for the Example module's EF model.
/// </summary>
/// <remarks>
/// The sandboxed host suppresses EF's pending-model-changes warning because its model's default
/// schema is the store's generated schema at runtime, while the snapshot records the logical
/// "example" schema. That suppression is only safe while the model still matches the checked-in
/// migration at design-time settings — which is exactly what this test asserts.
/// </remarks>
[TestClass]
public class ExampleDbContextModelTests
{
    /// <summary>
    /// Building the model with the design-time naming strategy must produce no pending migrations;
    /// otherwise the suppression in the host would hide a forgotten migration.
    /// </summary>
    [TestMethod]
    public void Model_WithDesignTimeSchema_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<ExampleDbContext>()
            .UseNpgsql("Host=localhost;Database=dotnetcloud;Username=dotnetcloud")
            .Options;

        using var context = new ExampleDbContext(options, new PostgreSqlNamingStrategy());

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "ExampleNote model and ExampleDbContextModelSnapshot have drifted; add an EF migration before shipping.");
    }

    /// <summary>
    /// The migrations must be schema-neutral: a sandboxed module's principal owns only its generated
    /// schema and has no <c>CREATE</c> privilege on the database, so naming a schema (or emitting
    /// <c>CREATE SCHEMA</c>) fails the whole migration with PostgreSQL 42501. Unqualified DDL lands
    /// in the module's schema through the <c>search_path</c> pinned by the provisioned connection
    /// string.
    /// </summary>
    [TestMethod]
    public void MigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<ExampleDbContext>()
            .UseNpgsql("Host=localhost;Database=dotnetcloud;Username=dotnetcloud")
            .Options;

        using var context = new ExampleDbContext(options, new PostgreSqlNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The migration must not create a schema; the store provisions it and the module principal cannot (42501).");
        Assert.IsFalse(
            script.Contains("\"example\"", StringComparison.OrdinalIgnoreCase),
            "The migration must not name the logical schema; the connection's search_path decides where tables land.");
        StringAssert.Contains(script, "Notes", "the Notes table must still be created");
    }
}