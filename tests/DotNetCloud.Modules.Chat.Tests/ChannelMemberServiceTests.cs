using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
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
        _service = new ChannelMemberService(_db, _eventBusMock.Object, NullLogger<ChannelMemberService>.Instance);

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
}
