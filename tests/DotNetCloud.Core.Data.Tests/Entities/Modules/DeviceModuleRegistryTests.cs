using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Entities.Modules;

/// <summary>
/// Tests for device and module registry relationships.
/// </summary>
[TestClass]
public class DeviceModuleRegistryTests
{
    private CoreDbContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"DeviceModuleTestDb_{Guid.NewGuid()}")
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
    public async Task UserDevice_ToUser_HasManyToOneRelationship()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "deviceuser",
            DisplayName = "Device User"
        };
        var device = new UserDevice
        {
            User = user,
            Name = "Windows Laptop",
            DeviceType = "Desktop",
            LastSeenAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var retrievedDevice = await _context.UserDevices
            .Include(d => d.User)
            .FirstAsync(d => d.Id == device.Id);

        // Assert
        Assert.IsNotNull(retrievedDevice.User, "Device should reference User");
        Assert.AreEqual(user.Id, retrievedDevice.UserId, "Device should reference correct user");
        Assert.AreEqual("Device User", retrievedDevice.User.DisplayName, "User data should be accessible");
    }

    [TestMethod]
    public async Task User_ToDevices_HasOneToManyRelationship()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "multidevice",
            DisplayName = "Multi Device User"
        };
        var device1 = new UserDevice
        {
            User = user,
            Name = "Windows Laptop",
            DeviceType = "Desktop",
            LastSeenAt = DateTime.UtcNow
        };
        var device2 = new UserDevice
        {
            User = user,
            Name = "iPhone",
            DeviceType = "Mobile",
            LastSeenAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.UserDevices.AddRange(device1, device2);
        await _context.SaveChangesAsync();

        // Act
        var userDevices = await _context.UserDevices
            .Where(d => d.UserId == user.Id)
            .ToListAsync();

        // Assert
        Assert.AreEqual(2, userDevices.Count, "User should have 2 devices");
        Assert.IsTrue(userDevices.All(d => d.UserId == user.Id), "All devices should reference the user");
    }

    [TestMethod]
    public async Task UserDevice_LastSeenAt_TrackPresence()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "presenceuser",
            DisplayName = "Presence User"
        };
        var device = new UserDevice
        {
            User = user,
            Name = "Test Device",
            DeviceType = "Desktop",
            LastSeenAt = DateTime.UtcNow.AddHours(-2)
        };

        _context.Users.Add(user);
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act - Update last seen
        await Task.Delay(100);
        var newLastSeen = DateTime.UtcNow;
        device.LastSeenAt = newLastSeen;
        _context.UserDevices.Update(device);
        await _context.SaveChangesAsync();

        var retrieved = await _context.UserDevices.FirstAsync(d => d.Id == device.Id);

        // Assert
        Assert.IsTrue(retrieved.LastSeenAt > DateTime.UtcNow.AddHours(-1), "LastSeenAt should be recently updated");
    }

    [TestMethod]
    public async Task InstalledModule_HasValidStatus()
    {
        // Arrange
        var statuses = new[] { "Enabled", "Disabled", "UpdateAvailable", "Failed", "Installing", "Uninstalling", "Updating" };
        
        foreach (var status in statuses)
        {
            var module = new InstalledModule
            {
                ModuleId = $"dotnetcloud.{status.ToLower()}",
                Version = "1.0.0",
                Status = status,
                InstalledAt = DateTime.UtcNow
            };

            _context.InstalledModules.Add(module);
        }
        await _context.SaveChangesAsync();

        // Act
        var modules = await _context.InstalledModules.ToListAsync();

        // Assert
        Assert.AreEqual(statuses.Length, modules.Count, $"Should have all {statuses.Length} module statuses");
        foreach (var status in statuses)
        {
            Assert.IsTrue(modules.Any(m => m.Status == status), $"Should have module with status: {status}");
        }
    }

    [TestMethod]
    public async Task InstalledModule_Version_UsesSemanticVersioning()
    {
        // Arrange
        var versions = new[] { "1.0.0", "1.1.0", "2.0.0", "1.0.1", "1.0.0-beta", "1.0.0-rc1" };
        
        foreach (var (version, index) in versions.Select((v, i) => (v, i)))
        {
            var module = new InstalledModule
            {
                ModuleId = $"dotnetcloud.versiontest{index}",
                Version = version,
                Status = "Enabled",
                InstalledAt = DateTime.UtcNow
            };
            _context.InstalledModules.Add(module);
        }
        await _context.SaveChangesAsync();

        // Act
        var modules = await _context.InstalledModules.ToListAsync();

        // Assert
        Assert.AreEqual(versions.Length, modules.Count, "All versions should be stored");
        Assert.IsTrue(modules.Any(m => m.Version == "2.0.0"), "Should store major versions");
        Assert.IsTrue(modules.Any(m => m.Version.Contains("beta")), "Should store pre-release versions");
    }

    [TestMethod]
    public async Task ModuleCapabilityGrant_ToModule_HasManyToOneRelationship()
    {
        // Arrange
        var module = new InstalledModule
        {
            ModuleId = "dotnetcloud.files",
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow
        };
        var grant = new ModuleCapabilityGrant
        {
            Module = module,
            CapabilityName = "IStorageProvider",
            GrantedAt = DateTime.UtcNow
        };

        _context.InstalledModules.Add(module);
        _context.ModuleCapabilityGrants.Add(grant);
        await _context.SaveChangesAsync();

        // Act
        var retrievedGrant = await _context.ModuleCapabilityGrants
            .Include(g => g.Module)
            .FirstAsync(g => g.Id == grant.Id);

        // Assert
        Assert.IsNotNull(retrievedGrant.Module, "Grant should reference Module");
        Assert.AreEqual("dotnetcloud.files", retrievedGrant.Module.ModuleId, "Grant should reference correct module");
    }

    [TestMethod]
    public async Task InstalledModule_ToCapabilityGrants_HasOneToManyRelationship()
    {
        // Arrange
        var module = new InstalledModule
        {
            ModuleId = "dotnetcloud.files",
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow
        };
        var grant1 = new ModuleCapabilityGrant
        {
            Module = module,
            CapabilityName = "IStorageProvider",
            GrantedAt = DateTime.UtcNow
        };
        var grant2 = new ModuleCapabilityGrant
        {
            Module = module,
            CapabilityName = "IFileIndexer",
            GrantedAt = DateTime.UtcNow
        };

        _context.InstalledModules.Add(module);
        _context.ModuleCapabilityGrants.AddRange(grant1, grant2);
        await _context.SaveChangesAsync();

        // Act
        var moduleWithGrants = await _context.InstalledModules
            .Include(m => m.CapabilityGrants)
            .FirstAsync(m => m.ModuleId == "dotnetcloud.files");

        // Assert
        Assert.AreEqual(2, moduleWithGrants.CapabilityGrants.Count, "Module should have 2 capability grants");
        Assert.IsTrue(moduleWithGrants.CapabilityGrants.Any(g => g.CapabilityName == "IStorageProvider"));
        Assert.IsTrue(moduleWithGrants.CapabilityGrants.Any(g => g.CapabilityName == "IFileIndexer"));
    }

    [TestMethod]
    public async Task ModuleCapabilityGrant_GrantedByUser_TrackingAudit()
    {
        // Arrange
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
            DisplayName = "System Admin"
        };
        var module = new InstalledModule
        {
            ModuleId = "dotnetcloud.test",
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow
        };
        var grant = new ModuleCapabilityGrant
        {
            Module = module,
            CapabilityName = "ITestCapability",
            GrantedAt = DateTime.UtcNow,
            GrantedByUser = admin
        };

        _context.Users.Add(admin);
        _context.InstalledModules.Add(module);
        _context.ModuleCapabilityGrants.Add(grant);
        await _context.SaveChangesAsync();

        // Act
        var retrievedGrant = await _context.ModuleCapabilityGrants
            .Include(g => g.GrantedByUser)
            .FirstAsync();

        // Assert
        Assert.IsNotNull(retrievedGrant.GrantedByUser, "Grant should track who granted the capability");
        Assert.AreEqual("System Admin", retrievedGrant.GrantedByUser.DisplayName, "Admin information should be preserved");
    }

    [TestMethod]
    public void ModuleCapabilityGrant_UniqueConstraint_OneCapabilityPerModule()
    {
        // Verify the model configuration has a unique index on (ModuleId, CapabilityName)
        var entityType = _context.Model.FindEntityType(typeof(ModuleCapabilityGrant))!;
        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.IsUnique &&
                i.Properties.Any(p => p.Name == nameof(ModuleCapabilityGrant.ModuleId)) &&
                i.Properties.Any(p => p.Name == nameof(ModuleCapabilityGrant.CapabilityName)));

        Assert.IsNotNull(uniqueIndex, "ModuleCapabilityGrant should have a unique constraint on (ModuleId, CapabilityName)");
        Assert.IsTrue(uniqueIndex.IsUnique, "Constraint should be unique");
    }

    [TestMethod]
    public async Task InstalledModule_PreservesInstallationDate()
    {
        // Arrange
        var installDate = DateTime.UtcNow.AddMonths(-3);
        var module = new InstalledModule
        {
            ModuleId = "dotnetcloud.oldmodule",
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = installDate
        };

        _context.InstalledModules.Add(module);
        await _context.SaveChangesAsync();

        // Act - Update version
        module.Version = "1.1.0";
        _context.InstalledModules.Update(module);
        await _context.SaveChangesAsync();

        var retrieved = await _context.InstalledModules.FirstAsync();

        // Assert
        Assert.AreEqual(installDate, retrieved.InstalledAt, "Installation date should be immutable");
        Assert.IsTrue(retrieved.UpdatedAt >= DateTime.UtcNow.AddSeconds(-5), "UpdatedAt should reflect recent modification");
    }

    [TestMethod]
    public async Task CascadeDelete_InstalledModule_DeletesCapabilityGrants()
    {
        // Arrange
        var module = new InstalledModule
        {
            ModuleId = "dotnetcloud.delete",
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow
        };
        var grant = new ModuleCapabilityGrant
        {
            Module = module,
            CapabilityName = "IDeleteCapability",
            GrantedAt = DateTime.UtcNow
        };

        _context.InstalledModules.Add(module);
        _context.ModuleCapabilityGrants.Add(grant);
        await _context.SaveChangesAsync();

        // Act - Delete module
        _context.InstalledModules.Remove(module);
        await _context.SaveChangesAsync();

        // Assert
        var orphanedGrants = await _context.ModuleCapabilityGrants
            .Where(g => g.ModuleId == "dotnetcloud.delete")
            .ToListAsync();
        Assert.AreEqual(0, orphanedGrants.Count, "Capability grants should be deleted with module");
    }

    [TestMethod]
    public void CascadeDelete_User_PreservesModuleCapabilityGrantAuditTrail()
    {
        // Verify the FK from ModuleCapabilityGrant to ApplicationUser uses Restrict delete
        // to preserve the audit trail (who granted the capability)
        var grantEntityType = _context.Model.FindEntityType(typeof(ModuleCapabilityGrant))!;
        var userFK = grantEntityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser));

        Assert.IsNotNull(userFK, "ModuleCapabilityGrant should have a FK to ApplicationUser");
        Assert.AreEqual(DeleteBehavior.Restrict, userFK.DeleteBehavior,
            "GrantedByUser FK should use Restrict delete to preserve audit trail");
    }
}
