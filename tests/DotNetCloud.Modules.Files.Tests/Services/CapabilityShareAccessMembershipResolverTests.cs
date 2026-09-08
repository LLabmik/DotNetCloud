using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Files.Data.Services;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="CapabilityShareAccessMembershipResolver"/> — verifies it
/// resolves team (and group) memberships through the core directory capabilities.
/// </summary>
[TestClass]
public class CapabilityShareAccessMembershipResolverTests
{
    private static TeamInfo CreateTeam(Guid id) => new()
    {
        Id = id,
        OrganizationId = Guid.CreateVersion7(),
        Name = "Team",
        MemberCount = 1,
        CreatedAt = DateTime.UtcNow,
    };

    private static GroupInfo CreateGroup(Guid id) => new()
    {
        Id = id,
        OrganizationId = Guid.CreateVersion7(),
        Name = "Group",
        MemberCount = 1,
        CreatedAt = DateTime.UtcNow,
    };

    [TestMethod]
    public async Task ResolveAsync_TeamAndGroupDirectories_ReturnsMemberships()
    {
        var teamId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTeam(teamId) });

        var groupDirectory = new Mock<IGroupDirectory>();
        groupDirectory
            .Setup(x => x.GetGroupsForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateGroup(groupId) });

        var resolver = new CapabilityShareAccessMembershipResolver(teamDirectory.Object, groupDirectory.Object);

        var result = await resolver.ResolveAsync(userId);

        CollectionAssert.AreEquivalent(new[] { teamId }, result.TeamIds.ToList());
        CollectionAssert.AreEquivalent(new[] { groupId }, result.GroupIds.ToList());
    }

    [TestMethod]
    public async Task ResolveAsync_NoDirectories_ReturnsEmptyMembership()
    {
        var resolver = new CapabilityShareAccessMembershipResolver();

        var result = await resolver.ResolveAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, result.TeamIds.Count);
        Assert.AreEqual(0, result.GroupIds.Count);
    }

    [TestMethod]
    public async Task ResolveAsync_DirectoryThrows_PropagatesException()
    {
        var teamDirectory = new Mock<ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("core unreachable"));

        var resolver = new CapabilityShareAccessMembershipResolver(teamDirectory.Object, null);

        // The resolver does not swallow directory failures — callers decide how to degrade.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(Guid.CreateVersion7()));
    }
}
