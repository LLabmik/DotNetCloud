using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the provider-native tool invocation built by <see cref="DatabaseBackupTool"/>.
/// </summary>
/// <remarks>
/// The SQL Server path must pass the login (<c>-U</c>) and the database name from the connection
/// string, and must never place the password on the command line, where it would be visible in
/// process listings.
/// </remarks>
[TestClass]
public class DatabaseBackupToolTests
{
    private const string SqlServerConnectionString =
        "Server=db.example.test,1433;Database=cloud;User Id=sa;Password=S3cret!Pass;TrustServerCertificate=true";

    private static DatabaseBackupTool CreateTool()
        => new(NullLogger<DatabaseBackupTool>.Instance);

    [TestMethod]
    public void BuildSqlServerDumpArguments_ConnectionStringWithCredentials_PassesServerDatabaseAndUser()
    {
        // Act
        var arguments = DatabaseBackupTool.BuildSqlServerDumpArguments(
            SqlServerConnectionString, "/tmp/dump.bak");

        // Assert
        CollectionAssert.Contains(arguments, "-S");
        CollectionAssert.Contains(arguments, "db.example.test,1433");
        CollectionAssert.Contains(arguments, "-d");
        CollectionAssert.Contains(arguments, "cloud");
        CollectionAssert.Contains(arguments, "-U");
        CollectionAssert.Contains(arguments, "sa");
        CollectionAssert.Contains(arguments, "-C");
        CollectionAssert.Contains(arguments, "-b");
        CollectionAssert.Contains(
            arguments,
            "BACKUP DATABASE [cloud] TO DISK = '/tmp/dump.bak'");
    }

    [TestMethod]
    public void BuildSqlServerDumpArguments_ConnectionStringWithCredentials_KeepsPasswordOffCommandLine()
    {
        // Act
        var arguments = DatabaseBackupTool.BuildSqlServerDumpArguments(
            SqlServerConnectionString, "/tmp/dump.bak");

        // Assert
        Assert.IsFalse(arguments.Contains("-P"), "The password must not be passed as an argument.");
        Assert.IsFalse(arguments.Contains("S3cret!Pass"), "The password must not leak onto the command line.");
        Assert.IsFalse(
            arguments.Any(a => a.Contains("S3cret!Pass", StringComparison.Ordinal)),
            "The password must not appear inside any argument.");
    }

    [TestMethod]
    public void BuildSqlServerRestoreArguments_ConnectionStringWithDatabase_RestoresThatDatabase()
    {
        // Act
        var arguments = DatabaseBackupTool.BuildSqlServerRestoreArguments(
            SqlServerConnectionString, "/tmp/dump.bak");

        // Assert
        CollectionAssert.Contains(
            arguments,
            "RESTORE DATABASE [cloud] FROM DISK = '/tmp/dump.bak'");
    }

    [TestMethod]
    public void BuildSqlServerDumpArguments_EmptyConnectionString_UsesDefaults()
    {
        // Act
        var arguments = DatabaseBackupTool.BuildSqlServerDumpArguments(null, "/tmp/dump.bak");

        // Assert
        CollectionAssert.Contains(arguments, "-S");
        CollectionAssert.Contains(arguments, "localhost");
        CollectionAssert.DoesNotContain(arguments, "-U");
        CollectionAssert.DoesNotContain(arguments, "-C");
        CollectionAssert.Contains(
            arguments,
            "BACKUP DATABASE [dotnetcloud] TO DISK = '/tmp/dump.bak'");
    }

    [TestMethod]
    public void BuildPasswordEnvironment_ConnectionStringWithPassword_SetsToolVariable()
    {
        // Act
        var environment = DatabaseBackupTool.BuildPasswordEnvironment(
            SqlServerConnectionString, "SQLCMDPASSWORD");

        // Assert
        Assert.AreEqual(1, environment.Count);
        Assert.AreEqual("S3cret!Pass", environment["SQLCMDPASSWORD"]);
    }

    [TestMethod]
    public void BuildPasswordEnvironment_ConnectionStringWithoutPassword_ReturnsEmptyEnvironment()
    {
        // Act
        var environment = DatabaseBackupTool.BuildPasswordEnvironment(
            "Host=localhost;Database=cloud;Username=test", "PGPASSWORD");

        // Assert
        Assert.AreEqual(0, environment.Count);
    }

    [TestMethod]
    public void BuildPostgreSqlDumpArguments_ConnectionString_PassesConnectionPiecesAndOutputFile()
    {
        // Act
        var arguments = DatabaseBackupTool.BuildPostgreSqlDumpArguments(
            "Host=db.example.test;Port=6432;Database=cloud;Username=dnc", "/tmp/dump.sql");

        // Assert
        Assert.AreEqual(
            "-h|db.example.test|-p|6432|-U|dnc|-d|cloud|-Fp|-f|/tmp/dump.sql",
            string.Join("|", arguments));
    }

    [TestMethod]
    public void BuildPostgreSqlRestoreArguments_ConnectionString_StopsOnFirstError()
    {
        // Act
        var arguments = DatabaseBackupTool.BuildPostgreSqlRestoreArguments(
            "Host=db.example.test;Database=cloud;Username=dnc", "/tmp/dump.sql");

        // Assert
        CollectionAssert.Contains(arguments, "ON_ERROR_STOP=1");
        CollectionAssert.Contains(arguments, "-f");
        CollectionAssert.Contains(arguments, "/tmp/dump.sql");
    }

    [TestMethod]
    public async Task CreateDumpAsync_UnsupportedProvider_ThrowsNotSupportedException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => CreateTool().CreateDumpAsync("mysql", null, "/tmp/dump.sql", CancellationToken.None));
    }

    [TestMethod]
    public async Task RestoreDumpAsync_UnsupportedProvider_ThrowsNotSupportedException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => CreateTool().RestoreDumpAsync("mysql", null, "/tmp/dump.sql", CancellationToken.None));
    }
}
