using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Entities.Settings;

/// <summary>
/// Tests for settings hierarchy (System → Organization → User).
/// </summary>
[TestClass]
public class SettingsHierarchyTests
{
    private CoreDbContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"SettingsTestDb_{Guid.NewGuid()}")
            .Options;

        var namingStrategy = new PostgreSqlNamingStrategy();
        _context = new CoreDbContext(options, namingStrategy);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
    }

    [TestMethod]
    public async Task SystemSetting_CompositeKey_IsModuleAndKey()
    {
        // Arrange
        var setting = new SystemSetting
        {
            Module = "core",
            Key = "SessionTimeout",
            Value = "3600",
            Description = "Session timeout in seconds"
        };

        _context.SystemSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var retrieved = await _context.SystemSettings
            .FirstAsync(s => s.Module == "core" && s.Key == "SessionTimeout");

        // Assert
        Assert.IsNotNull(retrieved, "Setting should be retrievable by composite key");
        Assert.AreEqual("core", retrieved.Module, "Module should match");
        Assert.AreEqual("SessionTimeout", retrieved.Key, "Key should match");
        Assert.AreEqual("3600", retrieved.Value, "Value should be correct");
    }

    [TestMethod]
    public async Task OrganizationSetting_OverridesSystemSetting()
    {
        // Arrange
        var systemSetting = new SystemSetting
        {
            Module = "files",
            Key = "MaxUploadSize",
            Value = "104857600" // 100MB
        };

        var org = new Organization { Name = "Small Org" };
        var orgSetting = new OrganizationSetting
        {
            OrganizationId = org.Id,
            Module = "files",
            Key = "MaxUploadSize",
            Value = "52428800" // 50MB (override)
        };

        _context.SystemSettings.Add(systemSetting);
        _context.Organizations.Add(org);
        _context.OrganizationSettings.Add(orgSetting);
        await _context.SaveChangesAsync();

        // Act
        var sysValue = await _context.SystemSettings
            .Where(s => s.Module == "files" && s.Key == "MaxUploadSize")
            .Select(s => s.Value)
            .FirstAsync();

        var orgValue = await _context.OrganizationSettings
            .Where(s => s.OrganizationId == org.Id && s.Module == "files" && s.Key == "MaxUploadSize")
            .Select(s => s.Value)
            .FirstAsync();

        // Assert
        Assert.AreEqual("104857600", sysValue, "System setting should retain original value");
        Assert.AreEqual("52428800", orgValue, "Organization setting should override with different value");
    }

    [TestMethod]
    public async Task UserSetting_OverridesOrganizationAndSystemSettings()
    {
        // Arrange
        var systemSetting = new SystemSetting
        {
            Module = "notifications",
            Key = "Timezone",
            Value = "UTC"
        };

        var org = new Organization { Name = "Test Org" };
        var orgSetting = new OrganizationSetting
        {
            OrganizationId = org.Id,
            Module = "notifications",
            Key = "Timezone",
            Value = "America/New_York"
        };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            DisplayName = "Test User"
        };
        var userSetting = new UserSetting
        {
            UserId = user.Id,
            Module = "notifications",
            Key = "Timezone",
            Value = "Europe/London"
        };

        _context.SystemSettings.Add(systemSetting);
        _context.Organizations.Add(org);
        _context.OrganizationSettings.Add(orgSetting);
        _context.Users.Add(user);
        _context.UserSettings.Add(userSetting);
        await _context.SaveChangesAsync();

        // Act
        var sysValue = await _context.SystemSettings
            .Where(s => s.Module == "notifications" && s.Key == "Timezone")
            .Select(s => s.Value)
            .FirstAsync();

        var orgValue = await _context.OrganizationSettings
            .Where(s => s.Module == "notifications" && s.Key == "Timezone")
            .Select(s => s.Value)
            .FirstAsync();

        var userValue = await _context.UserSettings
            .Where(s => s.Module == "notifications" && s.Key == "Timezone")
            .Select(s => s.Value)
            .FirstAsync();

        // Assert
        Assert.AreEqual("UTC", sysValue, "System setting should have default");
        Assert.AreEqual("America/New_York", orgValue, "Organization setting should override system");
        Assert.AreEqual("Europe/London", userValue, "User setting should override both");
    }

    [TestMethod]
    public async Task OrganizationSetting_UniqueConstraint_PreventsMultipleSettingsPerKey()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var setting1 = new OrganizationSetting
        {
            OrganizationId = org.Id,
            Module = "test",
            Key = "TestKey",
            Value = "Value1"
        };
        var setting2 = new OrganizationSetting
        {
            OrganizationId = org.Id,
            Module = "test",
            Key = "TestKey",
            Value = "Value2"
        };

        _context.Organizations.Add(org);
        _context.OrganizationSettings.Add(setting1);
        await _context.SaveChangesAsync();

        // Act & Assert
        _context.OrganizationSettings.Add(setting2);
        try
        {
            await _context.SaveChangesAsync();
            Assert.Fail("Should have thrown DbUpdateException for duplicate setting keys");
        }
        catch (DbUpdateException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task UserSetting_IsEncrypted_Flag_IndicatesSensitiveData()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            DisplayName = "Test User"
        };

        var encryptedSetting = new UserSetting
        {
            UserId = user.Id,
            Module = "security",
            Key = "ApiKey",
            Value = "encrypted_value",
            IsEncrypted = true
        };

        var plainSetting = new UserSetting
        {
            UserId = user.Id,
            Module = "ui",
            Key = "Theme",
            Value = "dark",
            IsEncrypted = false
        };

        _context.Users.Add(user);
        _context.UserSettings.AddRange(encryptedSetting, plainSetting);
        await _context.SaveChangesAsync();

        // Act
        var encrypted = await _context.UserSettings
            .Where(s => s.IsEncrypted)
            .ToListAsync();

        var plain = await _context.UserSettings
            .Where(s => !s.IsEncrypted)
            .ToListAsync();

        // Assert
        Assert.AreEqual(1, encrypted.Count, "Should find 1 encrypted setting");
        Assert.AreEqual(1, plain.Count, "Should find 1 plain setting");
        Assert.IsTrue(encrypted[0].IsEncrypted, "Encrypted setting should be marked");
        Assert.IsFalse(plain[0].IsEncrypted, "Plain setting should not be marked");
    }

    [TestMethod]
    public async Task SystemSetting_UpdatedAt_IsSetAndUpdated()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        var setting = new SystemSetting
        {
            Module = "test",
            Key = "UpdateTest",
            Value = "Original"
        };

        _context.SystemSettings.Add(setting);
        await _context.SaveChangesAsync();
        var afterCreation = DateTime.UtcNow;

        // Assert creation timestamp
        Assert.IsTrue(setting.UpdatedAt >= beforeCreation && setting.UpdatedAt <= afterCreation,
            "UpdatedAt should be set on creation");

        // Act - Update the setting
        var delayMs = 100;
        await Task.Delay(delayMs);
        var beforeUpdate = DateTime.UtcNow;
        
        setting.Value = "Updated";
        _context.SystemSettings.Update(setting);
        await _context.SaveChangesAsync();

        var afterUpdate = DateTime.UtcNow;

        // Assert update timestamp
        Assert.IsTrue(setting.UpdatedAt >= beforeUpdate && setting.UpdatedAt <= afterUpdate,
            "UpdatedAt should be updated on modification");
    }

    [TestMethod]
    public async Task CascadeDelete_Organization_DeletesOrganizationSettings()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var orgSetting = new OrganizationSetting
        {
            OrganizationId = org.Id,
            Module = "test",
            Key = "TestKey",
            Value = "TestValue"
        };

        _context.Organizations.Add(org);
        _context.OrganizationSettings.Add(orgSetting);
        await _context.SaveChangesAsync();

        var orgId = org.Id;

        // Act - Delete organization
        _context.Organizations.Remove(org);
        await _context.SaveChangesAsync();

        // Assert
        var orphanedSettings = await _context.OrganizationSettings
            .Where(s => s.OrganizationId == orgId)
            .ToListAsync();
        Assert.AreEqual(0, orphanedSettings.Count, "OrganizationSettings should be deleted with organization");
    }

    [TestMethod]
    public async Task CascadeDelete_User_DeletesUserSettings()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "deletetest",
            DisplayName = "Delete Test"
        };
        var userSetting = new UserSetting
        {
            UserId = user.Id,
            Module = "test",
            Key = "TestKey",
            Value = "TestValue"
        };

        _context.Users.Add(user);
        _context.UserSettings.Add(userSetting);
        await _context.SaveChangesAsync();

        var userId = user.Id;

        // Act - Delete user
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        // Assert
        var orphanedSettings = await _context.UserSettings
            .Where(s => s.UserId == userId)
            .ToListAsync();
        Assert.AreEqual(0, orphanedSettings.Count, "UserSettings should be deleted with user");
    }

    [TestMethod]
    public async Task SettingsHierarchy_MultipleModules_KeepsSeparateSettings()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "multimodule",
            DisplayName = "Multi Module"
        };

        var settings = new List<UserSetting>
        {
            new UserSetting { UserId = user.Id, Module = "files", Key = "DefaultView", Value = "grid" },
            new UserSetting { UserId = user.Id, Module = "chat", Key = "Notifications", Value = "enabled" },
            new UserSetting { UserId = user.Id, Module = "calendar", Key = "TimeFormat", Value = "24h" }
        };

        _context.Users.Add(user);
        _context.UserSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var fileSettings = await _context.UserSettings
            .Where(s => s.UserId == user.Id && s.Module == "files")
            .ToListAsync();

        var chatSettings = await _context.UserSettings
            .Where(s => s.UserId == user.Id && s.Module == "chat")
            .ToListAsync();

        var allSettings = await _context.UserSettings
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        // Assert
        Assert.AreEqual(1, fileSettings.Count, "Should find 1 files setting");
        Assert.AreEqual(1, chatSettings.Count, "Should find 1 chat setting");
        Assert.AreEqual(3, allSettings.Count, "Should find 3 total settings");
        Assert.IsTrue(allSettings.Select(s => s.Module).Distinct().Count() == 3, "Should have settings for 3 different modules");
    }
}
