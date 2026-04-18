using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelMemberService"/>.
/// </summary>
[TestClass]
public class ChannelMemberServiceTests
{
    private ChatDbContext _db = null!;
    private ChannelMemberService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;

    private CallerContext _ownerCaller = null!;
    private CallerContext _adminCaller = null!;
    private CallerContext _memberCaller = null!;
    private CallerContext _outsiderCaller = null!;

    private Guid _channelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<IChatRealtimeService>();
        _service = new ChannelMemberService(_db, _eventBusMock.Object, NullLogger<ChannelMemberService>.Instance, _realtimeMock.Object);

        _ownerCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _adminCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _memberCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _outsiderCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        var channel = new Channel
        {
            Name = "general",
            Type = ChannelType.Public,
            CreatedByUserId = _ownerCaller.UserId
        };

        _db.Channels.Add(channel);
        _db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = channel.Id, UserId = _ownerCaller.UserId, Role = ChannelMemberRole.Owner },
            new ChannelMember { ChannelId = channel.Id, UserId = _adminCaller.UserId, Role = ChannelMemberRole.Admin },
            new ChannelMember { ChannelId = channel.Id, UserId = _memberCaller.UserId, Role = ChannelMemberRole.Member });

        await _db.SaveChangesAsync();
        _channelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task WhenOwnerAddsMemberThenMembershipIsCreated()
    {
        var newUserId = Guid.NewGuid();

        await _service.AddMemberAsync(_channelId, newUserId, _ownerCaller);

        var membership = await _db.ChannelMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChannelId == _channelId && m.UserId == newUserId);

        Assert.IsNotNull(membership);
        Assert.AreEqual(ChannelMemberRole.Member, membership.Role);
        _realtimeMock.Verify(
            r => r.AddUserToChannelGroupAsync(newUserId, _channelId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenNonAdminAddsMemberThenUnauthorizedAccessExceptionIsThrown()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AddMemberAsync(_channelId, Guid.NewGuid(), _memberCaller));
    }

    [TestMethod]
    public async Task WhenOutsiderListsMembersThenUnauthorizedAccessExceptionIsThrown()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ListMembersAsync(_channelId, _outsiderCaller));
    }

    [TestMethod]
    public async Task WhenOwnerDemotesLastOwnerThenInvalidOperationExceptionIsThrown()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateMemberRoleAsync(_channelId, _ownerCaller.UserId, ChannelMemberRole.Member, _ownerCaller));
    }

    [TestMethod]
    public async Task WhenCallerMarksReadWithInvalidMessageThenInvalidOperationExceptionIsThrown()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.MarkAsReadAsync(_channelId, Guid.NewGuid(), _memberCaller));
    }

    [TestMethod]
    public async Task WhenGetUnreadCountsThenMentionsIncludeAllAndChannelTypes()
    {
        var now = DateTime.UtcNow;

        var message1 = new Message
        {
            ChannelId = _channelId,
            SenderUserId = _ownerCaller.UserId,
            Content = "ping @all",
            SentAt = now
        };

        var message2 = new Message
        {
            ChannelId = _channelId,
            SenderUserId = _ownerCaller.UserId,
            Content = "ping @channel",
            SentAt = now.AddSeconds(1)
        };

        _db.Messages.AddRange(message1, message2);

        _db.MessageMentions.AddRange(
            new MessageMention
            {
                MessageId = message1.Id,
                Type = MentionType.All,
                StartIndex = 5,
                Length = 4
            },
            new MessageMention
            {
                MessageId = message2.Id,
                Type = MentionType.Channel,
                StartIndex = 5,
                Length = 8
            });

        await _db.SaveChangesAsync();

        var unread = await _service.GetUnreadCountsAsync(_memberCaller);
        var channelUnread = unread.Single(u => u.ChannelId == _channelId);

        Assert.AreEqual(2, channelUnread.UnreadCount);
        Assert.AreEqual(2, channelUnread.MentionCount);
    }

    [TestMethod]
    public async Task WhenRemovingLastOwnerThenInvalidOperationExceptionIsThrown()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RemoveMemberAsync(_channelId, _ownerCaller.UserId, _ownerCaller));
    }

    [TestMethod]
    public async Task WhenAdminRemovesMemberThenRealtimeGroupMembershipIsRemoved()
    {
        await _service.RemoveMemberAsync(_channelId, _memberCaller.UserId, _adminCaller);

        _realtimeMock.Verify(
            r => r.RemoveUserFromChannelGroupAsync(_memberCaller.UserId, _channelId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

// ══════════════════════════════════════════════════════════════════════
//  DM → Group Auto-Conversion Tests (Phase A3)
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// Tests for the DM → Group channel auto-conversion logic in <see cref="ChannelMemberService"/>.
/// When a 3rd member is added to a <see cref="ChannelType.DirectMessage"/> channel, the channel
/// type must automatically escalate to <see cref="ChannelType.Group"/>.
/// </summary>
[TestClass]
public class DmToGroupConversionTests
{
    private ChatDbContext _db = null!;
    private ChannelMemberService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;

    private CallerContext _ownerCaller = null!;
    private CallerContext _member2Caller = null!;
    private Guid _dmChannelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<IChatRealtimeService>();
        _service = new ChannelMemberService(_db, _eventBusMock.Object, NullLogger<ChannelMemberService>.Instance, _realtimeMock.Object);

        _ownerCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _member2Caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        // Seed a 2-person DM channel (owner + one member)
        var channel = new Channel
        {
            Name = "DM",
            Type = ChannelType.DirectMessage,
            CreatedByUserId = _ownerCaller.UserId
        };

        _db.Channels.Add(channel);
        _db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = channel.Id, UserId = _ownerCaller.UserId, Role = ChannelMemberRole.Owner },
            new ChannelMember { ChannelId = channel.Id, UserId = _member2Caller.UserId, Role = ChannelMemberRole.Member });

        await _db.SaveChangesAsync();
        _dmChannelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ── Conversion triggers ─────────────────────────────────────────

    [TestMethod]
    public async Task AddMember_ThirdPersonToDm_ChangesTypeToGroup()
    {
        var thirdUser = Guid.NewGuid();

        await _service.AddMemberAsync(_dmChannelId, thirdUser, _ownerCaller);

        var channel = await _db.Channels.FindAsync(_dmChannelId);
        Assert.AreEqual(ChannelType.Group, channel!.Type);
    }

    [TestMethod]
    public async Task AddMember_ThirdPersonToDm_ThirdMemberIsInChannel()
    {
        var thirdUser = Guid.NewGuid();

        await _service.AddMemberAsync(_dmChannelId, thirdUser, _ownerCaller);

        var member = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.ChannelId == _dmChannelId && m.UserId == thirdUser);

        Assert.IsNotNull(member);
        Assert.AreEqual(ChannelMemberRole.Member, member.Role);
    }

    [TestMethod]
    public async Task AddMember_ThirdPersonToDm_ExistingMembersRetained()
    {
        var thirdUser = Guid.NewGuid();

        await _service.AddMemberAsync(_dmChannelId, thirdUser, _ownerCaller);

        var memberCount = await _db.ChannelMembers
            .CountAsync(m => m.ChannelId == _dmChannelId);

        Assert.AreEqual(3, memberCount);
    }

    [TestMethod]
    public async Task AddMember_ThirdPersonToDm_OwnerRolePreserved()
    {
        var thirdUser = Guid.NewGuid();

        await _service.AddMemberAsync(_dmChannelId, thirdUser, _ownerCaller);

        var ownerMember = await _db.ChannelMembers
            .FirstAsync(m => m.ChannelId == _dmChannelId && m.UserId == _ownerCaller.UserId);

        Assert.AreEqual(ChannelMemberRole.Owner, ownerMember.Role);
    }

    [TestMethod]
    public async Task AddMember_ThirdPersonToDm_SecondMemberRolePreserved()
    {
        var thirdUser = Guid.NewGuid();

        await _service.AddMemberAsync(_dmChannelId, thirdUser, _ownerCaller);

        var member2 = await _db.ChannelMembers
            .FirstAsync(m => m.ChannelId == _dmChannelId && m.UserId == _member2Caller.UserId);

        Assert.AreEqual(ChannelMemberRole.Member, member2.Role);
    }

    // ── No conversion when not a DM ────────────────────────────────

    [TestMethod]
    public async Task AddMember_ThirdPersonToPublicChannel_TypeRemainsPublic()
    {
        // Create a public channel with 2 members
        var publicOwner = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var publicChannel = new Channel
        {
            Name = "Public",
            Type = ChannelType.Public,
            CreatedByUserId = publicOwner.UserId
        };
        _db.Channels.Add(publicChannel);
        _db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = publicChannel.Id, UserId = publicOwner.UserId, Role = ChannelMemberRole.Owner },
            new ChannelMember { ChannelId = publicChannel.Id, UserId = Guid.NewGuid(), Role = ChannelMemberRole.Member });
        await _db.SaveChangesAsync();

        await _service.AddMemberAsync(publicChannel.Id, Guid.NewGuid(), publicOwner);

        var channel = await _db.Channels.FindAsync(publicChannel.Id);
        Assert.AreEqual(ChannelType.Public, channel!.Type);
    }

    [TestMethod]
    public async Task AddMember_ThirdPersonToGroupChannel_TypeRemainsGroup()
    {
        // Create a Group channel (already converted) with 2 members
        var groupOwner = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var groupChannel = new Channel
        {
            Name = "Group",
            Type = ChannelType.Group,
            CreatedByUserId = groupOwner.UserId
        };
        _db.Channels.Add(groupChannel);
        _db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = groupChannel.Id, UserId = groupOwner.UserId, Role = ChannelMemberRole.Owner },
            new ChannelMember { ChannelId = groupChannel.Id, UserId = Guid.NewGuid(), Role = ChannelMemberRole.Member });
        await _db.SaveChangesAsync();

        await _service.AddMemberAsync(groupChannel.Id, Guid.NewGuid(), groupOwner);

        var channel = await _db.Channels.FindAsync(groupChannel.Id);
        Assert.AreEqual(ChannelType.Group, channel!.Type);
    }

    // ── No conversion for 2nd member ──────────────────────────────

    [TestMethod]
    public async Task AddMember_SecondPersonToDm_TypeRemainsDm()
    {
        // Create a DM channel with only one member
        var soloOwner = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var soloChannel = new Channel
        {
            Name = "Solo DM",
            Type = ChannelType.DirectMessage,
            CreatedByUserId = soloOwner.UserId
        };
        _db.Channels.Add(soloChannel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = soloChannel.Id,
            UserId = soloOwner.UserId,
            Role = ChannelMemberRole.Owner
        });
        await _db.SaveChangesAsync();

        await _service.AddMemberAsync(soloChannel.Id, Guid.NewGuid(), soloOwner);

        var channel = await _db.Channels.FindAsync(soloChannel.Id);
        Assert.AreEqual(ChannelType.DirectMessage, channel!.Type);
    }

    // ── Idempotency: duplicate add does not convert again ─────────

    [TestMethod]
    public async Task AddMember_DuplicateAdd_DoesNotConvertTwice()
    {
        var thirdUser = Guid.NewGuid();

        // First add triggers conversion
        await _service.AddMemberAsync(_dmChannelId, thirdUser, _ownerCaller);

        // Add a 4th member — channel is now Group, should remain Group
        await _service.AddMemberAsync(_dmChannelId, Guid.NewGuid(), _ownerCaller);

        var channel = await _db.Channels.FindAsync(_dmChannelId);
        Assert.AreEqual(ChannelType.Group, channel!.Type);

        // Member count should be 4
        var count = await _db.ChannelMembers.CountAsync(m => m.ChannelId == _dmChannelId);
        Assert.AreEqual(4, count);
    }

    [TestMethod]
    public async Task AddMember_AlreadyMember_DoesNotConvert()
    {
        // Add existing member2 again — idempotent, no conversion expected
        // (channel still has only 2 unique members)
        await _service.AddMemberAsync(_dmChannelId, _member2Caller.UserId, _ownerCaller);

        var channel = await _db.Channels.FindAsync(_dmChannelId);
        // Still DirectMessage — the member count didn't increase
        Assert.AreEqual(ChannelType.DirectMessage, channel!.Type);

        var count = await _db.ChannelMembers.CountAsync(m => m.ChannelId == _dmChannelId);
        Assert.AreEqual(2, count);
    }
}
