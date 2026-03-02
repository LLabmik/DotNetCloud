using DotNetCloud.Core.Data.Entities.Organizations;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Unit tests for the <see cref="Organization"/> entity.
/// </summary>
[TestClass]
public class OrganizationTests
{
    [TestMethod]
    public void Organization_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var organization = new Organization();

        // Assert
        Assert.AreEqual(Guid.Empty, organization.Id);
        Assert.AreEqual(string.Empty, organization.Name);
        Assert.IsNull(organization.Description);
        Assert.AreEqual(default(DateTime), organization.CreatedAt);
        Assert.IsFalse(organization.IsDeleted);
        Assert.IsNull(organization.DeletedAt);
        Assert.IsNotNull(organization.Teams);
        Assert.IsNotNull(organization.Groups);
        Assert.IsNotNull(organization.Members);
        Assert.IsNotNull(organization.Settings);
    }

    [TestMethod]
    public void Organization_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Acme Corporation";
        var description = "Leading provider of widgets";
        var createdAt = DateTime.UtcNow;

        // Act
        var organization = new Organization
        {
            Id = id,
            Name = name,
            Description = description,
            CreatedAt = createdAt
        };

        // Assert
        Assert.AreEqual(id, organization.Id);
        Assert.AreEqual(name, organization.Name);
        Assert.AreEqual(description, organization.Description);
        Assert.AreEqual(createdAt, organization.CreatedAt);
    }

    [TestMethod]
    public void Organization_SoftDelete_SetsIsDeletedAndDeletedAt()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        organization.IsDeleted = true;
        organization.DeletedAt = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(organization.IsDeleted);
        Assert.IsNotNull(organization.DeletedAt);
        Assert.IsTrue(organization.DeletedAt.Value <= DateTime.UtcNow);
    }

    [TestMethod]
    public void Organization_NavigationProperties_CanBeInitialized()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org"
        };

        // Act
        organization.Teams = new List<Team> { new Team { Name = "Team1" } };
        organization.Groups = new List<Group> { new Group { Name = "Group1" } };
        organization.Members = new List<OrganizationMember>();

        // Assert
        Assert.AreEqual(1, organization.Teams.Count);
        Assert.AreEqual(1, organization.Groups.Count);
        Assert.AreEqual(0, organization.Members.Count);
    }

    [TestMethod]
    public void Organization_Name_RequiredProperty()
    {
        // Arrange
        var organization = new Organization();

        // Act
        organization.Name = "Test Organization";

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(organization.Name));
    }

    [TestMethod]
    public void Organization_Description_OptionalProperty()
    {
        // Arrange
        var organization = new Organization
        {
            Name = "Test Org"
        };

        // Act
        // Description is left null

        // Assert
        Assert.IsNull(organization.Description);
    }

    [TestMethod]
    public void Organization_MultipleTeams_CanBeAdded()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Multi-Team Org"
        };

        // Act
        organization.Teams.Add(new Team { Name = "Engineering" });
        organization.Teams.Add(new Team { Name = "Marketing" });
        organization.Teams.Add(new Team { Name = "Sales" });

        // Assert
        Assert.AreEqual(3, organization.Teams.Count);
    }

    [TestMethod]
    public void Organization_MultipleGroups_CanBeAdded()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Multi-Group Org"
        };

        // Act
        organization.Groups.Add(new Group { Name = "Administrators" });
        organization.Groups.Add(new Group { Name = "Editors" });

        // Assert
        Assert.AreEqual(2, organization.Groups.Count);
    }

    [TestMethod]
    public void Organization_IsDeleted_DefaultsToFalse()
    {
        // Arrange & Act
        var organization = new Organization();

        // Assert
        Assert.IsFalse(organization.IsDeleted);
    }

    [TestMethod]
    public void Organization_CreatedAt_CanBeSet()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var organization = new Organization
        {
            Name = "Test Org"
        };

        // Act
        organization.CreatedAt = now;

        // Assert
        Assert.AreEqual(now, organization.CreatedAt);
    }
}
