using DotNetCloud.CLI.Commands;

namespace DotNetCloud.CLI.Tests.Commands;

[TestClass]
public class SetupCommandTests
{
    [TestMethod]
    public void MaskConnectionString_EmptyString_ReturnsNotSet()
    {
        var result = SetupCommand.MaskConnectionString("");
        Assert.AreEqual("(not set)", result);
    }

    [TestMethod]
    public void MaskConnectionString_NullString_ReturnsNotSet()
    {
        var result = SetupCommand.MaskConnectionString(null!);
        Assert.AreEqual("(not set)", result);
    }

    [TestMethod]
    public void MaskConnectionString_WhitespaceString_ReturnsNotSet()
    {
        var result = SetupCommand.MaskConnectionString("   ");
        Assert.AreEqual("(not set)", result);
    }

    [TestMethod]
    public void MaskConnectionString_PostgreSql_MasksPassword()
    {
        var connStr = "Host=localhost;Database=dotnetcloud;Username=postgres;Password=secret123";
        var result = SetupCommand.MaskConnectionString(connStr);

        Assert.IsTrue(result.Contains("Host=localhost"));
        Assert.IsTrue(result.Contains("Database=dotnetcloud"));
        Assert.IsTrue(result.Contains("Username=postgres"));
        Assert.IsTrue(result.Contains("Password=****"));
        Assert.IsFalse(result.Contains("secret123"));
    }

    [TestMethod]
    public void MaskConnectionString_SqlServer_MasksPassword()
    {
        var connStr = "Server=localhost;Database=dotnetcloud;User=sa;Pwd=MyP@ssw0rd";
        var result = SetupCommand.MaskConnectionString(connStr);

        Assert.IsTrue(result.Contains("Server=localhost"));
        Assert.IsTrue(result.Contains("Database=dotnetcloud"));
        Assert.IsTrue(result.Contains("Pwd=****"));
        Assert.IsFalse(result.Contains("MyP@ssw0rd"));
    }

    [TestMethod]
    public void MaskConnectionString_NoPassword_ReturnsUnchanged()
    {
        var connStr = "Server=localhost;Database=dotnetcloud;Trusted_Connection=True";
        var result = SetupCommand.MaskConnectionString(connStr);

        Assert.AreEqual(connStr, result);
    }

    [TestMethod]
    public void MaskConnectionString_PreservesOtherFields()
    {
        var connStr = "Host=db.example.com;Port=5432;Database=dotnetcloud;Username=admin;Password=s3cret;SslMode=Require";
        var result = SetupCommand.MaskConnectionString(connStr);

        Assert.IsTrue(result.Contains("Host=db.example.com"));
        Assert.IsTrue(result.Contains("Port=5432"));
        Assert.IsTrue(result.Contains("SslMode=Require"));
        Assert.IsTrue(result.Contains("Password=****"));
        Assert.IsFalse(result.Contains("s3cret"));
    }

    [TestMethod]
    public void Create_ReturnsCommandNamedSetup()
    {
        var command = SetupCommand.Create();
        Assert.AreEqual("setup", command.Name);
    }

    [TestMethod]
    public void Create_HasDescription()
    {
        var command = SetupCommand.Create();
        Assert.IsFalse(string.IsNullOrWhiteSpace(command.Description));
    }

    [TestMethod]
    public void Create_HasBeginnerOption()
    {
        var command = SetupCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "--beginner");

        Assert.IsNotNull(option, "Expected --beginner option");
    }

    [TestMethod]
    public void Create_HasMigrateOnlyOption()
    {
        var command = SetupCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "--migrate-only");

        Assert.IsNotNull(option, "Expected --migrate-only option");
    }
}
