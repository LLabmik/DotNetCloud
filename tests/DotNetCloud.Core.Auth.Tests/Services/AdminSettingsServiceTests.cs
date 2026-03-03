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
/// Tests for <see cref="AdminSettingsService"/>.
/// </summary>
[TestClass]
public class AdminSettingsServiceTests
{
    private CoreDbContext _dbContext = null!;
    private Mock<ILogger<AdminSettingsService>> _loggerMock = null!;
    private AdminSettingsService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"SettingsTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        _loggerMock = new Mock<ILogger<AdminSettingsService>>();

        _service = new AdminSettingsService(_dbContext, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    // ---------------------------------------------------------------------------
    // ListSettingsAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenNoSettingsExistThenListSettingsReturnsEmptyList()
    {
        // Act
        var result = await _service.ListSettingsAsync();

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task WhenSettingsExistThenListSettingsReturnsAll()
    {
        // Arrange
        _dbContext.SystemSettings.AddRange(
            new SystemSetting { Module = "core", Key = "MaxUpload", Value = "5GB" },
            new SystemSetting { Module = "files", Key = "QuotaDefault", Value = "10GB" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListSettingsAsync();

        // Assert
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task WhenModuleFilterProvidedThenListSettingsFiltersCorrectly()
    {
        // Arrange
        _dbContext.SystemSettings.AddRange(
            new SystemSetting { Module = "core", Key = "Setting1", Value = "A" },
            new SystemSetting { Module = "core", Key = "Setting2", Value = "B" },
            new SystemSetting { Module = "files", Key = "Setting3", Value = "C" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListSettingsAsync("core");

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(s => s.Module == "core"));
    }

    // ---------------------------------------------------------------------------
    // GetSettingAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenSettingExistsThenGetSettingReturnsDto()
    {
        // Arrange
        _dbContext.SystemSettings.Add(
            new SystemSetting { Module = "core", Key = "MaxUpload", Value = "5GB", Description = "Max upload" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSettingAsync("core", "MaxUpload");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("core", result.Module);
        Assert.AreEqual("MaxUpload", result.Key);
        Assert.AreEqual("5GB", result.Value);
        Assert.AreEqual("Max upload", result.Description);
    }

    [TestMethod]
    public async Task WhenSettingDoesNotExistThenGetSettingReturnsNull()
    {
        // Act
        var result = await _service.GetSettingAsync("core", "Nonexistent");

        // Assert
        Assert.IsNull(result);
    }

    // ---------------------------------------------------------------------------
    // UpsertSettingAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenSettingDoesNotExistThenUpsertCreatesNewSetting()
    {
        // Arrange
        var dto = new UpsertSystemSettingDto { Value = "100MB", Description = "Upload limit" };

        // Act
        var result = await _service.UpsertSettingAsync("core", "UploadLimit", dto);

        // Assert
        Assert.AreEqual("core", result.Module);
        Assert.AreEqual("UploadLimit", result.Key);
        Assert.AreEqual("100MB", result.Value);

        var dbSetting = await _dbContext.SystemSettings.FirstOrDefaultAsync(
            s => s.Module == "core" && s.Key == "UploadLimit");
        Assert.IsNotNull(dbSetting);
    }

    [TestMethod]
    public async Task WhenSettingExistsThenUpsertUpdatesExistingSetting()
    {
        // Arrange
        _dbContext.SystemSettings.Add(
            new SystemSetting { Module = "core", Key = "Timeout", Value = "30", Description = "Old desc" });
        await _dbContext.SaveChangesAsync();

        var dto = new UpsertSystemSettingDto { Value = "60", Description = "New timeout" };

        // Act
        var result = await _service.UpsertSettingAsync("core", "Timeout", dto);

        // Assert
        Assert.AreEqual("60", result.Value);
        Assert.AreEqual("New timeout", result.Description);

        var count = await _dbContext.SystemSettings
            .CountAsync(s => s.Module == "core" && s.Key == "Timeout");
        Assert.AreEqual(1, count);
    }

    // ---------------------------------------------------------------------------
    // DeleteSettingAsync
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task WhenSettingExistsThenDeleteSettingReturnsTrue()
    {
        // Arrange
        _dbContext.SystemSettings.Add(
            new SystemSetting { Module = "core", Key = "ToDelete", Value = "val" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteSettingAsync("core", "ToDelete");

        // Assert
        Assert.IsTrue(result);

        var exists = await _dbContext.SystemSettings
            .AnyAsync(s => s.Module == "core" && s.Key == "ToDelete");
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task WhenSettingDoesNotExistThenDeleteSettingReturnsFalse()
    {
        // Act
        var result = await _service.DeleteSettingAsync("core", "Nonexistent");

        // Assert
        Assert.IsFalse(result);
    }
}
