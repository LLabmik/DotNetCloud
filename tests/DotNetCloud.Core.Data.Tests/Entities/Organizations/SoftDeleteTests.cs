using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Entities.Organizations;

/// <summary>
/// Tests for soft-delete query filtering on Organization, Team, and Group entities.
/// </summary>
[TestClass]
public class SoftDeleteTests
{
    private CoreDbContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"SoftDeleteTestDb_{Guid.NewGuid()}")
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
    public async Task Organization_WhenDeleted_IsExcludedFromQueries()
    {
        // Arrange
        var org = new Organization
        {
            Name = "Test Org",
            Description = "Test Organization"
        };
        _context.Organizations.Add(org);
        await _context.SaveChangesAsync();

        // Act - Delete the organization
        org.IsDeleted = true;
        org.DeletedAt = DateTime.UtcNow;
        _context.Organizations.Update(org);
        await _context.SaveChangesAsync();

        // Assert - Deleted organization should not appear in normal queries
        var activeOrgs = await _context.Organizations.ToListAsync();
        Assert.AreEqual(0, activeOrgs.Count, "Deleted organization should be excluded from queries");

        // Assert - Can be retrieved with IgnoreQueryFilters
        var allOrgs = await _context.Organizations.IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(1, allOrgs.Count, "Deleted organization should be retrievable with IgnoreQueryFilters");
    }

    [TestMethod]
    public async Task Team_WhenDeleted_IsExcludedFromQueries()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team = new Team
        {
            Organization = org,
            Name = "Test Team"
        };
        _context.Organizations.Add(org);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        // Act - Delete the team
        team.IsDeleted = true;
        team.DeletedAt = DateTime.UtcNow;
        _context.Teams.Update(team);
        await _context.SaveChangesAsync();

        // Assert - Deleted team should not appear in normal queries
        var activeTeams = await _context.Teams.ToListAsync();
        Assert.AreEqual(0, activeTeams.Count, "Deleted team should be excluded from queries");

        // Assert - Can be retrieved with IgnoreQueryFilters
        var allTeams = await _context.Teams.IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(1, allTeams.Count, "Deleted team should be retrievable with IgnoreQueryFilters");
    }

    [TestMethod]
    public async Task Group_WhenDeleted_IsExcludedFromQueries()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var group = new Group
        {
            Organization = org,
            Name = "Test Group"
        };
        _context.Organizations.Add(org);
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        // Act - Delete the group
        group.IsDeleted = true;
        group.DeletedAt = DateTime.UtcNow;
        _context.Groups.Update(group);
        await _context.SaveChangesAsync();

        // Assert - Deleted group should not appear in normal queries
        var activeGroups = await _context.Groups.ToListAsync();
        Assert.AreEqual(0, activeGroups.Count, "Deleted group should be excluded from queries");

        // Assert - Can be retrieved with IgnoreQueryFilters
        var allGroups = await _context.Groups.IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(1, allGroups.Count, "Deleted group should be retrievable with IgnoreQueryFilters");
    }

    [TestMethod]
    public async Task SoftDeleteFilter_MixedDeletedAndActive_ReturnsOnlyActive()
    {
        // Arrange
        var org1 = new Organization { Name = "Active Org" };
        var org2 = new Organization { Name = "Deleted Org", IsDeleted = true, DeletedAt = DateTime.UtcNow };
        var org3 = new Organization { Name = "Another Active Org" };

        _context.Organizations.AddRange(org1, org2, org3);
        await _context.SaveChangesAsync();

        // Act
        var activeOrgs = await _context.Organizations.ToListAsync();

        // Assert
        Assert.AreEqual(2, activeOrgs.Count, "Should return only active organizations");
        Assert.IsFalse(activeOrgs.Any(o => o.Name == "Deleted Org"), "Should not include deleted organization");
    }

    [TestMethod]
    public async Task SoftDeleteFilter_WithIncludes_AppliesFilterToRelatedEntities()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team1 = new Team { Organization = org, Name = "Active Team" };
        var team2 = new Team { Organization = org, Name = "Deleted Team", IsDeleted = true, DeletedAt = DateTime.UtcNow };

        _context.Organizations.Add(org);
        _context.Teams.AddRange(team1, team2);
        await _context.SaveChangesAsync();

        // Act
        var orgWithTeams = await _context.Organizations
            .Include(o => o.Teams)
            .FirstAsync();

        // Assert
        Assert.AreEqual(1, orgWithTeams.Teams.Count, "Should include only active teams");
        Assert.IsTrue(orgWithTeams.Teams.All(t => !t.IsDeleted), "All included teams should be active");
    }

    [TestMethod]
    public async Task SoftDeleteFilter_DeleteTimestamp_IsSetCorrectly()
    {
        // Arrange
        var beforeDelete = DateTime.UtcNow;
        var org = new Organization { Name = "Test Org" };
        _context.Organizations.Add(org);
        await _context.SaveChangesAsync();

        // Act
        org.IsDeleted = true;
        org.DeletedAt = DateTime.UtcNow;
        _context.Organizations.Update(org);
        await _context.SaveChangesAsync();
        var afterDelete = DateTime.UtcNow;

        // Assert
        Assert.IsNotNull(org.DeletedAt, "DeletedAt should be set");
        Assert.IsTrue(org.DeletedAt >= beforeDelete && org.DeletedAt <= afterDelete, "DeletedAt should be within deletion timeframe");
    }

    [TestMethod]
    public async Task SoftDeleteFilter_CascadeDeleteRelatedTeams_SoftDeletesTeams()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        var team = new Team { Organization = org, Name = "Test Team" };

        _context.Organizations.Add(org);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        // Act - Delete organization
        org.IsDeleted = true;
        org.DeletedAt = DateTime.UtcNow;
        _context.Organizations.Update(org);
        await _context.SaveChangesAsync();

        // Assert - Queries should not return deleted org or its teams
        var activeOrgs = await _context.Organizations.ToListAsync();
        var activeTeams = await _context.Teams.ToListAsync();

        Assert.AreEqual(0, activeOrgs.Count, "Active organizations should be empty");
        Assert.AreEqual(0, activeTeams.Count, "Active teams should be empty");

        // Can retrieve with IgnoreQueryFilters
        var allOrgs = await _context.Organizations.IgnoreQueryFilters().ToListAsync();
        var allTeams = await _context.Teams.IgnoreQueryFilters().ToListAsync();

        Assert.AreEqual(1, allOrgs.Count, "Total organizations (including soft-deleted) should be 1");
        Assert.AreEqual(1, allTeams.Count, "Total teams (including soft-deleted) should be 1");
    }

    [TestMethod]
    public async Task SoftDeleteFilter_RestoreDeletedEntity_BecomesVisibleAgain()
    {
        // Arrange
        var org = new Organization { Name = "Test Org" };
        _context.Organizations.Add(org);
        await _context.SaveChangesAsync();

        // Act - Delete and then restore
        org.IsDeleted = true;
        org.DeletedAt = DateTime.UtcNow;
        _context.Organizations.Update(org);
        await _context.SaveChangesAsync();

        org.IsDeleted = false;
        org.DeletedAt = null;
        _context.Organizations.Update(org);
        await _context.SaveChangesAsync();

        // Assert
        var activeOrgs = await _context.Organizations.ToListAsync();
        Assert.AreEqual(1, activeOrgs.Count, "Restored organization should appear in queries");
        Assert.AreEqual("Test Org", activeOrgs[0].Name, "Retrieved organization should have correct data");
    }
}
