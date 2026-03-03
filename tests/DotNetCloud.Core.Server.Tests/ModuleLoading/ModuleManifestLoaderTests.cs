using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

[TestClass]
public class ModuleManifestLoaderTests
{
    private readonly ModuleManifestLoader _loader;

    public ModuleManifestLoaderTests()
    {
        _loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);
    }

    // ── LoadAndValidate ────────────────────────────────────────────

    [TestMethod]
    public void LoadAndValidateWhenFileNotFoundThenFailure()
    {
        var result = _loader.LoadAndValidate(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "manifest.json"),
            "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Count > 0);
        Assert.IsTrue(result.Errors[0].Contains("not found"));
    }

    [TestMethod]
    public void LoadAndValidateWhenNullPathThenThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => _loader.LoadAndValidate(null!, "test.module"));
    }

    [TestMethod]
    public void LoadAndValidateWhenNullExpectedIdThenThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => _loader.LoadAndValidate("/tmp/manifest.json", null!));
    }

    [TestMethod]
    public void LoadAndValidateWhenInvalidJsonThenFailure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "manifest.json");
        File.WriteAllText(manifestPath, "{ invalid json }}}");

        var result = _loader.LoadAndValidate(manifestPath, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid JSON")));
    }

    [TestMethod]
    public void LoadAndValidateWhenValidManifestThenSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "manifest.json");
        File.WriteAllText(manifestPath, """
            {
                "id": "test.module",
                "name": "Test Module",
                "version": "1.0.0"
            }
            """);

        var result = _loader.LoadAndValidate(manifestPath, "test.module");

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Manifest);
        Assert.AreEqual("test.module", result.Manifest.Id);
        Assert.AreEqual("Test Module", result.Manifest.Name);
        Assert.AreEqual("1.0.0", result.Manifest.Version);
    }

    // ── Validate (internal) ────────────────────────────────────────

    [TestMethod]
    public void ValidateWhenMissingIdThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "",
            Name = "Test",
            Version = "1.0.0"
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("ID is required")));
    }

    [TestMethod]
    public void ValidateWhenUppercaseIdThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "Test.Module",
            Name = "Test",
            Version = "1.0.0"
        };

        var result = _loader.Validate(manifest, "Test.Module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("lowercase")));
    }

    [TestMethod]
    public void ValidateWhenIdHasNoDotThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "testmodule",
            Name = "Test",
            Version = "1.0.0"
        };

        var result = _loader.Validate(manifest, "testmodule");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("dot-separated")));
    }

    [TestMethod]
    public void ValidateWhenIdDoesNotMatchDirectoryThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0"
        };

        var result = _loader.Validate(manifest, "other.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("does not match")));
    }

    [TestMethod]
    public void ValidateWhenMissingNameThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "",
            Version = "1.0.0"
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("name is required")));
    }

    [TestMethod]
    public void ValidateWhenMissingVersionThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = ""
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("version is required")));
    }

    [TestMethod]
    public void ValidateWhenInvalidVersionFormatThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "not-a-version"
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("semantic versioning")));
    }

    [TestMethod]
    public void ValidateWhenSemverWithPrereleaseThenSuccess()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0-beta.1"
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ValidateWhenInvalidRestartPolicyThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0",
            RestartPolicy = "InvalidPolicy"
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid restart policy")));
    }

    [TestMethod]
    [DataRow("Immediate")]
    [DataRow("ExponentialBackoff")]
    [DataRow("AlertOnly")]
    [DataRow("immediate")]
    public void ValidateWhenValidRestartPolicyThenSuccess(string policy)
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0",
            RestartPolicy = policy
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ValidateWhenNegativeMemoryLimitThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0",
            MemoryLimitMb = -100
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Memory limit must be positive")));
    }

    [TestMethod]
    public void ValidateWhenZeroMemoryLimitThenFailure()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0",
            MemoryLimitMb = 0
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void ValidateWhenPositiveMemoryLimitThenSuccess()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test",
            Version = "1.0.0",
            MemoryLimitMb = 512
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ValidateWhenMultipleErrorsThenAllAreReported()
    {
        var manifest = new ModuleManifestData
        {
            Id = "",
            Name = "",
            Version = ""
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Count >= 3);
    }

    [TestMethod]
    public void ValidateWhenValidManifestThenCanStartIsTrue()
    {
        var manifest = new ModuleManifestData
        {
            Id = "test.module",
            Name = "Test Module",
            Version = "1.0.0",
            RequiredCapabilities = ["IUserDirectory", "INotificationService"]
        };

        var result = _loader.Validate(manifest, "test.module");

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Manifest);
    }

    // ── CreateDefaultManifest ──────────────────────────────────────

    [TestMethod]
    public void CreateDefaultManifestThenUsesModuleIdAsName()
    {
        var manifest = _loader.CreateDefaultManifest("org.example.chat");

        Assert.AreEqual("org.example.chat", manifest.Id);
        Assert.AreEqual("org.example.chat", manifest.Name);
        Assert.AreEqual("0.0.0", manifest.Version);
    }

    [TestMethod]
    public void CreateDefaultManifestThenDescriptionIndicatesNoManifest()
    {
        var manifest = _loader.CreateDefaultManifest("org.example.chat");

        Assert.IsNotNull(manifest.Description);
        Assert.IsTrue(manifest.Description.Contains("no manifest"));
    }
}
