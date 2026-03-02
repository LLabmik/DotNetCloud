using DotNetCloud.Core.Data.Entities.Organizations;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Unit tests for the <see cref="Team"/> entity.
/// </summary>
[TestClass]
public class TeamTests
{
    [TestMethod]
    public void Team_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var team = new Team();

        // Assert
        Assert.AreEqual(Guid.Empty, team.Id);
        Assert.AreEqual(Guid.Empty, team.OrganizationId);
        Assert.AreEqual(string.Empty, team.Name);
        Assert.IsNull(team.Description);
        Assert.AreEqual(default(DateTime), team.CreatedAt);
        Assert.IsFalse(team.IsDeleted);
        Assert.IsNull(team.DeletedAt);
        Assert.IsNotNull(team.Members);
    }

    [TestMethod]
    public void Team_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var name = "Engineering Team";
        var description = "Software development team";
        var createdAt = DateTime.UtcNow;

        // Act
        var team = new Team
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Description = description,
            CreatedAt = createdAt
        };

        // Assert
        Assert.AreEqual(id, team.Id);
        Assert.AreEqual(organizationId, team.OrganizationId);
        Assert.AreEqual(name, team.Name);
        Assert.AreEqual(description, team.Description);
        Assert.AreEqual(createdAt, team.CreatedAt);
    }

    [TestMethod]
    public void Team_SoftDelete_SetsIsDeletedAndDeletedAt()
    {
        // Arrange
        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Test Team",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        team.IsDeleted = true;
        team.DeletedAt = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(team.IsDeleted);
        Assert.IsNotNull(team.DeletedAt);
        Assert.IsTrue(team.DeletedAt.Value <= DateTime.UtcNow);
    }

    [TestMethod]
    public void Team_Members_NavigationProperty_CanBeInitialized()
    {
        // Arrange
        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Test Team"
        };

        // Act
        team.Members = new List<TeamMember>
        {
            new TeamMember { TeamId = team.Id, UserId = Guid.NewGuid() }
        };

        // Assert
        Assert.AreEqual(1, team.Members.Count);
    }

    [TestMethod]
    public void Team_OrganizationId_RequiredForeignKey()
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        // Act
        var team = new Team
        {
            OrganizationId = organizationId,
            Name = "Test Team"
        };

        // Assert
        Assert.AreEqual(organizationId, team.OrganizationId);
        Assert.AreNotEqual(Guid.Empty, team.OrganizationId);
    }

    [TestMethod]
    public void Team_Name_RequiredProperty()
    {
        // Arrange
        var team = new Team();

        // Act
        team.Name = "Marketing Team";

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(team.Name));
    }

    [TestMethod]
    public void Team_Description_OptionalProperty()
    {
        // Arrange
        var team = new Team
        {
            Name = "Test Team",
            OrganizationId = Guid.NewGuid()
        };

        // Act
        // Description is left null

        // Assert
        Assert.IsNull(team.Description);
    }

    [TestMethod]
    public void Team_MultipleMembers_CanBeAdded()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var team = new Team
        {
            Id = teamId,
            OrganizationId = Guid.NewGuid(),
            Name = "Large Team"
        };

        // Act
        team.Members.Add(new TeamMember { TeamId = teamId, UserId = Guid.NewGuid() });
        team.Members.Add(new TeamMember { TeamId = teamId, UserId = Guid.NewGuid() });
        team.Members.Add(new TeamMember { TeamId = teamId, UserId = Guid.NewGuid() });

        // Assert
        Assert.AreEqual(3, team.Members.Count);
    }

    [TestMethod]
    public void Team_IsDeleted_DefaultsToFalse()
    {
        // Arrange & Act
        var team = new Team();

        // Assert
        Assert.IsFalse(team.IsDeleted);
    }

    [TestMethod]
    public void Team_Organization_NavigationProperty_CanBeSet()
    {
        // Arrange
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org"
        };

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Test Team"
        };

        // Act
        team.Organization = organization;

        // Assert
        Assert.IsNotNull(team.Organization);
        Assert.AreEqual(organization.Id, team.Organization.Id);
    }
}
