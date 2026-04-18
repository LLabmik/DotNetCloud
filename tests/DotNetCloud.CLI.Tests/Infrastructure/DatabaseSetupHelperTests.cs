using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

[TestClass]
public class DatabaseSetupHelperTests
{
    [TestMethod]
    public void BuildPostgreSqlConnectionString_ReturnsExpectedFormat()
    {
        var result = DatabaseSetupHelper.BuildPostgreSqlConnectionString(
            "localhost", "dotnetcloud", "dbuser", "s3cret");

        Assert.AreEqual(
            "Host=localhost;Database=dotnetcloud;Username=dbuser;Password=s3cret",
            result);
    }

    [TestMethod]
    public void BuildPostgreSqlConnectionString_WithCustomHost_IncludesHost()
    {
        var result = DatabaseSetupHelper.BuildPostgreSqlConnectionString(
            "db.example.com", "mydb", "admin", "pass");

        Assert.IsTrue(result.StartsWith("Host=db.example.com;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildPostgreSqlConnectionString_WithSpecialCharPassword_PreservesPassword()
    {
        var result = DatabaseSetupHelper.BuildPostgreSqlConnectionString(
            "localhost", "dotnetcloud", "user", "p@ss=w;rd");

        Assert.IsTrue(result.Contains("Password=p@ss=w;rd", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildSqlServerConnectionString_TrustedConnection_OmitsCredentials()
    {
        var result = DatabaseSetupHelper.BuildSqlServerConnectionString(
            "localhost", "dotnetcloud", null, null, trustedConnection: true);

        Assert.AreEqual(
            "Server=localhost;Database=dotnetcloud;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
            result);
    }

    [TestMethod]
    public void BuildSqlServerConnectionString_TrustedConnection_IgnoresSuppliedCredentials()
    {
        var result = DatabaseSetupHelper.BuildSqlServerConnectionString(
            "localhost", "dotnetcloud", "sa", "secret", trustedConnection: true);

        Assert.IsFalse(result.Contains("sa", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("secret", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Trusted_Connection=True", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildSqlServerConnectionString_SqlAuth_IncludesCredentials()
    {
        var result = DatabaseSetupHelper.BuildSqlServerConnectionString(
            "sql.example.com", "dotnetcloud", "sa", "MyP@ss", trustedConnection: false);

        Assert.AreEqual(
            "Server=sql.example.com;Database=dotnetcloud;User Id=sa;Password=MyP@ss;TrustServerCertificate=True;MultipleActiveResultSets=True",
            result);
    }

    [TestMethod]
    public void BuildSqlServerConnectionString_SqlAuth_IncludesTrustServerCertificate()
    {
        var result = DatabaseSetupHelper.BuildSqlServerConnectionString(
            "localhost", "db", "user", "pass", trustedConnection: false);

        Assert.IsTrue(result.Contains("TrustServerCertificate=True", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildMariaDbConnectionString_ReturnsExpectedFormat()
    {
        var result = DatabaseSetupHelper.BuildMariaDbConnectionString(
            "localhost", "dotnetcloud", "dbuser", "s3cret");

        Assert.AreEqual(
            "Server=localhost;Database=dotnetcloud;User=dbuser;Password=s3cret",
            result);
    }

    [TestMethod]
    public void BuildMariaDbConnectionString_WithCustomServer_IncludesServer()
    {
        var result = DatabaseSetupHelper.BuildMariaDbConnectionString(
            "maria.local", "appdb", "admin", "pass");

        Assert.IsTrue(result.StartsWith("Server=maria.local;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildPostgreSqlConnectionString_AllPartsAppearInOutput()
    {
        var result = DatabaseSetupHelper.BuildPostgreSqlConnectionString(
            "myhost", "mydb", "myuser", "mypass");

        Assert.IsTrue(result.Contains("Host=myhost", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Database=mydb", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Username=myuser", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Password=mypass", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildMariaDbConnectionString_AllPartsAppearInOutput()
    {
        var result = DatabaseSetupHelper.BuildMariaDbConnectionString(
            "myhost", "mydb", "myuser", "mypass");

        Assert.IsTrue(result.Contains("Server=myhost", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Database=mydb", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("User=myuser", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Password=mypass", StringComparison.Ordinal));
    }
}
