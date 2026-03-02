using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Tests.Entities.Identity;

/// <summary>
/// Unit tests for the ApplicationRole entity.
/// </summary>
[TestClass]
public class ApplicationRoleTests
{
    [TestMethod]
    public void ApplicationRole_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var role = new ApplicationRole
        {
            Name = "TestRole"
        };

        // Assert
        Assert.IsFalse(role.IsSystemRole, "Default IsSystemRole should be false");
        Assert.IsNull(role.Description, "Default Description should be null");
    }

    [TestMethod]
    public void ApplicationRole_PrimaryKey_IsGuid()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole
        {
            Id = roleId,
            Name = "TestRole"
        };

        // Act & Assert
        Assert.AreEqual(roleId, role.Id);
        Assert.IsInstanceOfType(role.Id, typeof(Guid));
    }

    [TestMethod]
    public void ApplicationRole_Name_CanBeSet()
    {
        // Arrange
        var roleName = "Administrator";
        var role = new ApplicationRole
        {
            Name = roleName
        };

        // Act & Assert
        Assert.AreEqual(roleName, role.Name);
    }

    [TestMethod]
    public void ApplicationRole_Description_CanBeSet()
    {
        // Arrange
        var description = "System administrators with full access";
        var role = new ApplicationRole
        {
            Name = "Administrator",
            Description = description
        };

        // Act & Assert
        Assert.AreEqual(description, role.Description);
    }

    [TestMethod]
    public void ApplicationRole_IsSystemRole_CanBeSet()
    {
        // Arrange
        var role = new ApplicationRole
        {
            Name = "SystemAdministrator",
            IsSystemRole = true
        };

        // Act & Assert
        Assert.IsTrue(role.IsSystemRole);
    }

    [TestMethod]
    public void ApplicationRole_InheritsFromIdentityRole()
    {
        // Arrange
        var role = new ApplicationRole
        {
            Name = "Manager",
            NormalizedName = "MANAGER"
        };

        // Act & Assert - Check inherited properties
        Assert.AreEqual("Manager", role.Name);
        Assert.AreEqual("MANAGER", role.NormalizedName);
        Assert.IsNotNull(role.ConcurrencyStamp);
    }

    [TestMethod]
    public void ApplicationRole_AllProperties_CanBeRoundTripped()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var name = "ContentEditor";
        var description = "Can create and edit content";
        var isSystemRole = false;

        // Act
        var role = new ApplicationRole
        {
            Id = roleId,
            Name = name,
            Description = description,
            IsSystemRole = isSystemRole,
            NormalizedName = name.ToUpperInvariant()
        };

        // Assert
        Assert.AreEqual(roleId, role.Id);
        Assert.AreEqual(name, role.Name);
        Assert.AreEqual(description, role.Description);
        Assert.AreEqual(isSystemRole, role.IsSystemRole);
        Assert.AreEqual(name.ToUpperInvariant(), role.NormalizedName);
    }

    [TestMethod]
    public void ApplicationRole_SystemRole_CannotBeRegularRole()
    {
        // Arrange
        var systemRole = new ApplicationRole
        {
            Name = "SystemAdministrator",
            Description = "Cannot be deleted",
            IsSystemRole = true
        };

        var regularRole = new ApplicationRole
        {
            Name = "User",
            Description = "Regular user",
            IsSystemRole = false
        };

        // Act & Assert
        Assert.IsTrue(systemRole.IsSystemRole, "System role should be marked as system role");
        Assert.IsFalse(regularRole.IsSystemRole, "Regular role should not be marked as system role");
    }

    [TestMethod]
    public void ApplicationRole_Description_CanBeCleared()
    {
        // Arrange
        var role = new ApplicationRole
        {
            Name = "TestRole",
            Description = "Initial description"
        };

        // Act
        role.Description = null;

        // Assert
        Assert.IsNull(role.Description);
    }

    [TestMethod]
    public void ApplicationRole_Multiple_HaveUniqueIds()
    {
        // Arrange & Act
        var role1 = new ApplicationRole { Name = "Role1", Id = Guid.NewGuid() };
        var role2 = new ApplicationRole { Name = "Role2", Id = Guid.NewGuid() };
        var role3 = new ApplicationRole { Name = "Role3", Id = Guid.NewGuid() };

        // Assert
        Assert.AreNotEqual(role1.Id, role2.Id, "Role1 and Role2 should have different IDs");
        Assert.AreNotEqual(role2.Id, role3.Id, "Role2 and Role3 should have different IDs");
        Assert.AreNotEqual(role1.Id, role3.Id, "Role1 and Role3 should have different IDs");
    }
}
