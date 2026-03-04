using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="MentionNotificationService"/>.
/// </summary>
[TestClass]
public class MentionNotificationServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IPushNotificationService> _pushMock = null!;
    private Mock<IUserDirectory> _userDirectoryMock = null!;
    private MentionNotificationService _service = null!;
    private Guid _channelId;
    private Guid _senderUserId;
    private Guid _memberUserId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);

        _realtimeMock = new Mock<IChatRealtimeService>();
        _pushMock = new Mock<IPushNotificationService>();
        _userDirectoryMock = new Mock<IUserDirectory>();
        _userDirectoryMock
            .Setup(ud => ud.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _service = new MentionNotificationService(
            _db, _realtimeMock.Object, _pushMock.Object,
            NullLogger<MentionNotificationService>.Instance,
            _userDirectoryMock.Object);

        _senderUserId = Guid.NewGuid();
        _memberUserId = Guid.NewGuid();

        // Create a channel with sender and member
        var channel = new Channel { Name = "test-channel", CreatedByUserId = _senderUserId };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _senderUserId,
            Role = ChannelMemberRole.Owner
        });
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _memberUserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();
        _channelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task WhenNoMentionsThenNoNotificationsSent()
    {
        await _service.DispatchMentionNotificationsAsync(
            Guid.NewGuid(), _channelId, _senderUserId, [], CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pushMock.Verify(
            p => p.SendAsync(It.IsAny<Guid>(), It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenAtAllMentionThenAllMembersExceptSenderAreNotified()
    {
        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        // Member should be notified, sender should not
        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_memberUserId, _channelId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_senderUserId, _channelId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenAtChannelMentionThenAllMembersExceptSenderAreNotified()
    {
        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.Channel, StartIndex = 0, Length = 8 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_memberUserId, _channelId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_senderUserId, _channelId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenUserMentionThenOnlyMentionedUserIsNotified()
    {
        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.User, MentionedUserId = _memberUserId, StartIndex = 0, Length = 6 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_memberUserId, _channelId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenUserMentionsSenderThenSenderIsNotNotified()
    {
        var messageId = Guid.NewGuid();
        // Sender mentions themselves
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.User, MentionedUserId = _senderUserId, StartIndex = 0, Length = 6 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pushMock.Verify(
            p => p.SendAsync(It.IsAny<Guid>(), It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenMemberIsMutedThenNoNotificationSent()
    {
        // Mute the member
        var membership = await _db.ChannelMembers
            .FirstAsync(cm => cm.UserId == _memberUserId && cm.ChannelId == _channelId);
        membership.IsMuted = true;
        await _db.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_memberUserId, _channelId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenMemberHasNoneNotificationPrefThenNoNotificationSent()
    {
        var membership = await _db.ChannelMembers
            .FirstAsync(cm => cm.UserId == _memberUserId && cm.ChannelId == _channelId);
        membership.NotificationPref = NotificationPreference.None;
        await _db.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.User, MentionedUserId = _memberUserId, StartIndex = 0, Length = 6 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_memberUserId, _channelId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenPushNotificationSentThenCategoryIsChatMention()
    {
        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _pushMock.Verify(
            p => p.SendAsync(
                _memberUserId,
                It.Is<PushNotification>(n => n.Category == NotificationCategory.ChatMention),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenPushNotificationSentThenDataContainsChannelAndMessageIds()
    {
        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _pushMock.Verify(
            p => p.SendAsync(
                _memberUserId,
                It.Is<PushNotification>(n =>
                    n.Data.ContainsKey("channelId") && n.Data["channelId"] == _channelId.ToString() &&
                    n.Data.ContainsKey("messageId") && n.Data["messageId"] == messageId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenUserDirectoryProvidesSenderNameThenNotificationTitleContainsIt()
    {
        _userDirectoryMock
            .Setup(ud => ud.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_senderUserId] = "Alice" });

        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _pushMock.Verify(
            p => p.SendAsync(
                _memberUserId,
                It.Is<PushNotification>(n => n.Title.Contains("Alice")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenNoUserDirectoryThenFallbackSenderNameIsUsed()
    {
        var service = new MentionNotificationService(
            _db, _realtimeMock.Object, _pushMock.Object,
            NullLogger<MentionNotificationService>.Instance,
            userDirectory: null);

        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _pushMock.Verify(
            p => p.SendAsync(
                _memberUserId,
                It.Is<PushNotification>(n => n.Title.Contains("Someone")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenMultipleMembersExistThenAllEligibleAreNotified()
    {
        // Add a third member
        var thirdUserId = Guid.NewGuid();
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = _channelId,
            UserId = thirdUserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.All, StartIndex = 0, Length = 4 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        // Both non-sender members should be notified
        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_memberUserId, _channelId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(thirdUserId, _channelId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(_senderUserId, _channelId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenUserMentionedIsNotChannelMemberThenNoNotificationSent()
    {
        var nonMemberId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var mentions = new List<MessageMention>
        {
            new() { MessageId = messageId, Type = MentionType.User, MentionedUserId = nonMemberId, StartIndex = 0, Length = 6 }
        };

        await _service.DispatchMentionNotificationsAsync(
            messageId, _channelId, _senderUserId, mentions, CancellationToken.None);

        _realtimeMock.Verify(
            r => r.BroadcastUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
