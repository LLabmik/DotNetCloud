using DotNetCloud.Client.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.Core.Tests.Api;

[TestClass]
public class DeviceIdProviderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dnc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void GetOrCreateDeviceId_FirstCall_GeneratesAndPersists()
    {
        var provider = new DeviceIdProvider(NullLogger<DeviceIdProvider>.Instance);
        var deviceId = provider.GetOrCreateDeviceId(_tempDir);

        Assert.AreNotEqual(Guid.Empty, deviceId);
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "device-id")));
    }

    [TestMethod]
    public void GetOrCreateDeviceId_SecondCall_ReturnsSameId()
    {
        var provider = new DeviceIdProvider(NullLogger<DeviceIdProvider>.Instance);
        var first = provider.GetOrCreateDeviceId(_tempDir);
        var second = provider.GetOrCreateDeviceId(_tempDir);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void GetOrCreateDeviceId_PersistedAcrossInstances()
    {
        var first = new DeviceIdProvider(NullLogger<DeviceIdProvider>.Instance).GetOrCreateDeviceId(_tempDir);
        var second = new DeviceIdProvider(NullLogger<DeviceIdProvider>.Instance).GetOrCreateDeviceId(_tempDir);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void GetOrCreateDeviceId_CorruptFile_Regenerates()
    {
        var filePath = Path.Combine(_tempDir, "device-id");
        File.WriteAllText(filePath, "not-a-guid");

        var provider = new DeviceIdProvider(NullLogger<DeviceIdProvider>.Instance);
        var deviceId = provider.GetOrCreateDeviceId(_tempDir);

        Assert.AreNotEqual(Guid.Empty, deviceId);
        // The file should now contain a valid GUID
        Assert.IsTrue(Guid.TryParse(File.ReadAllText(filePath).Trim(), out _));
    }

    [TestMethod]
    public void GetOrCreateDeviceId_EmptyFile_Regenerates()
    {
        var filePath = Path.Combine(_tempDir, "device-id");
        File.WriteAllText(filePath, "");

        var provider = new DeviceIdProvider(NullLogger<DeviceIdProvider>.Instance);
        var deviceId = provider.GetOrCreateDeviceId(_tempDir);

        Assert.AreNotEqual(Guid.Empty, deviceId);
    }
}
