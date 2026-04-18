using DotNetCloud.Core.Auth.Services;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Services;

/// <summary>
/// Tests for <see cref="UserSettingsService"/>.
/// </summary>
[TestClass]
public class UserSettingsServiceTests
{
    private CoreDbContext _dbContext = null!;
    private Mock<ILogger<UserSettingsService>> _loggerMock = null!;
    private UserSettingsService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"UserSettingsTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        _loggerMock = new Mock<ILogger<UserSettingsService>>();

        _service = new UserSettingsService(_dbContext, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task WhenSettingExistsForUserThenGetSettingReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.UserSettings.Add(
            new UserSetting
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "true",
                Description = "Navbar state",
                IsEncrypted = false,
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("dotnetcloud.ui", result.Module);
        Assert.AreEqual("navbar.collapsed", result.Key);
        Assert.AreEqual("true", result.Value);
    }

    [TestMethod]
    public async Task WhenSettingDoesNotExistForUserThenGetSettingReturnsNull()
    {
        // Act
        var result = await _service.GetSettingAsync(Guid.NewGuid(), "dotnetcloud.ui", "navbar.collapsed");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenSettingDoesNotExistThenUpsertCreatesNewSetting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new UpsertUserSettingDto
        {
            Value = "true",
            Description = "Navbar state",
            IsSensitive = false,
        };

        // Act
        var result = await _service.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("true", result.Value);

        var dbSetting = await _dbContext.UserSettings.FirstOrDefaultAsync(
            s => s.UserId == userId && s.Module == "dotnetcloud.ui" && s.Key == "navbar.collapsed");
        Assert.IsNotNull(dbSetting);
        Assert.AreEqual("true", dbSetting.Value);
    }

    [TestMethod]
    public async Task WhenSettingExistsThenUpsertUpdatesExistingSetting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.UserSettings.Add(
            new UserSetting
            {
                UserId = userId,
                Module = "dotnetcloud.ui",
                Key = "navbar.collapsed",
                Value = "false",
                Description = "Old",
                IsEncrypted = false,
            });
        await _dbContext.SaveChangesAsync();

        var dto = new UpsertUserSettingDto
        {
            Value = "true",
            Description = "Updated",
            IsSensitive = false,
        };

        // Act
        var result = await _service.UpsertSettingAsync(userId, "dotnetcloud.ui", "navbar.collapsed", dto);

        // Assert
        Assert.AreEqual("true", result.Value);
        Assert.AreEqual("Updated", result.Description);

        var count = await _dbContext.UserSettings.CountAsync(
            s => s.UserId == userId && s.Module == "dotnetcloud.ui" && s.Key == "navbar.collapsed");
        Assert.AreEqual(1, count);
    }
}
