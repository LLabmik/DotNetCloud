using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="TeamManagerService"/>. Mirrors production by configuring
/// <see cref="QueryTrackingBehavior.NoTracking"/> so the tests fail if a team
/// update/soft-delete forgets to use <c>AsTracking()</c>.
/// </summary>
[TestClass]
public class TeamManagerServiceTests
{
    // Unique per test method: the InMemory store is keyed by name, so a fresh
    // name avoids cross-test pollution while still being reused between the
    // seed and verify contexts WITHIN a single test.
    private string _dbName = null!;

    private CoreDbContext CreateNoTrackingContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options,
            new PostgreSqlNamingStrategy());

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"TeamManagerServiceTests_{Guid.NewGuid():N}";
    }

    [TestMethod]
    public async Task UpdateTeamAsync_PersistsNameAndDescription()
    {
        Guid teamId;
        using (var seed = CreateNoTrackingContext())
        {
            var org = new Organization
            {
                Id = Guid.CreateVersion7(),
                Name = "Acme",
                CreatedAt = DateTime.UtcNow
            };
            var team = new Team
            {
                Id = Guid.CreateVersion7(),
                OrganizationId = org.Id,
                Name = "Engineering",
                Description = "Old description",
                CreatedAt = DateTime.UtcNow
            };
            seed.Organizations.Add(org);
            seed.Teams.Add(team);
            await seed.SaveChangesAsync();
            teamId = team.Id;

            var service = new TeamManagerService(seed);
            var result = await service.UpdateTeamAsync(teamId, "Platform", "New description");
            Assert.IsNotNull(result);
        }

        using var verify = CreateNoTrackingContext();
        var persisted = await verify.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId);
        Assert.IsNotNull(persisted);
        Assert.AreEqual("Platform", persisted!.Name);
        Assert.AreEqual("New description", persisted.Description);
    }

    [TestMethod]
    public async Task DeleteTeamAsync_PersistsSoftDelete()
    {
        Guid teamId;
        using (var seed = CreateNoTrackingContext())
        {
            var org = new Organization
            {
                Id = Guid.CreateVersion7(),
                Name = "Acme",
                CreatedAt = DateTime.UtcNow
            };
            var team = new Team
            {
                Id = Guid.CreateVersion7(),
                OrganizationId = org.Id,
                Name = "Support",
                CreatedAt = DateTime.UtcNow
            };
            seed.Organizations.Add(org);
            seed.Teams.Add(team);
            await seed.SaveChangesAsync();
            teamId = team.Id;

            var service = new TeamManagerService(seed);
            var result = await service.DeleteTeamAsync(teamId);
            Assert.IsTrue(result);
        }

        using var verify = CreateNoTrackingContext();
        var persisted = await verify.Teams
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId);
        Assert.IsNotNull(persisted);
        Assert.IsTrue(persisted!.IsDeleted);
        Assert.IsNotNull(persisted.DeletedAt);
    }
}
