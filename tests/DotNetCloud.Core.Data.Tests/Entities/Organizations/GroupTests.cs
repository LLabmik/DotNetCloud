using DotNetCloud.Core.Data.Entities.Organizations;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Unit tests for the <see cref="Group"/> entity.
/// </summary>
[TestClass]
public class GroupTests
{
    [TestMethod]
    public void Group_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var group = new Group();

        // Assert
        Assert.AreEqual(Guid.Empty, group.Id);
        Assert.AreEqual(Guid.Empty, group.OrganizationId);
        Assert.AreEqual(string.Empty, group.Name);
        Assert.IsNull(group.Description);
        Assert.AreEqual(default(DateTime), group.CreatedAt);
        Assert.IsFalse(group.IsDeleted);
        Assert.IsNull(group.DeletedAt);
        Assert.IsNotNull(group.Members);
    }

    [TestMethod]
    public void Group_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();
        var name = "Administrators";
        var description = "Organization administrators with full access";
        var createdAt = DateTime.UtcNow;

        // Act
        var group = new Group
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Description = description,
            CreatedAt = createdAt
        };

        // Assert
        Assert.AreEqual(id, group.Id);
        Assert.AreEqual(organizationId, group.OrganizationId);
        Assert.AreEqual(name, group.Name);
        Assert.AreEqual(description, group.Description);
        Assert.AreEqual(createdAt, group.CreatedAt);
    }

    [TestMethod]
    public void Group_SoftDelete_SetsIsDeletedAndDeletedAt()
    {
        // Arrange
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            Name = "Test Group",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        group.IsDeleted = true;
        group.DeletedAt = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(group.IsDeleted);
        Assert.IsNotNull(group.DeletedAt);
        Assert.IsTrue(group.DeletedAt.Value <= DateTime.UtcNow);
    }

    [TestMethod]
    public void Group_Members_NavigationProperty_CanBeInitialized()
    {
        // Arrange
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            Name = "Test Group"
        };

        // Act
        group.Members = new List<GroupMember>
        {
            new GroupMember { GroupId = group.Id, UserId = Guid.CreateVersion7() }
        };

        // Assert
        Assert.AreEqual(1, group.Members.Count);
    }

    [TestMethod]
    public void Group_OrganizationId_RequiredForeignKey()
    {
        // Arrange
        var organizationId = Guid.CreateVersion7();

        // Act
        var group = new Group
        {
            OrganizationId = organizationId,
            Name = "Test Group"
        };

        // Assert
        Assert.AreEqual(organizationId, group.OrganizationId);
        Assert.AreNotEqual(Guid.Empty, group.OrganizationId);
    }

    [TestMethod]
    public void Group_Name_RequiredProperty()
    {
        // Arrange
        var group = new Group();

        // Act
        group.Name = "ContentEditors";

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(group.Name));
    }

    [TestMethod]
    public void Group_Description_OptionalProperty()
    {
        // Arrange
        var group = new Group
        {
            Name = "Test Group",
            OrganizationId = Guid.CreateVersion7()
        };

        // Act
        // Description is left null

        // Assert
        Assert.IsNull(group.Description);
    }

    [TestMethod]
    public void Group_MultipleMembers_CanBeAdded()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var group = new Group
        {
            Id = groupId,
            OrganizationId = Guid.CreateVersion7(),
            Name = "Large Group"
        };

        // Act
        group.Members.Add(new GroupMember { GroupId = groupId, UserId = Guid.CreateVersion7() });
        group.Members.Add(new GroupMember { GroupId = groupId, UserId = Guid.CreateVersion7() });
        group.Members.Add(new GroupMember { GroupId = groupId, UserId = Guid.CreateVersion7() });

        // Assert
        Assert.AreEqual(3, group.Members.Count);
    }

    [TestMethod]
    public void Group_IsDeleted_DefaultsToFalse()
    {
        // Arrange & Act
        var group = new Group();

        // Assert
        Assert.IsFalse(group.IsDeleted);
    }

    [TestMethod]
    public void Group_Organization_NavigationProperty_CanBeSet()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Org"
        };

        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organization.Id,
            Name = "Test Group"
        };

        // Act
        group.Organization = organization;

        // Assert
        Assert.IsNotNull(group.Organization);
        Assert.AreEqual(organization.Id, group.Organization.Id);
    }

    [TestMethod]
    public void Group_CrossTeamPermissionGroup_Concept()
    {
        // Arrange
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            Name = "AllDevelopers",
            Description = "All developers across multiple teams"
        };

        // Act
        // Add members from different teams
        group.Members.Add(new GroupMember { GroupId = group.Id, UserId = Guid.CreateVersion7() }); // From Team A
        group.Members.Add(new GroupMember { GroupId = group.Id, UserId = Guid.CreateVersion7() }); // From Team B
        group.Members.Add(new GroupMember { GroupId = group.Id, UserId = Guid.CreateVersion7() }); // From Team C

        // Assert
        Assert.AreEqual(3, group.Members.Count);
        Assert.AreEqual("AllDevelopers", group.Name);
    }
}
