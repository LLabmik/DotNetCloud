using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class SyncDeviceResolverTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static SyncDeviceResolver CreateResolver(FilesDbContext db) =>
        new(db, NullLogger<SyncDeviceResolver>.Instance);

    [TestMethod]
    public async Task ResolveAsync_NullDeviceId_ReturnsNull()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync(null, Guid.NewGuid(), "host", "Linux", "0.1.0", CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveAsync_EmptyDeviceId_ReturnsNull()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);

        var result = await resolver.ResolveAsync(Guid.Empty, Guid.NewGuid(), "host", "Linux", "0.1.0", CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveAsync_NewDevice_CreatesAndReturns()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await resolver.ResolveAsync(deviceId, userId, "test-host", "Windows", "0.1.0", CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(deviceId, result.Id);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("test-host", result.DeviceName);
        Assert.AreEqual("Windows", result.Platform);
        Assert.AreEqual("0.1.0", result.ClientVersion);
        Assert.IsTrue(result.IsActive);
    }

    [TestMethod]
    public async Task ResolveAsync_ExistingDevice_UpdatesLastSeen()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // First contact
        var first = await resolver.ResolveAsync(deviceId, userId, "host1", "Linux", "0.1.0", CancellationToken.None);
        Assert.IsNotNull(first);
        var firstSeenAt = first.LastSeenAt;

        // Small delay to differentiate timestamps
        await Task.Delay(10);

        // Second contact with updated info
        var second = await resolver.ResolveAsync(deviceId, userId, "host2", "Windows", "0.2.0", CancellationToken.None);

        Assert.IsNotNull(second);
        Assert.AreEqual(deviceId, second.Id);
        Assert.AreEqual("host2", second.DeviceName);
        Assert.AreEqual("Windows", second.Platform);
        Assert.AreEqual("0.2.0", second.ClientVersion);
        Assert.IsTrue(second.LastSeenAt >= firstSeenAt);
    }

    [TestMethod]
    public async Task ResolveAsync_CrossUserSpoofing_ReturnsNull()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);
        var deviceId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Register device for user1
        var first = await resolver.ResolveAsync(deviceId, user1, "host", "Linux", "0.1.0", CancellationToken.None);
        Assert.IsNotNull(first);

        // Attempt the same device ID with user2 — should be rejected
        var spoofed = await resolver.ResolveAsync(deviceId, user2, "host", "Linux", "0.1.0", CancellationToken.None);

        Assert.IsNull(spoofed);
    }

    [TestMethod]
    public async Task ResolveAsync_PersistsToDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create and resolve
        using (var db = CreateContext(dbName))
        {
            var resolver = CreateResolver(db);
            await resolver.ResolveAsync(deviceId, userId, "host", "Linux", "0.1.0", CancellationToken.None);
        }

        // Verify from fresh context
        using (var db = CreateContext(dbName))
        {
            var device = await db.SyncDevices.FindAsync(deviceId);
            Assert.IsNotNull(device);
            Assert.AreEqual(userId, device.UserId);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_NullDeviceName_DefaultsToUnknown()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await resolver.ResolveAsync(deviceId, userId, null, null, null, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual("Unknown", result.DeviceName);
    }

    [TestMethod]
    public async Task ResolveAsync_InactiveDevice_ReturnsNull()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Register device, then deactivate it
        db.SyncDevices.Add(new SyncDevice
        {
            Id = deviceId,
            UserId = userId,
            DeviceName = "disabled-box",
            IsActive = false
        });
        await db.SaveChangesAsync();

        var result = await resolver.ResolveAsync(deviceId, userId, "disabled-box", "Linux", "0.1.0", CancellationToken.None);

        Assert.IsNull(result, "Deactivated devices should be rejected by the resolver.");
    }

    [TestMethod]
    public async Task ResolveAsync_ReactivatedDevice_ReturnsDevice()
    {
        using var db = CreateContext();
        var resolver = CreateResolver(db);
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Register device as active
        db.SyncDevices.Add(new SyncDevice
        {
            Id = deviceId,
            UserId = userId,
            DeviceName = "reactivated-box",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await resolver.ResolveAsync(deviceId, userId, "reactivated-box", "Linux", "0.1.0", CancellationToken.None);

        Assert.IsNotNull(result, "Active devices should be resolved normally.");
        Assert.AreEqual(deviceId, result.Id);
    }
}
