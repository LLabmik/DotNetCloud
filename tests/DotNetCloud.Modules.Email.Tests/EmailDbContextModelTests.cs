using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Email.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Drift guard for the Email module's EF model, which became schema-neutral in Track C.
/// </summary>
/// <remarks>
/// A sandboxed module owns the schema the core provisioned for it, so neither the model nor the
/// migrations may name a schema: the unqualified DDL lands wherever the connection's
/// <c>search_path</c> (PostgreSQL) or default schema (SQL Server) points. Naming one would also need
/// <c>CREATE</c> on the database, which the module principal deliberately does not have (42501).
/// </remarks>
[TestClass]
public class EmailDbContextModelTests
{
    /// <summary>
    /// The model and the checked-in snapshot must agree, otherwise the next migration silently
    /// re-creates columns the module already has.
    /// </summary>
    [TestMethod]
    public void Model_WithDesignTimeNaming_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseNpgsql("Host=localhost;Database=dotnetcloud;Username=dotnetcloud")
            .Options;

        using var context = new EmailDbContext(options, new PostgreSqlNamingStrategy());

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The Email model and EmailDbContextModelSnapshot have drifted; add an EF migration before shipping.");
    }

    /// <summary>
    /// The generated migration script must not create or name a schema.
    /// </summary>
    [TestMethod]
    public void MigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseNpgsql("Host=localhost;Database=dotnetcloud;Username=dotnetcloud")
            .Options;

        using var context = new EmailDbContext(options, new PostgreSqlNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The migration must not create a schema; the store provisions it and the module principal cannot (42501).");
        Assert.IsFalse(
            script.Contains("\"email\"", StringComparison.OrdinalIgnoreCase),
            "The migration must not name the logical schema; the connection's search_path decides where tables land.");
        StringAssert.Contains(script, "EmailAccounts", "the account table must still be created");
        StringAssert.Contains(script, "EmailMessages", "the message table must still be created");
    }

    /// <summary>
    /// SQL Server migrations are checked the same way, since the module must run on both engines.
    /// </summary>
    [TestMethod]
    public void SqlServerMigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseSqlServer("Server=localhost;Database=dotnetcloud;User Id=sa;Password=not-a-real-password;TrustServerCertificate=True")
            .Options;

        using var context = new EmailDbContext(options, new SqlServerNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("[email]", StringComparison.OrdinalIgnoreCase),
            "The SQL Server migration must not name the logical schema either.");
        StringAssert.Contains(script, "EmailAccounts", "the account table must still be created");
    }
}
