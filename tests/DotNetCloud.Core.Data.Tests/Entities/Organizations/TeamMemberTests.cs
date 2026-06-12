using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Unit tests for the <see cref="TeamMember"/> entity.
/// </summary>
[TestClass]
public class TeamMemberTests
{
    [TestMethod]
    public void TeamMember_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var teamMember = new TeamMember();

        // Assert
        Assert.AreEqual(Guid.Empty, teamMember.TeamId);
        Assert.AreEqual(Guid.Empty, teamMember.UserId);
        Assert.IsNotNull(teamMember.RoleIds);
        Assert.AreEqual(0, teamMember.RoleIds.Count);
        Assert.AreEqual(default(DateTime), teamMember.JoinedAt);
    }

    [TestMethod]
    public void TeamMember_SetProperties_StoresValuesCorrectly()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var roleId1 = Guid.CreateVersion7();
        var roleId2 = Guid.CreateVersion7();
        var joinedAt = DateTime.UtcNow;

        // Act
        var teamMember = new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            RoleIds = new List<Guid> { roleId1, roleId2 },
            JoinedAt = joinedAt
        };

        // Assert
        Assert.AreEqual(teamId, teamMember.TeamId);
        Assert.AreEqual(userId, teamMember.UserId);
        Assert.AreEqual(2, teamMember.RoleIds.Count);
        Assert.IsTrue(teamMember.RoleIds.Contains(roleId1));
        Assert.IsTrue(teamMember.RoleIds.Contains(roleId2));
        Assert.AreEqual(joinedAt, teamMember.JoinedAt);
    }

    [TestMethod]
    public void TeamMember_CompositeKey_TeamIdAndUserId()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        // Act
        var teamMember = new TeamMember
        {
            TeamId = teamId,
            UserId = userId
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, teamMember.TeamId);
        Assert.AreNotEqual(Guid.Empty, teamMember.UserId);
    }

    [TestMethod]
    public void TeamMember_RoleIds_CanBeEmpty()
    {
        // Arrange
        var teamMember = new TeamMember
        {
            TeamId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            JoinedAt = DateTime.UtcNow
        };

        // Act
        // RoleIds left as default (empty collection)

        // Assert
        Assert.IsNotNull(teamMember.RoleIds);
        Assert.AreEqual(0, teamMember.RoleIds.Count);
    }

    [TestMethod]
    public void TeamMember_RoleIds_CanHaveMultipleRoles()
    {
        // Arrange
        var teamMember = new TeamMember
        {
            TeamId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
        };

        // Act
        teamMember.RoleIds.Add(Guid.CreateVersion7()); // TeamLead
        teamMember.RoleIds.Add(Guid.CreateVersion7()); // Reviewer
        teamMember.RoleIds.Add(Guid.CreateVersion7()); // Admin

        // Assert
        Assert.AreEqual(3, teamMember.RoleIds.Count);
    }

    [TestMethod]
    public void TeamMember_NavigationProperty_Team_CanBeSet()
    {
        // Arrange
        var team = new Team
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Team",
            OrganizationId = Guid.CreateVersion7()
        };

        var teamMember = new TeamMember
        {
            TeamId = team.Id,
            UserId = Guid.CreateVersion7()
        };

        // Act
        teamMember.Team = team;

        // Assert
        Assert.IsNotNull(teamMember.Team);
        Assert.AreEqual(team.Id, teamMember.Team.Id);
    }

    [TestMethod]
    public void TeamMember_NavigationProperty_User_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var teamMember = new TeamMember
        {
            TeamId = Guid.CreateVersion7(),
            UserId = user.Id
        };

        // Act
        teamMember.User = user;

        // Assert
        Assert.IsNotNull(teamMember.User);
        Assert.AreEqual(user.Id, teamMember.User.Id);
    }

    [TestMethod]
    public void TeamMember_JoinedAt_TracksTimestamp()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var teamMember = new TeamMember
        {
            TeamId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            JoinedAt = now
        };

        // Assert
        Assert.AreEqual(now, teamMember.JoinedAt);
    }

    [TestMethod]
    public void TeamMember_RoleIds_CanBeModified()
    {
        // Arrange
        var roleId1 = Guid.CreateVersion7();
        var roleId2 = Guid.CreateVersion7();
        var teamMember = new TeamMember
        {
            TeamId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            RoleIds = new List<Guid> { roleId1 }
        };

        // Act
        teamMember.RoleIds.Add(roleId2);
        teamMember.RoleIds.Remove(roleId1);

        // Assert
        Assert.AreEqual(1, teamMember.RoleIds.Count);
        Assert.IsTrue(teamMember.RoleIds.Contains(roleId2));
        Assert.IsFalse(teamMember.RoleIds.Contains(roleId1));
    }

    [TestMethod]
    public void TeamMember_DifferentUsersInSameTeam()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        // Act
        var member1 = new TeamMember
        {
            TeamId = teamId,
            UserId = Guid.CreateVersion7()
        };

        var member2 = new TeamMember
        {
            TeamId = teamId,
            UserId = Guid.CreateVersion7()
        };

        // Assert
        Assert.AreEqual(teamId, member1.TeamId);
        Assert.AreEqual(teamId, member2.TeamId);
        Assert.AreNotEqual(member1.UserId, member2.UserId);
    }
}
