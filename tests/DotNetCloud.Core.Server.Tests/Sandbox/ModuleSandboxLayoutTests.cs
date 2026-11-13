using System.Text.Json;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleSandboxLayout"/> and error sanitization.
/// </summary>
[TestClass]
public class ModuleSandboxLayoutTests
{
    private string _dataRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "dnc-sandbox-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(_dataRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Create_PlacesDirectoriesUnderModulesRoot()
    {
        var layout = ModuleSandboxLayout.Create(_dataRoot, "org.example.customapp");
        layout.EnsureCreated();

        Assert.IsTrue(Directory.Exists(layout.ConfigDirectory));
        Assert.IsTrue(Directory.Exists(layout.DataDirectory));
        Assert.IsTrue(Directory.Exists(layout.RunDirectory));
        StringAssert.Contains(layout.ConfigDirectory, Path.Combine("modules", "org.example.customapp", "config"));
    }

    [TestMethod]
    public void WriteModuleJson_ContainsOnlyNonSecretTopology()
    {
        var layout = ModuleSandboxLayout.Create(_dataRoot, "org.example.customapp");
        layout.EnsureCreated();

        layout.WriteModuleJson(
            "1.2.0",
            ModuleDataStoreMode.Isolated,
            "PostgreSQL",
            "dnc_example_1a2b3c4d",
            "http://localhost:50100",
            "http://localhost:50101");

        var json = File.ReadAllText(layout.ModuleJsonPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual("org.example.customapp", root.GetProperty("moduleId").GetString());
        Assert.AreEqual("1.2.0", root.GetProperty("version").GetString());
        Assert.AreEqual("isolated", root.GetProperty("dataStore").GetString());
        Assert.AreEqual("PostgreSQL", root.GetProperty("databaseProvider").GetString());
        Assert.AreEqual("dnc_example_1a2b3c4d", root.GetProperty("databaseSchema").GetString());

        Assert.IsFalse(json.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Create_RejectsUnsafeModuleIds()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ModuleSandboxLayout.Create(_dataRoot, "../escape"));

        Assert.ThrowsExactly<ArgumentException>(
            () => ModuleSandboxLayout.Create(_dataRoot, "org.example/evil"));
    }

    [TestMethod]
    public void EnsureCreated_RestrictsDirectoriesToOwnerOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file modes are not applicable on Windows.");
            return;
        }

        var layout = ModuleSandboxLayout.Create(_dataRoot, "org.example.customapp");
        layout.EnsureCreated();

        var mode = File.GetUnixFileMode(layout.DataDirectory);
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode);
    }

    [TestMethod]
    [DataRow("password=abc123;Host=db", "password=***;Host=db")]
    [DataRow("Login failed; PWD=hunter2", "Login failed; PWD=***")]
    public void SanitizeError_RedactsCredentialFragments(string input, string expected)
    {
        Assert.AreEqual(expected, ModuleDataStoreProvisioningService.SanitizeError(input));
    }

    [TestMethod]
    public void SanitizeError_TruncatesToStoredErrorLimit()
    {
        var sanitized = ModuleDataStoreProvisioningService.SanitizeError(new string('x', 5000));

        Assert.AreEqual(2000, sanitized.Length);
    }
}
