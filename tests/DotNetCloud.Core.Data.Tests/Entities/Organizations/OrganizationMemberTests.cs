using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Unit tests for the <see cref="OrganizationMember"/> entity.
/// </summary>
[TestClass]
public class OrganizationMemberTests
{
    [TestMethod]
    public void OrganizationMember_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var orgMember = new OrganizationMember();

        // Assert
        Assert.AreEqual(Guid.Empty, orgMember.OrganizationId);
        Assert.AreEqual(Guid.Empty, orgMember.UserId);
        Assert.IsNotNull(orgMember.RoleIds);
        Assert.AreEqual(0, orgMember.RoleIds.Count);
        Assert.AreEqual(default(DateTime), orgMember.JoinedAt);
        Assert.IsNull(orgMember.InvitedByUserId);
        Assert.IsTrue(orgMember.IsActive);
    }

    [TestMethod]
    public void OrganizationMember_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();
        var joinedAt = DateTime.UtcNow;
        var invitedByUserId = Guid.NewGuid();

        // Act
        var orgMember = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            RoleIds = new List<Guid> { roleId1, roleId2 },
            JoinedAt = joinedAt,
            InvitedByUserId = invitedByUserId,
            IsActive = true
        };

        // Assert
        Assert.AreEqual(organizationId, orgMember.OrganizationId);
        Assert.AreEqual(userId, orgMember.UserId);
        Assert.AreEqual(2, orgMember.RoleIds.Count);
        Assert.IsTrue(orgMember.RoleIds.Contains(roleId1));
        Assert.IsTrue(orgMember.RoleIds.Contains(roleId2));
        Assert.AreEqual(joinedAt, orgMember.JoinedAt);
        Assert.AreEqual(invitedByUserId, orgMember.InvitedByUserId);
        Assert.IsTrue(orgMember.IsActive);
    }

    [TestMethod]
    public void OrganizationMember_CompositeKey_OrganizationIdAndUserId()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var orgMember = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, orgMember.OrganizationId);
        Assert.AreNotEqual(Guid.Empty, orgMember.UserId);
    }

    [TestMethod]
    public void OrganizationMember_RoleIds_CanBeEmpty()
    {
        // Arrange
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            JoinedAt = DateTime.UtcNow
        };

        // Act
        // RoleIds left as default (empty collection)

        // Assert
        Assert.IsNotNull(orgMember.RoleIds);
        Assert.AreEqual(0, orgMember.RoleIds.Count);
    }

    [TestMethod]
    public void OrganizationMember_RoleIds_CanHaveMultipleRoles()
    {
        // Arrange
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        // Act
        orgMember.RoleIds.Add(Guid.NewGuid()); // OrganizationAdmin
        orgMember.RoleIds.Add(Guid.NewGuid()); // BillingManager
        orgMember.RoleIds.Add(Guid.NewGuid()); // UserManager

        // Assert
        Assert.AreEqual(3, orgMember.RoleIds.Count);
    }

    [TestMethod]
    public void OrganizationMember_NavigationProperty_Organization_CanBeSet()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org"
        };

        var orgMember = new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = Guid.NewGuid()
        };

        // Act
        orgMember.Organization = organization;

        // Assert
        Assert.IsNotNull(orgMember.Organization);
        Assert.AreEqual(organization.Id, orgMember.Organization.Id);
    }

    [TestMethod]
    public void OrganizationMember_NavigationProperty_User_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = user.Id
        };

        // Act
        orgMember.User = user;

        // Assert
        Assert.IsNotNull(orgMember.User);
        Assert.AreEqual(user.Id, orgMember.User.Id);
    }

    [TestMethod]
    public void OrganizationMember_NavigationProperty_InvitedByUser_CanBeSet()
    {
        // Arrange
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
            Email = "admin@example.com",
            DisplayName = "Admin User"
        };

        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            InvitedByUserId = admin.Id
        };

        // Act
        orgMember.InvitedByUser = admin;

        // Assert
        Assert.IsNotNull(orgMember.InvitedByUser);
        Assert.AreEqual(admin.Id, orgMember.InvitedByUser.Id);
    }

    [TestMethod]
    public void OrganizationMember_IsActive_DefaultsToTrue()
    {
        // Arrange & Act
        var orgMember = new OrganizationMember();

        // Assert
        Assert.IsTrue(orgMember.IsActive);
    }

    [TestMethod]
    public void OrganizationMember_IsActive_CanBeSetToFalse()
    {
        // Arrange
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsActive = true
        };

        // Act
        orgMember.IsActive = false;

        // Assert
        Assert.IsFalse(orgMember.IsActive);
    }

    [TestMethod]
    public void OrganizationMember_InvitedByUserId_OptionalProperty()
    {
        // Arrange
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            JoinedAt = DateTime.UtcNow
        };

        // Act
        // InvitedByUserId left null (self-registration or system setup)

        // Assert
        Assert.IsNull(orgMember.InvitedByUserId);
    }

    [TestMethod]
    public void OrganizationMember_JoinedAt_TracksTimestamp()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            JoinedAt = now
        };

        // Assert
        Assert.AreEqual(now, orgMember.JoinedAt);
    }

    [TestMethod]
    public void OrganizationMember_RoleIds_CanBeModified()
    {
        // Arrange
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RoleIds = new List<Guid> { roleId1 }
        };

        // Act
        orgMember.RoleIds.Add(roleId2);
        orgMember.RoleIds.Remove(roleId1);

        // Assert
        Assert.AreEqual(1, orgMember.RoleIds.Count);
        Assert.IsTrue(orgMember.RoleIds.Contains(roleId2));
        Assert.IsFalse(orgMember.RoleIds.Contains(roleId1));
    }

    [TestMethod]
    public void OrganizationMember_DifferentUsersInSameOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        // Act
        var member1 = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = Guid.NewGuid()
        };

        var member2 = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = Guid.NewGuid()
        };

        // Assert
        Assert.AreEqual(organizationId, member1.OrganizationId);
        Assert.AreEqual(organizationId, member2.OrganizationId);
        Assert.AreNotEqual(member1.UserId, member2.UserId);
    }

    [TestMethod]
    public void OrganizationMember_RoleHierarchy_OrganizationWide()
    {
        // Arrange
        var organizationAdminRoleId = Guid.NewGuid();
        var billingManagerRoleId = Guid.NewGuid();

        // Act
        var orgMember = new OrganizationMember
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RoleIds = new List<Guid> { organizationAdminRoleId, billingManagerRoleId }
        };

        // Assert
        Assert.AreEqual(2, orgMember.RoleIds.Count);
        Assert.IsTrue(orgMember.RoleIds.Contains(organizationAdminRoleId));
        Assert.IsTrue(orgMember.RoleIds.Contains(billingManagerRoleId));
    }
}
