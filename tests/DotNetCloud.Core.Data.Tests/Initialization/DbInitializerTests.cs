using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
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
        Assert.AreEqual(3, roles.Count, "Should seed 3 default org roles");
        Assert.IsTrue(roles.Any(r => r.Name == "Org Admin"), "Should include Org Admin role");
        Assert.IsTrue(roles.Any(r => r.Name == "Org Manager"), "Should include Org Manager role");
        Assert.IsTrue(roles.Any(r => r.Name == "Org Member"), "Should include Org Member role");
        Assert.IsTrue(roles.All(r => r.IsSystemRole), "All seeded roles should be system roles");

        // Assert - Permissions seeding is deferred to a future phase
        var permissions = await _context.Permissions.ToListAsync();
        Assert.AreEqual(0, permissions.Count, "Should not seed any permissions yet");

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

        var groups = await _context.Groups.ToListAsync();
        Assert.AreEqual(1, groups.Count, "Should seed one built-in group for the default organization");
        Assert.AreEqual(Group.AllUsersGroupName, groups[0].Name, "Built-in group should use the reserved All Users name");
        Assert.IsTrue(groups[0].IsAllUsersGroup, "Built-in group should be marked as the implicit all-users group");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        // Act - Initialize twice
        await _initializer.InitializeAsync();
        await _initializer.InitializeAsync();

        // Assert - Verify no duplicates
        var roles = await _context.Roles.ToListAsync();
        Assert.AreEqual(3, roles.Count, "Should still have exactly 3 org roles after second initialization");

        var permissions = await _context.Permissions.ToListAsync();
        Assert.AreEqual(0, permissions.Count, "Should not seed any permissions (deferred)");

        var settings = await _context.SystemSettings.ToListAsync();
        var settingsCount = settings.Count;
        Assert.IsTrue(settingsCount >= 20, "Should have seeded settings");

        var allUsersGroupCount = await _context.Groups.CountAsync(g => g.IsAllUsersGroup);
        Assert.AreEqual(1, allUsersGroupCount, "Initializer should remain idempotent for the built-in All Users group");

        // Verify no duplicate keys in settings
        var distinctSettingKeys = settings.Select(s => new { s.Module, s.Key }).Distinct().Count();
        Assert.AreEqual(settingsCount, distinctSettingKeys, "All setting keys should be unique per module");
    }

    [TestMethod]
    public async Task InitializeAsync_WithExistingOrganization_BackfillsBuiltInAllUsersGroup()
    {
        _context.Organizations.Add(new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Existing Org",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _initializer.InitializeAsync();

        var groups = await _context.Groups.Where(g => g.IsAllUsersGroup).ToListAsync();
        Assert.AreEqual(1, groups.Count, "Existing organizations should receive a built-in All Users group during initialization");
        Assert.AreEqual(Group.AllUsersGroupName, groups[0].Name);
    }

    [TestMethod]
    public async Task InitializeAsync_SeedsRolesWithCorrectProperties()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Org Admin");
        Assert.IsNotNull(adminRole, "Org Admin role should exist");
        Assert.IsTrue(adminRole.IsSystemRole, "Org Admin should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(adminRole.Description), "Org Admin should have a description");

        var managerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Org Manager");
        Assert.IsNotNull(managerRole, "Org Manager role should exist");
        Assert.IsTrue(managerRole.IsSystemRole, "Org Manager should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(managerRole.Description), "Org Manager should have a description");

        var memberRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Org Member");
        Assert.IsNotNull(memberRole, "Org Member role should exist");
        Assert.IsTrue(memberRole.IsSystemRole, "Org Member should be a system role");
        Assert.IsFalse(string.IsNullOrEmpty(memberRole.Description), "Org Member should have a description");
    }

    [TestMethod]
    public async Task InitializeAsync_PermissionsSeedingIsDeferred()
    {
        // Act
        await _initializer.InitializeAsync();

        // Assert — permissions seeding is deferred to a future phase
        var permissions = await _context.Permissions.ToListAsync();
        Assert.AreEqual(0, permissions.Count, "Should not seed permissions (deferred to future phase)");
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
    public async Task InitializeAsync_WithExistingData_StillSeedsWellKnownRoles()
    {
        // Arrange - Pre-populate with one custom role (not a well-known org role)
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

        // Assert - Pre-existing role preserved, well-known org roles also seeded
        var roles = await _context.Roles.ToListAsync();
        Assert.AreEqual(4, roles.Count, "Should preserve existing role and seed 3 well-known org roles");
        Assert.IsTrue(roles.Any(r => r.Name == "PreExisting"), "Pre-existing role should be preserved");
        Assert.IsTrue(roles.Any(r => r.Name == "Org Admin"), "Org Admin should be seeded");
        Assert.IsTrue(roles.Any(r => r.Name == "Org Manager"), "Org Manager should be seeded");
        Assert.IsTrue(roles.Any(r => r.Name == "Org Member"), "Org Member should be seeded");
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

        // Assert - Existing setting remains, and defaults are seeded without duplicates
        var settings = await _context.SystemSettings.ToListAsync();

        Assert.IsTrue(settings.Count > 1, "Should seed missing default settings");
        Assert.AreEqual(1, settings.Count(s => s.Module == "test.module" && s.Key == "TestKey"),
            "Existing setting should not be duplicated");
        Assert.IsTrue(settings.Any(s => s.Module == "dotnetcloud.core"),
            "Default core settings should be present");
    }
}
