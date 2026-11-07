using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Quotas;

/// <summary>
/// Tests for <see cref="ModuleUsageMeasurer"/> directory accounting.
/// </summary>
[TestClass]
public class ModuleUsageMeasurerTests
{
    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"dnc-usage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ModuleUsageMeasurer CreateMeasurer() =>
        new(
            new CoreDatabaseOptions("Host=127.0.0.1;Port=1;Database=unused;Timeout=1", DatabaseProvider.PostgreSQL),
            NullLogger<ModuleUsageMeasurer>.Instance,
            _root);

    [TestMethod]
    public void MeasureDirectoryBytes_WhenDirectoryMissing_ReturnsZero()
    {
        Assert.AreEqual(0, ModuleUsageMeasurer.MeasureDirectoryBytes(Path.Combine(_root, "missing")));
    }

    [TestMethod]
    public void MeasureDirectoryBytes_SumsNestedFiles()
    {
        var nested = Path.Combine(_root, "a", "b");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(_root, "one.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(nested, "two.bin"), new byte[250]);

        Assert.AreEqual(350, ModuleUsageMeasurer.MeasureDirectoryBytes(_root));
    }

    [TestMethod]
    public void MeasureDataDirectoryBytes_ReadsTheSandboxDataDirectory()
    {
        var moduleId = "org.example.usage";
        var dataDirectory = Path.Combine(_root, "modules", moduleId, "data");
        var configDirectory = Path.Combine(_root, "modules", moduleId, "config");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(configDirectory);
        File.WriteAllBytes(Path.Combine(dataDirectory, "payload.bin"), new byte[512]);
        File.WriteAllBytes(Path.Combine(configDirectory, "ignored.bin"), new byte[4096]);

        var measurer = CreateMeasurer();

        Assert.AreEqual(512, measurer.MeasureDataDirectoryBytes(moduleId),
            "Only the module's private data directory counts toward the data quota");
    }

    [TestMethod]
    public void MeasureDataDirectoryBytes_WhenNoDirectory_ReturnsZero()
    {
        Assert.AreEqual(0, CreateMeasurer().MeasureDataDirectoryBytes("org.example.empty"));
    }

    [TestMethod]
    public void MeasureDatabaseBytesAsync_WhenDatabaseUnreachable_ReturnsNull()
    {
        var measurer = CreateMeasurer();

        var bytes = measurer
            .MeasureDatabaseBytesAsync("dnc_never_exists_00000000", CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.IsNull(bytes, "A failed measurement must be reported as unknown, never as over quota");
    }
}
