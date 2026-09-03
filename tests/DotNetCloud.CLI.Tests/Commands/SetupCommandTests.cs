using DotNetCloud.CLI.Commands;
using DotNetCloud.CLI.Infrastructure;

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

    [TestMethod]
    public void HasExistingCertificate_NullPath_ReturnsFalse()
    {
        var config = new CliConfig { TlsCertificatePath = null };

        Assert.IsFalse(SetupCommand.HasExistingCertificate(config));
    }

    [TestMethod]
    public void HasExistingCertificate_WhitespacePath_ReturnsFalse()
    {
        var config = new CliConfig { TlsCertificatePath = "   " };

        Assert.IsFalse(SetupCommand.HasExistingCertificate(config));
    }

    [TestMethod]
    public void HasExistingCertificate_MissingFile_ReturnsFalse()
    {
        var config = new CliConfig { TlsCertificatePath = "/nonexistent/dotnetcloud-cert.pfx" };

        Assert.IsFalse(SetupCommand.HasExistingCertificate(config));
    }

    [TestMethod]
    public void HasExistingCertificate_ExistingFile_ReturnsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnetcloud-test-{Guid.NewGuid():N}.pfx");
        File.WriteAllText(path, "test");

        try
        {
            var config = new CliConfig { TlsCertificatePath = path };

            Assert.IsTrue(SetupCommand.HasExistingCertificate(config));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void OptionalModules_ExcludesAllRequiredModules()
    {
        var required = DotNetCloud.Core.Modules.RequiredModules.ModuleIds;

        foreach (var moduleId in SetupCommand.OptionalModules)
        {
            Assert.IsFalse(required.Contains(moduleId), $"{moduleId} is required but listed as optional");
        }
    }

    [TestMethod]
    public void OptionalModules_ContainsExpectedSet()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "dotnetcloud.tracks",
                "dotnetcloud.music",
                "dotnetcloud.photos",
                "dotnetcloud.video",
                "dotnetcloud.bookmarks",
                "dotnetcloud.email",
                "dotnetcloud.ai"
            },
            SetupCommand.OptionalModules);
    }

    [TestMethod]
    public void GenerateWopiTokenSigningKey_ReturnsBase64Key()
    {
        var key = SetupCommand.GenerateWopiTokenSigningKey();

        Assert.IsNotNull(key);
        var decoded = Convert.FromBase64String(key);
        Assert.AreEqual(32, decoded.Length);
    }

    [TestMethod]
    public void GenerateWopiTokenSigningKey_TwoCalls_Differ()
    {
        var first = SetupCommand.GenerateWopiTokenSigningKey();
        var second = SetupCommand.GenerateWopiTokenSigningKey();

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public async Task VerifyDatabaseConnectionAsync_UnreachableDatabase_ReturnsFalse()
    {
        // Reserve an ephemeral loopback port then release it so nothing is
        // listening. A real connection attempt must fail — this guards the
        // regression where verification returned true without ever opening a
        // connection, which let the wizard skip database auto-creation and
        // crash-loop the server on first start (28P01/3D000).
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var config = new CliConfig
        {
            DatabaseProvider = "PostgreSQL",
            ConnectionString = $"Host=127.0.0.1;Port={port};Database=dotnetcloud;Username=dotnetcloud;Password=not-the-password;Timeout=3"
        };

        var result = await SetupCommand.VerifyDatabaseConnectionAsync(config);

        Assert.IsFalse(result, "Verification must fail when the database cannot be reached.");
    }

    [TestMethod]
    public async Task VerifyDatabaseConnectionAsync_EmptyConnectionString_ReturnsFalse()
    {
        var config = new CliConfig { DatabaseProvider = "PostgreSQL" };

        var result = await SetupCommand.VerifyDatabaseConnectionAsync(config);

        Assert.IsFalse(result, "Verification must fail when no connection string is configured.");
    }
}
