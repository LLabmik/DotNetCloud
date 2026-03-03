using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Tests.Initialization;

/// <summary>
/// Integration tests for <see cref="DbInitializer"/>.
/// </summary>
[TestClass]
public class DbInitializerTests
{
    private CoreDbContext _context = null!;
    private DbInitializer _initializer = null!;
    private Mock<ILogger<DbInitializer>> _mockLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create in-memory database with unique name for each test
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        // Create naming strategy (PostgreSQL for testing)
        var namingStrategy = new PostgreSqlNamingStrategy();

        // Create context
        _context = new CoreDbContext(options, namingStrategy);

        // Create mock logger
        _mockLogger = new Mock<ILogger<DbInitializer>>();

        // Create initializer
        _initializer = new DbInitializer(_context, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
    }

    [TestMethod]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        try
        {
            _ = new DbInitializer(null!, _mockLogger.Object);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        try
        {
            _ = new DbInitializer(_context, null!);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task InitializeAsync_WithEmptyDatabase_SeedsAllDefaultData()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert - Verify roles were seeded
        var roles = await _context.Roles.ToListAsync();
        Assert.AreEqual(4, roles.Count, "Should seed 4 default roles");
        Assert.IsTrue(roles.Any(r => r.Name == "Administrator"), "Should include Administrator role");
        Assert.IsTrue(roles.Any(r => r.Name == "User"), "Should include User role");
        Assert.IsTrue(roles.Any(r => r.Name == "Guest"), "Should include Guest role");
        Assert.IsTrue(roles.Any(r => r.Name == "Moderator"), "Should include Moderator role");
        Assert.IsTrue(roles.All(r => r.IsSystemRole), "All seeded roles should be system roles");

        // Assert - Verify permissions were seeded
        var permissions = await _context.Permissions.ToListAsync();
        Assert.IsTrue(permissions.Count >= 40, $"Should seed at least 40 permissions, got {permissions.Count}");
        
        // Verify core permissions
        Assert.IsTrue(permissions.Any(p => p.Code == "core.admin"), "Should include core.admin permission");
        Assert.IsTrue(permissions.Any(p => p.Code == "core.users.view"), "Should include core.users.view permission");
        
        // Verify files permissions
        Assert.IsTrue(permissions.Any(p => p.Code == "files.upload"), "Should include files.upload permission");
        Assert.IsTrue(permissions.Any(p => p.Code == "files.download"), "Should include files.download permission");
        
        // Verify chat permissions
        Assert.IsTrue(permissions.Any(p => p.Code == "chat.send"), "Should include chat.send permission");
        Assert.IsTrue(permissions.Any(p => p.Code == "chat.read"), "Should include chat.read permission");

        // Assert - Verify system settings were seeded
        var settings = await _context.SystemSettings.ToListAsync();
        Assert.IsTrue(settings.Count >= 20, $"Should seed at least 20 system settings, got {settings.Count}");
        
        // Verify core settings
        var sessionTimeout = settings.FirstOrDefault(s => s.Module == "dotnetcloud.core" && s.Key == "SessionTimeout");
        Assert.IsNotNull(sessionTimeout, "Should include SessionTimeout setting");
        Assert.AreEqual("3600", sessionTimeout.Value, "SessionTimeout should be 3600 seconds");
        
        // Verify files settings
        var maxUploadSize = settings.FirstOrDefault(s => s.Module == "dotnetcloud.files" && s.Key == "MaxUploadSizeBytes");
        Assert.IsNotNull(maxUploadSize, "Should include MaxUploadSizeBytes setting");
        Assert.AreEqual("104857600", maxUploadSize.Value, "MaxUploadSizeBytes should be 100 MB");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        // Act - Initialize twice
        await _initializer.InitializeAsync();
        await _initializer.InitializeAsync();

        // Assert - Verify no duplicates
        var roles = await _context.Roles.ToListAsync();
        Assert.AreEqual(4, roles.Count, "Should still have exactly 4 roles after second initialization");

        var permissions = await _context.Permissions.ToListAsync();
        var permissionCount = permissions.Count;
        Assert.IsTrue(permissionCount >= 40, "Should have seeded permissions");

        var settings = await _context.SystemSettings.ToListAsync();
        var settingsCount = settings.Count;
        Assert.IsTrue(settingsCount >= 20, "Should have seeded settings");

        // Verify no duplicate codes/keys
        var distinctPermissionCodes = permissions.Select(p => p.Code).Distinct().Count();
        Assert.AreEqual(permissionCount, distinctPermissionCodes, "All permission codes should be unique");

        var distinctSettingKeys = settings.Select(s => new { s.Module, s.Key }).Distinct().Count();
        Assert.AreEqual(settingsCount, distinctSettingKeys, "All setting keys should be unique per module");
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsRolesWithCorrectProperties()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        Assert.IsNotNull(adminRole, "Administrator role should exist");
        Assert.IsTrue(adminRole.IsSystemRole, "Administrator should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(adminRole.Description), "Administrator should have a description");

        var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
        Assert.IsNotNull(userRole, "User role should exist");
        Assert.IsTrue(userRole.IsSystemRole, "User should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(userRole.Description), "User should have a description");

        var guestRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Guest");
        Assert.IsNotNull(guestRole, "Guest role should exist");
        Assert.IsTrue(guestRole.IsSystemRole, "Guest should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(guestRole.Description), "Guest should have a description");

        var moderatorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Moderator");
        Assert.IsNotNull(moderatorRole, "Moderator role should exist");
        Assert.IsTrue(moderatorRole.IsSystemRole, "Moderator should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(moderatorRole.Description), "Moderator should have a description");
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsPermissionsWithHierarchicalNaming()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var permissions = await _context.Permissions.ToListAsync();

        // Verify hierarchical naming convention (module.action)
        Assert.IsTrue(permissions.All(p => p.Code.Contains('.')), 
            "All permission codes should follow hierarchical naming with dots");

        // Verify core module permissions
        var corePermissions = permissions.Where(p => p.Code.StartsWith("core.")).ToList();
        Assert.IsTrue(corePermissions.Count >= 10, $"Should have at least 10 core permissions, got {corePermissions.Count}");

        // Verify files module permissions
        var filesPermissions = permissions.Where(p => p.Code.StartsWith("files.")).ToList();
        Assert.IsTrue(filesPermissions.Count >= 5, $"Should have at least 5 files permissions, got {filesPermissions.Count}");

        // Verify chat module permissions
        var chatPermissions = permissions.Where(p => p.Code.StartsWith("chat.")).ToList();
        Assert.IsTrue(chatPermissions.Count >= 5, $"Should have at least 5 chat permissions, got {chatPermissions.Count}");

        // Verify all permissions have display names and descriptions
        Assert.IsTrue(permissions.All(p => !string.IsNullOrEmpty(p.DisplayName)),
            "All permissions should have display names");
        Assert.IsTrue(permissions.All(p => !string.IsNullOrEmpty(p.Description)),
            "All permissions should have descriptions");
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsSystemSettingsForMultipleModules()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var settings = await _context.SystemSettings.ToListAsync();

        // Verify settings for different modules exist
        var coreSettings = settings.Where(s => s.Module == "dotnetcloud.core").ToList();
        Assert.IsTrue(coreSettings.Count >= 5, $"Should have at least 5 core settings, got {coreSettings.Count}");

        var filesSettings = settings.Where(s => s.Module == "dotnetcloud.files").ToList();
        Assert.IsTrue(filesSettings.Count >= 3, $"Should have at least 3 files settings, got {filesSettings.Count}");

        var notificationSettings = settings.Where(s => s.Module == "dotnetcloud.notifications").ToList();
        Assert.IsTrue(notificationSettings.Count >= 2, $"Should have at least 2 notification settings, got {notificationSettings.Count}");

        var securitySettings = settings.Where(s => s.Module == "dotnetcloud.security").ToList();
        Assert.IsTrue(securitySettings.Count >= 2, $"Should have at least 2 security settings, got {securitySettings.Count}");

        // Verify all settings have descriptions
        Assert.IsTrue(settings.All(s => !string.IsNullOrEmpty(s.Description)),
            "All settings should have descriptions");
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsPasswordPolicySettings()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var passwordMinLength = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.core" && s.Key == "PasswordMinLength");
        Assert.IsNotNull(passwordMinLength, "PasswordMinLength setting should exist");
        Assert.AreEqual("8", passwordMinLength.Value);

        var passwordRequireUppercase = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.core" && s.Key == "PasswordRequireUppercase");
        Assert.IsNotNull(passwordRequireUppercase, "PasswordRequireUppercase setting should exist");
        Assert.AreEqual("true", passwordRequireUppercase.Value);

        var passwordRequireDigit = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.core" && s.Key == "PasswordRequireDigit");
        Assert.IsNotNull(passwordRequireDigit, "PasswordRequireDigit setting should exist");
        Assert.AreEqual("true", passwordRequireDigit.Value);

        var maxLoginAttempts = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.core" && s.Key == "MaxLoginAttempts");
        Assert.IsNotNull(maxLoginAttempts, "MaxLoginAttempts setting should exist");
        Assert.AreEqual("5", maxLoginAttempts.Value);
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsFileStorageSettings()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var maxUploadSize = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.files" && s.Key == "MaxUploadSizeBytes");
        Assert.IsNotNull(maxUploadSize, "MaxUploadSizeBytes setting should exist");
        Assert.AreEqual("104857600", maxUploadSize.Value); // 100 MB

        var enableVersioning = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.files" && s.Key == "EnableVersioning");
        Assert.IsNotNull(enableVersioning, "EnableVersioning setting should exist");
        Assert.AreEqual("true", enableVersioning.Value);

        var defaultQuota = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.files" && s.Key == "DefaultQuotaGB");
        Assert.IsNotNull(defaultQuota, "DefaultQuotaGB setting should exist");
        Assert.AreEqual("10", defaultQuota.Value);
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsSecuritySettings()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var enableTwoFactor = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.security" && s.Key == "EnableTwoFactor");
        Assert.IsNotNull(enableTwoFactor, "EnableTwoFactor setting should exist");
        Assert.AreEqual("true", enableTwoFactor.Value);

        var requireTwoFactorForAdmins = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.security" && s.Key == "RequireTwoFactorForAdmins");
        Assert.IsNotNull(requireTwoFactorForAdmins, "RequireTwoFactorForAdmins setting should exist");
        Assert.AreEqual("true", requireTwoFactorForAdmins.Value);

        var enableWebAuthn = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Module == "dotnetcloud.security" && s.Key == "EnableWebAuthn");
        Assert.IsNotNull(enableWebAuthn, "EnableWebAuthn setting should exist");
        Assert.AreEqual("true", enableWebAuthn.Value);
    }

    [TestMethod]
    public async Task InitializeAsync_LogsInitializationSteps()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting database initialization")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database initialization completed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InitializeAsync_WithExistingData_SkipsSeeding()
    {
        // Arrange - Pre-populate with one role
        var existingRole = new DotNetCloud.Core.Data.Entities.Permissions.Role
        {
            Id = Guid.NewGuid(),
            Name = "PreExisting",
            Description = "Pre-existing role",
            IsSystemRole = false
        };
        _context.Roles.Add(existingRole);
        await _context.SaveChangesAsync();

        // Act
        await _initializer.InitializeAsync();

        // Assert - Verify only the pre-existing role exists (no seeding occurred)
        var roles = await _context.Roles.ToListAsync();
        Assert.AreEqual(1, roles.Count, "Should not seed roles when data already exists");
        Assert.AreEqual("PreExisting", roles[0].Name);
    }

    [TestMethod]
    public async Task InitializeAsync_WithExistingPermissions_SkipsPermissionSeeding()
    {
        // Arrange - Pre-populate with one permission
        var existingPermission = new DotNetCloud.Core.Data.Entities.Permissions.Permission
        {
            Id = Guid.NewGuid(),
            Code = "test.permission",
            DisplayName = "Test Permission",
            Description = "Pre-existing permission"
        };
        _context.Permissions.Add(existingPermission);
        await _context.SaveChangesAsync();

        // Act
        await _initializer.InitializeAsync();

        // Assert - Verify only the pre-existing permission exists (no seeding occurred)
        var permissions = await _context.Permissions.ToListAsync();
        Assert.AreEqual(1, permissions.Count, "Should not seed permissions when data already exists");
        Assert.AreEqual("test.permission", permissions[0].Code);
    }

    [TestMethod]
    public async Task InitializeAsync_WithExistingSettings_SkipsSettingsSeeding()
    {
        // Arrange - Pre-populate with one setting
        var existingSetting = new DotNetCloud.Core.Data.Entities.Settings.SystemSetting
        {
            Module = "test.module",
            Key = "TestKey",
            Value = "TestValue",
            Description = "Pre-existing setting"
        };
        _context.SystemSettings.Add(existingSetting);
        await _context.SaveChangesAsync();

        // Act
        await _initializer.InitializeAsync();

        // Assert - Verify only the pre-existing setting exists (no seeding occurred)
        var settings = await _context.SystemSettings.ToListAsync();
        Assert.AreEqual(1, settings.Count, "Should not seed settings when data already exists");
        Assert.AreEqual("test.module", settings[0].Module);
    }
}
