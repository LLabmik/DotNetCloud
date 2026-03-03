using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Tests for entity relationships in the organization hierarchy.
/// </summary>
[TestClass]
public class RelationshipTests
{
    private CoreDbContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"RelationshipTestDb_{Guid.NewGuid()}")
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
    public async Task Organization_ToTeams_HasOneToManyRelationship()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team1 = new Team { Organization = org, Name = "Team 1" };
        var team2 = new Team { Organization = org, Name = "Team 2" };

        _context.Organizations.Add(org);
        _context.Teams.AddRange(team1, team2);
        await _context.SaveChangesAsync();

        // Act
        var retrievedOrg = await _context.Organizations
            .Include(o => o.Teams)
            .FirstAsync(o => o.Id == org.Id);

        // Assert
        Assert.IsNotNull(retrievedOrg.Teams, "Organization should have Teams collection");
        Assert.AreEqual(2, retrievedOrg.Teams.Count, "Organization should have 2 teams");
        Assert.IsTrue(retrievedOrg.Teams.All(t => t.OrganizationId == org.Id), "All teams should reference the organization");
    }

    [TestMethod]
    public async Task Team_ToOrganization_HasManyToOneRelationship()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team = new Team { Organization = org, Name = "Test Team" };

        _context.Organizations.Add(org);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        // Act
        var retrievedTeam = await _context.Teams
            .Include(t => t.Organization)
            .FirstAsync(t => t.Id == team.Id);

        // Assert
        Assert.IsNotNull(retrievedTeam.Organization, "Team should have Organization reference");
        Assert.AreEqual(org.Id, retrievedTeam.OrganizationId, "Team should reference correct organization");
        Assert.AreEqual("Test Org", retrievedTeam.Organization.Name, "Organization name should be accessible");
    }

    [TestMethod]
    public async Task TeamMember_ToTeamAndUser_HasProperReferences()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team = new Team { Organization = org, Name = "Test Team" };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            DisplayName = "Test User"
        };
        var teamMember = new TeamMember
        {
            Team = team,
            User = user,
            JoinedAt = DateTime.UtcNow,
            RoleIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };

        _context.Organizations.Add(org);
        _context.Teams.Add(team);
        _context.Users.Add(user);
        _context.TeamMembers.Add(teamMember);
        await _context.SaveChangesAsync();

        // Act
        var retrievedMember = await _context.TeamMembers
            .Include(tm => tm.Team)
            .Include(tm => tm.User)
            .FirstAsync(tm => tm.TeamId == team.Id && tm.UserId == user.Id);

        // Assert
        Assert.IsNotNull(retrievedMember.Team, "TeamMember should reference Team");
        Assert.IsNotNull(retrievedMember.User, "TeamMember should reference User");
        Assert.AreEqual(team.Id, retrievedMember.Team.Id, "Team reference should be correct");
        Assert.AreEqual(user.Id, retrievedMember.User.Id, "User reference should be correct");
        Assert.AreEqual(2, retrievedMember.RoleIds.Count, "RoleIds collection should be preserved");
    }

    [TestMethod]
    public async Task GroupMember_WithAddedByUser_HasAuditTrail()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var group = new Group { Organization = org, Name = "Test Group" };
        var member = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "member",
            DisplayName = "Member User"
        };
        var addedByUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
            DisplayName = "Admin User"
        };

        var groupMember = new GroupMember
        {
            Group = group,
            User = member,
            AddedByUser = addedByUser,
            AddedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);
        _context.Groups.Add(group);
        _context.Users.AddRange(member, addedByUser);
        _context.GroupMembers.Add(groupMember);
        await _context.SaveChangesAsync();

        // Act
        var retrieved = await _context.GroupMembers
            .Include(gm => gm.User)
            .Include(gm => gm.AddedByUser)
            .FirstAsync();

        // Assert
        Assert.IsNotNull(retrieved.AddedByUser, "AddedByUser audit trail should be preserved");
        Assert.AreEqual("Admin User", retrieved.AddedByUser.DisplayName, "Audit trail should contain correct user");
    }

    [TestMethod]
    public async Task OrganizationMember_WithInvitedByUser_HasAuditTrail()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var invitedUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "invited",
            DisplayName = "Invited User"
        };
        var inviterUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "inviter",
            DisplayName = "Inviter User"
        };

        var orgMember = new OrganizationMember
        {
            Organization = org,
            User = invitedUser,
            InvitedByUser = inviterUser,
            JoinedAt = DateTime.UtcNow,
            RoleIds = new List<Guid> { Guid.NewGuid() },
            IsActive = true
        };

        _context.Organizations.Add(org);
        _context.Users.AddRange(invitedUser, inviterUser);
        _context.OrganizationMembers.Add(orgMember);
        await _context.SaveChangesAsync();

        // Act
        var retrieved = await _context.OrganizationMembers
            .Include(om => om.User)
            .Include(om => om.InvitedByUser)
            .FirstAsync();

        // Assert
        Assert.IsNotNull(retrieved.InvitedByUser, "Invitation audit trail should be preserved");
        Assert.AreEqual("Inviter User", retrieved.InvitedByUser.DisplayName, "Inviter information should be correct");
    }

    [TestMethod]
    public async Task Organization_ToGroups_HasOneToManyRelationship()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var group1 = new Group { Organization = org, Name = "Group 1" };
        var group2 = new Group { Organization = org, Name = "Group 2" };

        _context.Organizations.Add(org);
        _context.Groups.AddRange(group1, group2);
        await _context.SaveChangesAsync();

        // Act
        var retrievedOrg = await _context.Organizations
            .Include(o => o.Groups)
            .FirstAsync(o => o.Id == org.Id);

        // Assert
        Assert.AreEqual(2, retrievedOrg.Groups.Count, "Organization should have 2 groups");
        Assert.IsTrue(retrievedOrg.Groups.All(g => g.OrganizationId == org.Id), "All groups should reference the organization");
    }

    [TestMethod]
    public async Task OrganizationMember_MultipleUsers_InMultipleOrganizations()
    {
        // Arrange
        var org1 = new Organization { Name = "Org 1" };
        var org2 = new Organization { Name = "Org 2" };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "multi-org-user",
            DisplayName = "Multi-Org User"
        };

        var member1 = new OrganizationMember
        {
            Organization = org1,
            User = user,
            JoinedAt = DateTime.UtcNow,
            RoleIds = new List<Guid> { Guid.NewGuid() },
            IsActive = true
        };
        var member2 = new OrganizationMember
        {
            Organization = org2,
            User = user,
            JoinedAt = DateTime.UtcNow,
            RoleIds = new List<Guid> { Guid.NewGuid() },
            IsActive = true
        };

        _context.Organizations.AddRange(org1, org2);
        _context.Users.Add(user);
        _context.OrganizationMembers.AddRange(member1, member2);
        await _context.SaveChangesAsync();

        // Act
        var userOrganizations = await _context.OrganizationMembers
            .Where(om => om.UserId == user.Id)
            .Include(om => om.Organization)
            .ToListAsync();

        // Assert
        Assert.AreEqual(2, userOrganizations.Count, "User should be member of 2 organizations");
        Assert.IsTrue(userOrganizations.Any(om => om.Organization.Name == "Org 1"), "User should be in Org 1");
        Assert.IsTrue(userOrganizations.Any(om => om.Organization.Name == "Org 2"), "User should be in Org 2");
    }

    [TestMethod]
    public async Task CascadeDelete_Organization_DeletesTeamsAndGroups()
    {
        // Arrange - Create org with teams and groups
        var org = new Organization { Name = "Test Org" };
        var team = new Team { Organization = org, Name = "Test Team" };
        var group = new Group { Organization = org, Name = "Test Group" };

        _context.Organizations.Add(org);
        _context.Teams.Add(team);
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        // Act - Delete organization
        _context.Organizations.Remove(org);
        await _context.SaveChangesAsync();

        // Assert - Teams and groups should be deleted via cascade
        var teams = await _context.Teams.IgnoreQueryFilters().ToListAsync();
        var groups = await _context.Groups.IgnoreQueryFilters().ToListAsync();

        Assert.AreEqual(0, teams.Count, "Teams should be deleted when organization is deleted");
        Assert.AreEqual(0, groups.Count, "Groups should be deleted when organization is deleted");
    }

    [TestMethod]
    public async Task CascadeDelete_Team_DeletesTeamMembers()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team = new Team { Organization = org, Name = "Test Team" };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            DisplayName = "Test User"
        };
        var teamMember = new TeamMember
        {
            Team = team,
            User = user,
            JoinedAt = DateTime.UtcNow,
            RoleIds = new List<Guid>()
        };

        _context.Organizations.Add(org);
        _context.Teams.Add(team);
        _context.Users.Add(user);
        _context.TeamMembers.Add(teamMember);
        await _context.SaveChangesAsync();

        var teamId = team.Id;

        // Act - Delete team
        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();

        // Assert
        var members = await _context.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .ToListAsync();
        Assert.AreEqual(0, members.Count, "TeamMembers should be deleted when team is deleted");
    }
}
