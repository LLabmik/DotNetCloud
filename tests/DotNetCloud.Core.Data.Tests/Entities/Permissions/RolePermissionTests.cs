using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Permissions;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Entities.Permissions;

/// <summary>
/// Tests for permission and role relationships.
/// </summary>
[TestClass]
public class RolePermissionTests
{
    private CoreDbContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"PermissionTestDb_{Guid.NewGuid()}")
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
    public async Task Role_ToPermissions_HasManyToManyRelationship()
    {
        // Arrange
        var role = new Role { Name = "Admin", IsSystemRole = true };
        var perm1 = new Permission { Code = "core.admin", DisplayName = "Core Admin" };
        var perm2 = new Permission { Code = "core.users.manage", DisplayName = "Manage Users" };

        var rolePermission1 = new RolePermission { Role = role, Permission = perm1 };
        var rolePermission2 = new RolePermission { Role = role, Permission = perm2 };

        _context.Roles.Add(role);
        _context.Permissions.AddRange(perm1, perm2);
        _context.RolePermissions.AddRange(rolePermission1, rolePermission2);
        await _context.SaveChangesAsync();

        // Act
        var retrievedRole = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == role.Id);

        // Assert
        Assert.IsNotNull(retrievedRole.RolePermissions, "Role should have RolePermissions collection");
        Assert.AreEqual(2, retrievedRole.RolePermissions.Count, "Role should have 2 permissions");
        Assert.IsTrue(retrievedRole.RolePermissions.All(rp => rp.RoleId == role.Id), "All role permissions should reference this role");
    }

    [TestMethod]
    public async Task Permission_ToRoles_HasManyToManyRelationship()
    {
        // Arrange
        var role1 = new Role { Name = "Admin", IsSystemRole = true };
        var role2 = new Role { Name = "User", IsSystemRole = true };
        var permission = new Permission { Code = "core.view", DisplayName = "View Core" };

        var rolePermission1 = new RolePermission { Role = role1, Permission = permission };
        var rolePermission2 = new RolePermission { Role = role2, Permission = permission };

        _context.Roles.AddRange(role1, role2);
        _context.Permissions.Add(permission);
        _context.RolePermissions.AddRange(rolePermission1, rolePermission2);
        await _context.SaveChangesAsync();

        // Act
        var retrievedPermission = await _context.Permissions
            .Include(p => p.RolePermissions)
            .ThenInclude(rp => rp.Role)
            .FirstAsync(p => p.Id == permission.Id);

        // Assert
        Assert.AreEqual(2, retrievedPermission.RolePermissions.Count, "Permission should be assigned to 2 roles");
        Assert.IsTrue(retrievedPermission.RolePermissions.Any(rp => rp.Role.Name == "Admin"), "Permission should be in Admin role");
        Assert.IsTrue(retrievedPermission.RolePermissions.Any(rp => rp.Role.Name == "User"), "Permission should be in User role");
    }

    [TestMethod]
    public async Task RolePermission_CompositeKey_IdentifiesJunction()
    {
        // Arrange
        var role = new Role { Name = "Moderator", IsSystemRole = true };
        var permission = new Permission { Code = "content.moderate", DisplayName = "Moderate Content" };
        var rolePermission = new RolePermission { Role = role, Permission = permission };

        _context.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act
        var retrieved = await _context.RolePermissions
            .FirstAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);

        // Assert
        Assert.IsNotNull(retrieved, "RolePermission should be retrievable by composite key");
        Assert.AreEqual(role.Id, retrieved.RoleId, "RoleId should match");
        Assert.AreEqual(permission.Id, retrieved.PermissionId, "PermissionId should match");
    }

    [TestMethod]
    public async Task Permission_Code_IsUnique()
    {
        // Arrange
        var perm1 = new Permission { Code = "core.admin", DisplayName = "Core Admin" };
        var perm2 = new Permission { Code = "core.admin", DisplayName = "Duplicate Core Admin" };

        _context.Permissions.Add(perm1);
        await _context.SaveChangesAsync();

        // Act & Assert
        _context.Permissions.Add(perm2);
        try
        {
            await _context.SaveChangesAsync();
            Assert.Fail("Should have thrown DbUpdateException for duplicate permission codes");
        }
        catch (DbUpdateException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task Role_Name_IsUnique()
    {
        // Arrange
        var role1 = new Role { Name = "Admin", IsSystemRole = true };
        var role2 = new Role { Name = "Admin", IsSystemRole = false };

        _context.Roles.Add(role1);
        await _context.SaveChangesAsync();

        // Act & Assert
        _context.Roles.Add(role2);
        try
        {
            await _context.SaveChangesAsync();
            Assert.Fail("Should have thrown DbUpdateException for duplicate role names");
        }
        catch (DbUpdateException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task Role_CanHaveMultiplePermissions()
    {
        // Arrange
        var role = new Role { Name = "SuperAdmin", IsSystemRole = true };
        var permissions = new List<Permission>
        {
            new Permission { Code = "core.admin", DisplayName = "Core Admin" },
            new Permission { Code = "core.users.manage", DisplayName = "Manage Users" },
            new Permission { Code = "core.roles.manage", DisplayName = "Manage Roles" },
            new Permission { Code = "core.settings.manage", DisplayName = "Manage Settings" }
        };

        _context.Roles.Add(role);
        _context.Permissions.AddRange(permissions);
        await _context.SaveChangesAsync();

        // Add role permissions
        var rolePermissions = permissions.Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id }).ToList();
        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();

        // Act
        var retrievedRole = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == role.Id);

        // Assert
        Assert.AreEqual(4, retrievedRole.RolePermissions.Count, "SuperAdmin role should have 4 permissions");
        Assert.IsTrue(retrievedRole.RolePermissions.All(rp => rp.Permission != null), "All permissions should be loaded");
    }

    [TestMethod]
    public async Task Permission_CanBeAssignedToMultipleRoles()
    {
        // Arrange
        var permission = new Permission { Code = "files.view", DisplayName = "View Files" };
        var roles = new List<Role>
        {
            new Role { Name = "Admin", IsSystemRole = true },
            new Role { Name = "User", IsSystemRole = true },
            new Role { Name = "Guest", IsSystemRole = true }
        };

        _context.Permissions.Add(permission);
        _context.Roles.AddRange(roles);
        await _context.SaveChangesAsync();

        // Add role permissions
        var rolePermissions = roles.Select(r => new RolePermission { RoleId = r.Id, PermissionId = permission.Id }).ToList();
        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();

        // Act
        var retrievedPermission = await _context.Permissions
            .Include(p => p.RolePermissions)
            .ThenInclude(rp => rp.Role)
            .FirstAsync(p => p.Id == permission.Id);

        // Assert
        Assert.AreEqual(3, retrievedPermission.RolePermissions.Count, "Permission should be in 3 roles");
        var roleNames = retrievedPermission.RolePermissions.Select(rp => rp.Role.Name).ToList();
        Assert.IsTrue(roleNames.Contains("Admin"), "Permission should be in Admin role");
        Assert.IsTrue(roleNames.Contains("User"), "Permission should be in User role");
        Assert.IsTrue(roleNames.Contains("Guest"), "Permission should be in Guest role");
    }

    [TestMethod]
    public async Task CascadeDelete_Permission_RemovesRolePermissions()
    {
        // Arrange
        var role = new Role { Name = "Admin", IsSystemRole = true };
        var permission = new Permission { Code = "test.delete", DisplayName = "Test Delete" };
        var rolePermission = new RolePermission { Role = role, Permission = permission };

        _context.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act - Delete permission
        _context.Permissions.Remove(permission);
        await _context.SaveChangesAsync();

        // Assert
        var orphanedRolePermissions = await _context.RolePermissions
            .Where(rp => rp.PermissionId == permission.Id)
            .ToListAsync();
        Assert.AreEqual(0, orphanedRolePermissions.Count, "RolePermission should be deleted with permission");
    }

    [TestMethod]
    public async Task CascadeDelete_Role_RemovesRolePermissions()
    {
        // Arrange
        var role = new Role { Name = "TempRole", IsSystemRole = false };
        var permission = new Permission { Code = "temp.action", DisplayName = "Temp Action" };
        var rolePermission = new RolePermission { Role = role, Permission = permission };

        _context.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act - Delete role
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        // Assert
        var orphanedRolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync();
        Assert.AreEqual(0, orphanedRolePermissions.Count, "RolePermission should be deleted with role");
    }

    [TestMethod]
    public async Task SystemRole_Flag_DistinguishesSystemAndCustomRoles()
    {
        // Arrange
        var systemRole = new Role { Name = "SystemAdmin", IsSystemRole = true };
        var customRole = new Role { Name = "CustomRole", IsSystemRole = false };

        _context.Roles.AddRange(systemRole, customRole);
        await _context.SaveChangesAsync();

        // Act
        var systemRoles = await _context.Roles
            .Where(r => r.IsSystemRole)
            .ToListAsync();
        var customRoles = await _context.Roles
            .Where(r => !r.IsSystemRole)
            .ToListAsync();

        // Assert
        Assert.AreEqual(1, systemRoles.Count, "Should find 1 system role");
        Assert.AreEqual(1, customRoles.Count, "Should find 1 custom role");
        Assert.IsTrue(systemRoles[0].IsSystemRole, "System role should be marked");
        Assert.IsFalse(customRoles[0].IsSystemRole, "Custom role should not be marked");
    }
}
