namespace DotNetCloud.Core.Tests.DTOs;

using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for permission related DTOs.
/// </summary>
[TestClass]
public class PermissionDtosTests
{
    [TestMethod]
    public void PermissionDto_CanBeCreated()
    {
        // Arrange
        var permissionId = Guid.NewGuid();

        // Act
        var permission = new PermissionDto
        {
            Id = permissionId,
            Code = "files.upload",
            DisplayName = "Upload Files",
            Module = "files"
        };

        // Assert
        Assert.AreEqual(permissionId, permission.Id);
        Assert.AreEqual("files.upload", permission.Code);
        Assert.AreEqual("Upload Files", permission.DisplayName);
    }

    [TestMethod]
    public void PermissionDto_WithDescription()
    {
        // Arrange & Act
        var permission = new PermissionDto
        {
            Id = Guid.NewGuid(),
            Code = "files.delete",
            DisplayName = "Delete Files",
            Description = "Allows deleting user files",
            Module = "files"
        };

        // Assert
        Assert.AreEqual("Allows deleting user files", permission.Description);
    }

    [TestMethod]
    public void CreatePermissionDto_CanBeCreated()
    {
        // Arrange & Act
        var permission = new CreatePermissionDto
        {
            Code = "users.manage",
            DisplayName = "Manage Users",
            Description = "Full user management access",
            Module = "core"
        };

        // Assert
        Assert.AreEqual("users.manage", permission.Code);
        Assert.AreEqual("Manage Users", permission.DisplayName);
        Assert.AreEqual("Full user management access", permission.Description);
    }
}

/// <summary>
/// Tests for role related DTOs.
/// </summary>
[TestClass]
public class RoleDtosTests
{
    [TestMethod]
    public void RoleDto_CanBeCreated()
    {
        // Arrange
        var roleId = Guid.NewGuid();

        // Act
        var role = new RoleDto
        {
            Id = roleId,
            Name = "Administrator"
        };

        // Assert
        Assert.AreEqual(roleId, role.Id);
        Assert.AreEqual("Administrator", role.Name);
    }

    [TestMethod]
    public void RoleDto_WithDescription()
    {
        // Arrange & Act
        var role = new RoleDto
        {
            Id = Guid.NewGuid(),
            Name = "Moderator",
            Description = "Content moderator role",
            IsSystemRole = false
        };

        // Assert
        Assert.AreEqual("Moderator", role.Name);
        Assert.AreEqual("Content moderator role", role.Description);
        Assert.IsFalse(role.IsSystemRole);
    }

    [TestMethod]
    public void RoleDto_WithPermissions()
    {
        // Arrange
        var permissions = new List<PermissionDto>
        {
            new PermissionDto { Id = Guid.NewGuid(), Code = "read", DisplayName = "Read", Module = "core" },
            new PermissionDto { Id = Guid.NewGuid(), Code = "write", DisplayName = "Write", Module = "core" }
        };

        // Act
        var role = new RoleDto
        {
            Id = Guid.NewGuid(),
            Name = "Editor",
            Permissions = permissions
        };

        // Assert
        Assert.AreEqual(2, role.Permissions.Count);
    }

    [TestMethod]
    public void RoleDto_HasDefaultPermissions()
    {
        // Arrange & Act
        var role = new RoleDto { Name = "User" };

        // Assert
        Assert.IsNotNull(role.Permissions);
    }

    [TestMethod]
    public void CreateRoleDto_CanBeCreated()
    {
        // Arrange & Act
        var role = new CreateRoleDto
        {
            Name = "Editor",
            Description = "Content editor"
        };

        // Assert
        Assert.AreEqual("Editor", role.Name);
        Assert.AreEqual("Content editor", role.Description);
    }
}

/// <summary>
/// Tests for module DTOs.
/// </summary>
[TestClass]
public class ModuleDtosTests
{
    [TestMethod]
    public void ModuleDto_CanBeCreated()
    {
        // Arrange & Act
        var module = new ModuleDto
        {
            Id = "test.module",
            Name = "Test Module",
            Version = "1.0.0",
            Status = "Enabled"
        };

        // Assert
        Assert.AreEqual("test.module", module.Id);
        Assert.AreEqual("Test Module", module.Name);
        Assert.AreEqual("1.0.0", module.Version);
    }

    [TestMethod]
    public void ModuleDto_WithCapabilities()
    {
        // Arrange & Act
        var module = new ModuleDto
        {
            Id = "test.module",
            Name = "Test Module",
            Version = "1.0.0",
            Status = "Enabled",
            RequiredCapabilities = new[] { "IEventBus", "IStorageProvider" }
        };

        // Assert
        Assert.AreEqual(2, module.RequiredCapabilities.Count);
    }

    [TestMethod]
    public void ModuleDto_WithEvents()
    {
        // Arrange & Act
        var module = new ModuleDto
        {
            Id = "test.module",
            Name = "Test Module",
            Version = "1.0.0",
            Status = "Enabled",
            PublishedEvents = new[] { "TestEvent" },
            SubscribedEvents = new[] { "UserCreatedEvent" }
        };

        // Assert
        Assert.AreEqual(1, module.PublishedEvents.Count);
        Assert.AreEqual(1, module.SubscribedEvents.Count);
    }

    [TestMethod]
    public void CreateModuleDto_CanBeCreated()
    {
        // Arrange & Act
        var module = new CreateModuleDto
        {
            Id = "test.module",
            Name = "Test Module",
            Version = "1.0.0",
            Description = "A test module"
        };

        // Assert
        Assert.AreEqual("test.module", module.Id);
        Assert.AreEqual("1.0.0", module.Version);
    }
}
