using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Unit tests for the <see cref="GroupMember"/> entity.
/// </summary>
[TestClass]
public class GroupMemberTests
{
    [TestMethod]
    public void GroupMember_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var groupMember = new GroupMember();

        // Assert
        Assert.AreEqual(Guid.Empty, groupMember.GroupId);
        Assert.AreEqual(Guid.Empty, groupMember.UserId);
        Assert.AreEqual(default(DateTime), groupMember.AddedAt);
        Assert.IsNull(groupMember.AddedByUserId);
    }

    [TestMethod]
    public void GroupMember_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var addedAt = DateTime.UtcNow;
        var addedByUserId = Guid.CreateVersion7();

        // Act
        var groupMember = new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            AddedAt = addedAt,
            AddedByUserId = addedByUserId
        };

        // Assert
        Assert.AreEqual(groupId, groupMember.GroupId);
        Assert.AreEqual(userId, groupMember.UserId);
        Assert.AreEqual(addedAt, groupMember.AddedAt);
        Assert.AreEqual(addedByUserId, groupMember.AddedByUserId);
    }

    [TestMethod]
    public void GroupMember_CompositeKey_GroupIdAndUserId()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        // Act
        var groupMember = new GroupMember
        {
            GroupId = groupId,
            UserId = userId
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, groupMember.GroupId);
        Assert.AreNotEqual(Guid.Empty, groupMember.UserId);
    }

    [TestMethod]
    public void GroupMember_AddedByUserId_OptionalProperty()
    {
        // Arrange
        var groupMember = new GroupMember
        {
            GroupId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            AddedAt = DateTime.UtcNow
        };

        // Act
        // AddedByUserId left null (system added or initial setup)

        // Assert
        Assert.IsNull(groupMember.AddedByUserId);
    }

    [TestMethod]
    public void GroupMember_NavigationProperty_Group_CanBeSet()
    {
        // Arrange
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            Name = "Administrators",
            OrganizationId = Guid.CreateVersion7()
        };

        var groupMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = Guid.CreateVersion7()
        };

        // Act
        groupMember.Group = group;

        // Assert
        Assert.IsNotNull(groupMember.Group);
        Assert.AreEqual(group.Id, groupMember.Group.Id);
    }

    [TestMethod]
    public void GroupMember_NavigationProperty_User_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var groupMember = new GroupMember
        {
            GroupId = Guid.CreateVersion7(),
            UserId = user.Id
        };

        // Act
        groupMember.User = user;

        // Assert
        Assert.IsNotNull(groupMember.User);
        Assert.AreEqual(user.Id, groupMember.User.Id);
    }

    [TestMethod]
    public void GroupMember_NavigationProperty_AddedByUser_CanBeSet()
    {
        // Arrange
        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "admin",
            Email = "admin@example.com",
            DisplayName = "Admin User"
        };

        var groupMember = new GroupMember
        {
            GroupId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            AddedByUserId = admin.Id
        };

        // Act
        groupMember.AddedByUser = admin;

        // Assert
        Assert.IsNotNull(groupMember.AddedByUser);
        Assert.AreEqual(admin.Id, groupMember.AddedByUser.Id);
    }

    [TestMethod]
    public void GroupMember_AddedAt_TracksTimestamp()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var groupMember = new GroupMember
        {
            GroupId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            AddedAt = now
        };

        // Assert
        Assert.AreEqual(now, groupMember.AddedAt);
    }

    [TestMethod]
    public void GroupMember_DifferentUsersInSameGroup()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();

        // Act
        var member1 = new GroupMember
        {
            GroupId = groupId,
            UserId = Guid.CreateVersion7()
        };

        var member2 = new GroupMember
        {
            GroupId = groupId,
            UserId = Guid.CreateVersion7()
        };

        // Assert
        Assert.AreEqual(groupId, member1.GroupId);
        Assert.AreEqual(groupId, member2.GroupId);
        Assert.AreNotEqual(member1.UserId, member2.UserId);
    }

    [TestMethod]
    public void GroupMember_AuditTracking_WithAddedBy()
    {
        // Arrange
        var adminId = Guid.CreateVersion7();
        var addedAt = DateTime.UtcNow;

        // Act
        var groupMember = new GroupMember
        {
            GroupId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            AddedAt = addedAt,
            AddedByUserId = adminId
        };

        // Assert
        Assert.AreEqual(addedAt, groupMember.AddedAt);
        Assert.AreEqual(adminId, groupMember.AddedByUserId);
    }

    [TestMethod]
    public void GroupMember_SystemAddedMembership_NullAddedBy()
    {
        // Arrange & Act
        var groupMember = new GroupMember
        {
            GroupId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            AddedAt = DateTime.UtcNow,
            AddedByUserId = null // System added
        };

        // Assert
        Assert.IsNull(groupMember.AddedByUserId);
        Assert.AreNotEqual(default(DateTime), groupMember.AddedAt);
    }
}
