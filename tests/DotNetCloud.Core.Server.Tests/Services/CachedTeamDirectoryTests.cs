using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Services;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the <see cref="CachedTeamDirectory"/> short-lived membership cache.
/// </summary>
[TestClass]
public class CachedTeamDirectoryTests
{
    private static TeamInfo CreateTeam(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        OrganizationId = Guid.CreateVersion7(),
        Name = name,
        Description = null,
        MemberCount = 1,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static TeamMemberInfo CreateMember(Guid teamId) => new()
    {
        TeamId = teamId,
        UserId = Guid.CreateVersion7(),
        JoinedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [TestMethod]
    public async Task GetTeamsForUserAsync_WithinTtl_ReturnsCachedValueAndCallsInnerOnce()
    {
        var team = CreateTeam("Engineering");
        var inner = new Mock<ITeamDirectory>();
        inner
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { team });

        var clock = new ManualTimeProvider();
        var cache = new CachedTeamDirectory(inner.Object, TimeSpan.FromSeconds(30), timeProvider: clock);

        var userId = Guid.CreateVersion7();
        var first = await cache.GetTeamsForUserAsync(userId);
        var second = await cache.GetTeamsForUserAsync(userId);

        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(team.Name, second[0].Name);
        inner.Verify(
            x => x.GetTeamsForUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetTeamsForUserAsync_AfterTtl_RefreshesFromInner()
    {
        var team1 = CreateTeam("Engineering");
        var team2 = CreateTeam("Design");
        var inner = new Mock<ITeamDirectory>();
        var callCount = 0;
        inner
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? new[] { team1 } : new[] { team1, team2 };
            });

        var clock = new ManualTimeProvider();
        var cache = new CachedTeamDirectory(inner.Object, TimeSpan.FromSeconds(30), timeProvider: clock);

        var userId = Guid.CreateVersion7();
        var first = await cache.GetTeamsForUserAsync(userId);
        Assert.AreEqual(1, first.Count);

        // Advance beyond the TTL — a subsequent call must hit the inner directory again.
        clock.Advance(TimeSpan.FromSeconds(31));

        var second = await cache.GetTeamsForUserAsync(userId);
        Assert.AreEqual(2, second.Count);

        inner.Verify(
            x => x.GetTeamsForUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetTeamAsync_CachesResultWithinTtl()
    {
        var team = CreateTeam("Engineering");
        var inner = new Mock<ITeamDirectory>();
        inner
            .Setup(x => x.GetTeamAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var cache = new CachedTeamDirectory(inner.Object, TimeSpan.FromSeconds(30), timeProvider: new ManualTimeProvider());

        var first = await cache.GetTeamAsync(team.Id);
        var second = await cache.GetTeamAsync(team.Id);

        Assert.IsNotNull(first);
        Assert.AreEqual(team.Name, second!.Name);
        inner.Verify(
            x => x.GetTeamAsync(team.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetTeamAsync_NotFoundIsCachedThenRefreshedAfterTtl()
    {
        var team = CreateTeam("Engineering");
        var inner = new Mock<ITeamDirectory>();
        inner
            .Setup(x => x.GetTeamAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id == team.Id ? team : null);

        var clock = new ManualTimeProvider();
        var cache = new CachedTeamDirectory(inner.Object, TimeSpan.FromSeconds(30), timeProvider: clock);

        var unknownId = Guid.CreateVersion7();
        var first = await cache.GetTeamAsync(unknownId);
        var second = await cache.GetTeamAsync(unknownId);
        Assert.IsNull(first);
        Assert.IsNull(second);

        // Both hits served from cache → inner called once for the not-found result.
        inner.Verify(
            x => x.GetTeamAsync(unknownId, It.IsAny<CancellationToken>()),
            Times.Once);

        clock.Advance(TimeSpan.FromSeconds(31));

        var afterTtl = await cache.GetTeamAsync(unknownId);
        Assert.IsNull(afterTtl);
        inner.Verify(
            x => x.GetTeamAsync(unknownId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetTeamMembersAsync_PassesThroughToInner()
    {
        var teamId = Guid.CreateVersion7();
        var inner = new Mock<ITeamDirectory>();
        inner
            .Setup(x => x.GetTeamMembersAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateMember(teamId) });

        var cache = new CachedTeamDirectory(inner.Object, TimeSpan.FromSeconds(30), timeProvider: new ManualTimeProvider());

        var result = await cache.GetTeamMembersAsync(teamId);

        Assert.AreEqual(1, result.Count);
        inner.Verify(
            x => x.GetTeamMembersAsync(teamId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A manually-advanceable <see cref="TimeProvider"/> for testing TTL expiry.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        private long _ticks;

        /// <inheritdoc />
        public override long GetTimestamp() => _ticks;

        /// <summary>Advances the fake clock by the given amount.</summary>
        public void Advance(TimeSpan amount)
            => _ticks += checked((long)(amount.TotalSeconds * TimestampFrequency));
    }
}
