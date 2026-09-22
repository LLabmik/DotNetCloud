using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Auth.Tests.Helpers;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Organizations;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Tests.Capabilities;

/// <summary>
/// Pins the per-operation context contract for the directory services that Blazor components hold.
/// </summary>
/// <remarks>
/// This service set is scoped, but the circuit resolving it is long-lived, so components interleave
/// their initializers on one instance. Sharing a context there throws "a second operation was started
/// on this context instance"; these tests assert the property that removes it - every operation gets
/// its own context, and that context is released.
/// </remarks>
[TestClass]
public class DirectoryServiceContextTests
{
    private CoreDbContext _dbContext = null!;
    private InMemoryCoreDbContextFactory _factory = null!;

    [TestInitialize]
    public void Setup()
    {
        var databaseName = $"DirectoryContextTests_{Guid.CreateVersion7()}";
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        _dbContext = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _factory = new InMemoryCoreDbContextFactory(databaseName);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task TeamDirectory_TwoCalls_EachUseTheirOwnContext()
    {
        var service = new TeamDirectoryService(_factory);
        var teamId = Guid.CreateVersion7();

        var first = await service.GetTeamMembersAsync(teamId);
        var second = await service.GetTeamMembersAsync(teamId);

        Assert.AreEqual(0, first.Count);
        Assert.AreEqual(0, second.Count);
        Assert.AreEqual(2, _factory.CreatedContexts.Count);
        Assert.AreEqual(2, _factory.CreatedContexts.Distinct().Count());
        Assert.ThrowsExactly<ObjectDisposedException>(() => _factory.CreatedContexts[0].Teams.ToList());
    }

    [TestMethod]
    public async Task OrganizationDirectory_TwoCalls_EachUseTheirOwnContext()
    {
        var service = new OrganizationDirectoryService(_factory);
        var organizationId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var first = await service.IsOrganizationMemberAsync(organizationId, userId);
        var second = await service.HasOrgRoleAsync(organizationId, userId, Guid.CreateVersion7());

        Assert.IsFalse(first);
        Assert.IsFalse(second);
        Assert.AreEqual(2, _factory.CreatedContexts.Count);
        Assert.AreEqual(2, _factory.CreatedContexts.Distinct().Count());
        Assert.ThrowsExactly<ObjectDisposedException>(() => _factory.CreatedContexts[0].Set<OrganizationMember>().ToList());
    }

    [TestMethod]
    public async Task GroupDirectory_TwoCalls_EachUseTheirOwnContext()
    {
        var service = new GroupDirectoryService(_factory);
        var groupId = Guid.CreateVersion7();

        var first = await service.GetGroupMembersAsync(groupId);
        var second = await service.GetGroupAsync(groupId);

        Assert.AreEqual(0, first.Count);
        Assert.IsNull(second);
        Assert.AreEqual(2, _factory.CreatedContexts.Count);
        Assert.AreEqual(2, _factory.CreatedContexts.Distinct().Count());
    }
}
