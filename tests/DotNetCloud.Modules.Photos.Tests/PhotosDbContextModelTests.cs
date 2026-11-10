using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Photos.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Drift guard for the Photos module's EF model, which became schema-neutral in Track C.
/// </summary>
/// <remarks>
/// A sandboxed module owns the schema the core provisioned for it, so neither the model nor the
/// migrations may name a schema: the unqualified DDL lands wherever the connection's
/// <c>search_path</c> (PostgreSQL) or default schema (SQL Server) points. Naming one would also need
/// <c>CREATE</c> on the database, which the module principal deliberately does not have (42501).
/// </remarks>
[TestClass]
public class PhotosDbContextModelTests
{
    private const string PostgreSqlConnection = "Host=localhost;Database=dotnetcloud;Username=dotnetcloud";
    private const string SqlServerConnection =
        "Server=localhost;Database=dotnetcloud;User Id=sa;Password=not-a-real-password;TrustServerCertificate=True";

    /// <summary>
    /// The model and the checked-in snapshot must agree; the migrations were re-stamped schema-neutral
    /// with their original ids, so drift here means an edit to the model was not migrated.
    /// </summary>
    [TestMethod]
    public void Model_WithDesignTimeNaming_HasNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new PhotosDbContext(options, new PostgreSqlNamingStrategy());

        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The Photos model and PhotosDbContextModelSnapshot have drifted; add an EF migration before shipping.");
    }

    /// <summary>
    /// The generated PostgreSQL script must not create or name a schema, and must still create the
    /// photo catalogue tables.
    /// </summary>
    [TestMethod]
    public void MigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseNpgsql(PostgreSqlConnection)
            .Options;

        using var context = new PhotosDbContext(options, new PostgreSqlNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The migration must not create a schema; the store provisions it and the module principal cannot (42501).");
        Assert.IsFalse(
            script.Contains("\"photos\".", StringComparison.Ordinal),
            "The migration must not schema-qualify its tables; the connection's search_path decides where they land.");

        StringAssert.Contains(script, "Photos", "the Photos table must still be created");
        StringAssert.Contains(script, "Albums", "the Albums table must still be created");
        StringAssert.Contains(script, "PhotoEditRecords", "the PhotoEditRecords table must still be created");
    }

    /// <summary>
    /// SQL Server migrations are checked the same way, since the module must run on both engines.
    /// </summary>
    [TestMethod]
    public void SqlServerMigrationScript_IsSchemaNeutral()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseSqlServer(SqlServerConnection, sql =>
                sql.MigrationsAssembly("DotNetCloud.Modules.Photos.Data.SqlServer"))
            .Options;

        using var context = new PhotosDbContext(options, new SqlServerNamingStrategy());
        var script = context.Database.GenerateCreateScript();

        Assert.IsFalse(
            script.Contains("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase),
            "The SQL Server migration must not create a schema either.");
        Assert.IsFalse(
            script.Contains("[photos].", StringComparison.Ordinal),
            "The SQL Server migration must not schema-qualify its tables.");
        StringAssert.Contains(script, "Photos", "the Photos table must still be created");
    }
}
