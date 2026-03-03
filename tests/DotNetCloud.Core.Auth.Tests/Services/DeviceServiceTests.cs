using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Services;

/// <summary>
/// Tests for <see cref="DeviceService"/>.
/// </summary>
[TestClass]
public class DeviceServiceTests
{
    private CoreDbContext _dbContext = null!;
    private Mock<ILogger<DeviceService>> _loggerMock = null!;
    private DeviceService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"DeviceTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        _loggerMock = new Mock<ILogger<DeviceService>>();

        _service = new DeviceService(_dbContext, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    // ---------------------------------------------------------------------------
    // GetDevicesAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenUserHasNoDevicesThenReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var devices = await _service.GetDevicesAsync(userId);

        // Assert
        Assert.AreEqual(0, devices.Count);
    }

    [TestMethod]
    public async Task WhenUserHasDevicesThenReturnsAllDevices()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.UserDevices.AddRange(
            new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Windows Laptop",
                DeviceType = "Desktop",
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            },
            new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Android Phone",
                DeviceType = "Mobile",
                LastSeenAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-5),
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var devices = await _service.GetDevicesAsync(userId);

        // Assert
        Assert.AreEqual(2, devices.Count);
    }

    [TestMethod]
    public async Task WhenUserHasDevicesThenDevicesAreOrderedByLastSeenDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var older = DateTime.UtcNow.AddHours(-2);
        var newer = DateTime.UtcNow;

        _dbContext.UserDevices.AddRange(
            new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Old Device",
                DeviceType = "Desktop",
                LastSeenAt = older,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            },
            new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "New Device",
                DeviceType = "Mobile",
                LastSeenAt = newer,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var devices = await _service.GetDevicesAsync(userId);

        // Assert
        Assert.AreEqual("New Device", devices[0].Name);
        Assert.AreEqual("Old Device", devices[1].Name);
    }

    [TestMethod]
    public async Task WhenOtherUserHasDevicesThenOnlyCurrentUserDevicesReturned()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _dbContext.UserDevices.AddRange(
            new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "My Device",
                DeviceType = "Desktop",
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            },
            new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Other User Device",
                DeviceType = "Mobile",
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var devices = await _service.GetDevicesAsync(userId);

        // Assert
        Assert.AreEqual(1, devices.Count);
        Assert.AreEqual("My Device", devices[0].Name);
    }

    [TestMethod]
    public async Task WhenDeviceReturnedThenFieldsMappedCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var created = DateTime.UtcNow.AddDays(-3);
        var lastSeen = DateTime.UtcNow;

        _dbContext.UserDevices.Add(new UserDevice
        {
            Id = deviceId,
            UserId = userId,
            Name = "Test Device",
            DeviceType = "Tablet",
            LastSeenAt = lastSeen,
            CreatedAt = created,
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var devices = await _service.GetDevicesAsync(userId);

        // Assert
        Assert.AreEqual(1, devices.Count);
        var device = devices[0];
        Assert.AreEqual(deviceId, device.Id);
        Assert.AreEqual(userId, device.UserId);
        Assert.AreEqual("Test Device", device.Name);
        Assert.AreEqual("Tablet", device.DeviceType);
        Assert.AreEqual(created, device.RegisteredAt);
    }

    // ---------------------------------------------------------------------------
    // RemoveDeviceAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenDeviceExistsThenRemoveReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        _dbContext.UserDevices.Add(new UserDevice
        {
            Id = deviceId,
            UserId = userId,
            Name = "To Remove",
            DeviceType = "Desktop",
            LastSeenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RemoveDeviceAsync(userId, deviceId);

        // Assert
        Assert.IsTrue(result);
        var remaining = await _dbContext.UserDevices.CountAsync(d => d.UserId == userId);
        Assert.AreEqual(0, remaining);
    }

    [TestMethod]
    public async Task WhenDeviceDoesNotExistThenRemoveReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonExistentDeviceId = Guid.NewGuid();

        // Act
        var result = await _service.RemoveDeviceAsync(userId, nonExistentDeviceId);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WhenDeviceBelongsToOtherUserThenRemoveReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        _dbContext.UserDevices.Add(new UserDevice
        {
            Id = deviceId,
            UserId = otherUserId,
            Name = "Other User Device",
            DeviceType = "Desktop",
            LastSeenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RemoveDeviceAsync(userId, deviceId);

        // Assert
        Assert.IsFalse(result);
        var stillExists = await _dbContext.UserDevices.AnyAsync(d => d.Id == deviceId);
        Assert.IsTrue(stillExists);
    }

    [TestMethod]
    public async Task WhenRemovingDeviceThenOtherDevicesUnaffected()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceToRemove = Guid.NewGuid();
        var deviceToKeep = Guid.NewGuid();

        _dbContext.UserDevices.AddRange(
            new UserDevice
            {
                Id = deviceToRemove,
                UserId = userId,
                Name = "Remove Me",
                DeviceType = "Desktop",
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            },
            new UserDevice
            {
                Id = deviceToKeep,
                UserId = userId,
                Name = "Keep Me",
                DeviceType = "Mobile",
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.RemoveDeviceAsync(userId, deviceToRemove);

        // Assert
        var remaining = await _dbContext.UserDevices.Where(d => d.UserId == userId).ToListAsync();
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual("Keep Me", remaining[0].Name);
    }
}
