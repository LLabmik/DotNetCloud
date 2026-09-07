using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelService.GetRecentChannelsAsync"/>.
/// </summary>
[TestClass]
public class ChannelServiceGetRecentChannelsTests
{
    private ChatDbContext _db = null!;
    private ChannelService _service = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _realtimeMock = new Mock<IChatRealtimeService>();
        _service = new ChannelService(
            _db,
            new Mock<IEventBus>().Object,
            NullLogger<ChannelService>.Instance,
            _realtimeMock.Object);
        _caller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    /// <summary>
    /// Seeds the default "Public" channel with the caller as a member so
    /// <c>EnsureDefaultPublicChannelForUserAsync</c> does not create a second one.
    /// </summary>
    private async Task SeedDefaultPublicChannelAsync()
    {
        var channel = new Channel
        {
            Name = "Public",
            Type = ChannelType.Public,
            OrganizationId = null,
            CreatedByUserId = _caller.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = _caller.UserId });
        await _db.SaveChangesAsync();
    }

    private async Task<Channel> SeedChannelAsync(string name, DateTime? lastActivityAt, params Guid[] memberUserIds)
    {
        var channel = new Channel
        {
            Name = name,
            Type = ChannelType.Public,
            OrganizationId = null,
            CreatedByUserId = _caller.UserId,
            LastActivityAt = lastActivityAt,
            CreatedAt = lastActivityAt ?? DateTime.UtcNow
        };
        _db.Channels.Add(channel);
        foreach (var userId in memberUserIds)
            _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = userId });
        await _db.SaveChangesAsync();
        return channel;
    }

    [TestMethod]
    public async Task GetRecentChannels_MostRecentlyActiveFirst_ReturnsChannelsOrderedByLastActivityDescending()
    {
        await SeedDefaultPublicChannelAsync();
        var now = DateTime.UtcNow;

        await SeedChannelAsync("Alpha", now.AddDays(-3), _caller.UserId);
        await SeedChannelAsync("Beta", now.AddDays(-2), _caller.UserId);
        await SeedChannelAsync("Gamma", now.AddDays(-1), _caller.UserId);

        var result = await _service.GetRecentChannelsAsync(_caller);

        CollectionAssert.AreEqual(
            new[] { "Gamma", "Beta", "Alpha", "Public" },
            result.Select(c => c.Name).ToArray());
    }

    [TestMethod]
    public async Task GetRecentChannels_RespectsCount_ReturnsOnlyRequestedNumberOfMostRecent()
    {
        await SeedDefaultPublicChannelAsync();
        var now = DateTime.UtcNow;

        await SeedChannelAsync("Delta", now.AddDays(-4), _caller.UserId);
        await SeedChannelAsync("Echo", now.AddDays(-3), _caller.UserId);
        await SeedChannelAsync("Foxtrot", now.AddDays(-2), _caller.UserId);
        await SeedChannelAsync("Golf", now.AddDays(-1), _caller.UserId);

        var result = await _service.GetRecentChannelsAsync(_caller, count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "Golf", "Foxtrot" },
            result.Select(c => c.Name).ToArray());
    }

    [TestMethod]
    public async Task GetRecentChannels_OnlyChannelsWhereCallerIsMember_ExcludesOthers()
    {
        await SeedDefaultPublicChannelAsync();
        var now = DateTime.UtcNow;
        var otherUser = Guid.CreateVersion7();

        await SeedChannelAsync("mine", now.AddDays(-2), _caller.UserId);
        await SeedChannelAsync("notmine", now.AddDays(-1), otherUser);

        var result = await _service.GetRecentChannelsAsync(_caller);

        CollectionAssert.AreEquivalent(
            new[] { "Public", "mine" },
            result.Select(c => c.Name).ToArray());
        Assert.IsFalse(result.Any(c => c.Name == "notmine"));
    }

    [TestMethod]
    public async Task GetRecentChannels_NoChannels_ReturnsDefaultPublicChannel()
    {
        // Caller has no memberships yet; the service auto-provisions the default Public channel.
        var result = await _service.GetRecentChannelsAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Public", result[0].Name);

        var membership = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == result[0].Id && m.UserId == _caller.UserId);
        Assert.IsNotNull(membership);
    }

    private async Task<Channel> SeedDmChannelAsync(Guid otherUserId, DateTime? lastActivityAt)
    {
        var dm = new Channel
        {
            Name = $"DM-{_caller.UserId}-{otherUserId}",
            Type = ChannelType.DirectMessage,
            OrganizationId = null,
            CreatedByUserId = _caller.UserId,
            LastActivityAt = lastActivityAt,
            CreatedAt = lastActivityAt ?? DateTime.UtcNow
        };
        _db.Channels.Add(dm);
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = dm.Id, UserId = _caller.UserId });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = dm.Id, UserId = otherUserId });
        await _db.SaveChangesAsync();
        return dm;
    }

    private static Mock<IUserDirectory> CreateUserDirectoryMock(Dictionary<Guid, string> displayNames)
    {
        var userDirectory = new Mock<IUserDirectory>();
        userDirectory
            .Setup(ud => ud.GetDisplayNamesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Where(displayNames.ContainsKey).ToDictionary(id => id, id => displayNames[id]));
        return userDirectory;
    }

    [TestMethod]
    public async Task GetRecentChannels_DmWithHyphenatedGuidName_ResolvesToOtherUsersDisplayName()
    {
        await SeedDefaultPublicChannelAsync();
        var now = DateTime.UtcNow;
        var otherUser = Guid.CreateVersion7();
        var dm = await SeedDmChannelAsync(otherUser, now);

        // DM names store hyphenated GUIDs (DM-{guid1}-{guid2} → 11 dash segments).
        var userDirectory = CreateUserDirectoryMock(new Dictionary<Guid, string>
        {
            [otherUser] = "Alice Example"
        });
        var service = new ChannelService(
            _db,
            new Mock<IEventBus>().Object,
            NullLogger<ChannelService>.Instance,
            _realtimeMock.Object,
            userDirectory.Object);

        var result = await service.GetRecentChannelsAsync(_caller);

        var dmResult = result.Single(c => c.Id == dm.Id);
        Assert.AreEqual("Alice Example", dmResult.Name);
        Assert.IsFalse(dmResult.Name.StartsWith("DM-"));
    }

    [TestMethod]
    public async Task GetRecentChannels_DmWithOtherUserFirst_ResolvesToOtherUsersDisplayName()
    {
        await SeedDefaultPublicChannelAsync();
        var now = DateTime.UtcNow;
        var otherUser = Guid.CreateVersion7();

        // Name has the other user first: DM-{otherUser}-{caller}. Resolution must pick the caller's peer.
        var dm = new Channel
        {
            Name = $"DM-{otherUser}-{_caller.UserId}",
            Type = ChannelType.DirectMessage,
            OrganizationId = null,
            CreatedByUserId = otherUser,
            LastActivityAt = now,
            CreatedAt = now
        };
        _db.Channels.Add(dm);
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = dm.Id, UserId = _caller.UserId });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = dm.Id, UserId = otherUser });
        await _db.SaveChangesAsync();

        var userDirectory = CreateUserDirectoryMock(new Dictionary<Guid, string>
        {
            [otherUser] = "Bob Builder"
        });
        var service = new ChannelService(
            _db,
            new Mock<IEventBus>().Object,
            NullLogger<ChannelService>.Instance,
            _realtimeMock.Object,
            userDirectory.Object);

        var result = await service.GetRecentChannelsAsync(_caller);

        var dmResult = result.Single(c => c.Id == dm.Id);
        Assert.AreEqual("Bob Builder", dmResult.Name);
    }

    [TestMethod]
    public async Task GetRecentChannels_DmWithoutUserDirectory_UsesFallbackPrefix()
    {
        await SeedDefaultPublicChannelAsync();
        var now = DateTime.UtcNow;
        var otherUser = Guid.CreateVersion7();
        var dm = await SeedDmChannelAsync(otherUser, now);

        // No IUserDirectory (constructor default) → falls back to the other user's ID prefix.
        var result = await _service.GetRecentChannelsAsync(_caller);

        var dmResult = result.Single(c => c.Id == dm.Id);
        Assert.AreEqual(otherUser.ToString()[..8], dmResult.Name);
        Assert.IsFalse(dmResult.Name.StartsWith("DM-"));
    }
}
