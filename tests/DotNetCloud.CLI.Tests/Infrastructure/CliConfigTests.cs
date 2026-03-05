using System.Text.Json;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

[TestClass]
public class CliConfigTests
{
    [TestMethod]
    public void NewCliConfig_HasDefaultHttpPort()
    {
        var config = new CliConfig();
        Assert.AreEqual(5080, config.HttpPort);
    }

    [TestMethod]
    public void NewCliConfig_HasDefaultHttpsPort()
    {
        var config = new CliConfig();
        Assert.AreEqual(5443, config.HttpsPort);
    }

    [TestMethod]
    public void NewCliConfig_EnableHttpsIsTrue()
    {
        var config = new CliConfig();
        Assert.IsTrue(config.EnableHttps);
    }

    [TestMethod]
    public void NewCliConfig_DatabaseProviderIsEmpty()
    {
        var config = new CliConfig();
        Assert.AreEqual(string.Empty, config.DatabaseProvider);
    }

    [TestMethod]
    public void NewCliConfig_ConnectionStringIsEmpty()
    {
        var config = new CliConfig();
        Assert.AreEqual(string.Empty, config.ConnectionString);
    }

    [TestMethod]
    public void NewCliConfig_EnabledModulesIsEmpty()
    {
        var config = new CliConfig();
        Assert.IsNotNull(config.EnabledModules);
        Assert.AreEqual(0, config.EnabledModules.Count);
    }

    [TestMethod]
    public void NewCliConfig_SetupCompletedAtIsNull()
    {
        var config = new CliConfig();
        Assert.IsNull(config.SetupCompletedAt);
    }

    [TestMethod]
    public void NewCliConfig_TlsCertificatePathIsNull()
    {
        var config = new CliConfig();
        Assert.IsNull(config.TlsCertificatePath);
    }

    [TestMethod]
    public void NewCliConfig_UseLetsEncryptIsFalse()
    {
        var config = new CliConfig();
        Assert.IsFalse(config.UseLetsEncrypt);
    }

    [TestMethod]
    public void NewCliConfig_DataDirectoryContainsDotnetcloud()
    {
        var config = new CliConfig();
        Assert.IsTrue(config.DataDirectory.Contains("dotnetcloud"));
        // System installs use FHS path /var/lib/dotnetcloud; user-local ends with "data"
        Assert.IsTrue(
            config.DataDirectory.EndsWith("data") ||
            config.DataDirectory.EndsWith("dotnetcloud"));
    }

    [TestMethod]
    public void NewCliConfig_LogDirectoryContainsDotnetcloud()
    {
        var config = new CliConfig();
        Assert.IsTrue(config.LogDirectory.Contains("dotnetcloud"));
        // System installs use FHS path /var/log/dotnetcloud; user-local ends with "logs"
        Assert.IsTrue(
            config.LogDirectory.EndsWith("logs") ||
            config.LogDirectory.EndsWith("dotnetcloud"));
    }

    [TestMethod]
    public void NewCliConfig_BackupDirectoryContainsDotnetcloud()
    {
        var config = new CliConfig();
        Assert.IsTrue(config.BackupDirectory.Contains("dotnetcloud"));
        Assert.IsTrue(config.BackupDirectory.EndsWith("backups"));
    }

    [TestMethod]
    public void CliConfig_JsonRoundtrip_PreservesAllProperties()
    {
        var original = new CliConfig
        {
            DatabaseProvider = "PostgreSQL",
            ConnectionString = "Host=localhost;Database=test;Username=admin;Password=secret",
            HttpPort = 8080,
            HttpsPort = 8443,
            EnableHttps = false,
            TlsCertificatePath = "/etc/ssl/cert.pem",
            UseLetsEncrypt = true,
            LetsEncryptDomain = "cloud.example.com",
            OrganizationName = "TestOrg",
            AdminEmail = "admin@example.com",
            EnabledModules = ["dotnetcloud.files", "dotnetcloud.chat"],
            DataDirectory = "/var/lib/dotnetcloud/data",
            LogDirectory = "/var/log/dotnetcloud",
            BackupDirectory = "/var/backups/dotnetcloud",
            SetupCompletedAt = new DateTime(2025, 7, 19, 12, 0, 0, DateTimeKind.Utc)
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<CliConfig>(json, options)!;

        Assert.AreEqual(original.DatabaseProvider, deserialized.DatabaseProvider);
        Assert.AreEqual(original.ConnectionString, deserialized.ConnectionString);
        Assert.AreEqual(original.HttpPort, deserialized.HttpPort);
        Assert.AreEqual(original.HttpsPort, deserialized.HttpsPort);
        Assert.AreEqual(original.EnableHttps, deserialized.EnableHttps);
        Assert.AreEqual(original.TlsCertificatePath, deserialized.TlsCertificatePath);
        Assert.AreEqual(original.UseLetsEncrypt, deserialized.UseLetsEncrypt);
        Assert.AreEqual(original.LetsEncryptDomain, deserialized.LetsEncryptDomain);
        Assert.AreEqual(original.OrganizationName, deserialized.OrganizationName);
        Assert.AreEqual(original.AdminEmail, deserialized.AdminEmail);
        CollectionAssert.AreEqual(original.EnabledModules, deserialized.EnabledModules);
        Assert.AreEqual(original.DataDirectory, deserialized.DataDirectory);
        Assert.AreEqual(original.LogDirectory, deserialized.LogDirectory);
        Assert.AreEqual(original.BackupDirectory, deserialized.BackupDirectory);
        Assert.AreEqual(original.SetupCompletedAt, deserialized.SetupCompletedAt);
    }

    [TestMethod]
    public void CliConfig_SaveAndLoad_Roundtrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-test-{Guid.NewGuid():N}");
        var configFile = Path.Combine(tempDir, "config.json");

        try
        {
            Directory.CreateDirectory(tempDir);

            var config = new CliConfig
            {
                DatabaseProvider = "SqlServer",
                ConnectionString = "Server=localhost;Database=dotnetcloud;Trusted_Connection=True",
                HttpPort = 9090,
                OrganizationName = "TestOrg",
                EnabledModules = ["dotnetcloud.files"]
            };

            // Save
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(configFile, json);

            // Load
            var loadedJson = File.ReadAllText(configFile);
            var loaded = JsonSerializer.Deserialize<CliConfig>(loadedJson, options)!;

            Assert.AreEqual("SqlServer", loaded.DatabaseProvider);
            Assert.AreEqual(9090, loaded.HttpPort);
            Assert.AreEqual("TestOrg", loaded.OrganizationName);
            Assert.AreEqual(1, loaded.EnabledModules.Count);
            Assert.AreEqual("dotnetcloud.files", loaded.EnabledModules[0]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CliConfig_OrganizationNameIsNull()
    {
        var config = new CliConfig();
        Assert.IsNull(config.OrganizationName);
    }

    [TestMethod]
    public void CliConfig_AdminEmailIsNull()
    {
        var config = new CliConfig();
        Assert.IsNull(config.AdminEmail);
    }

    [TestMethod]
    public void CliConfig_LetsEncryptDomainIsNull()
    {
        var config = new CliConfig();
        Assert.IsNull(config.LetsEncryptDomain);
    }
}
