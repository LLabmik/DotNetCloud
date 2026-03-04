using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="MessageService"/>.
/// </summary>
[TestClass]
public class MessageServiceTests
{
    private ChatDbContext _db = null!;
    private MessageService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;
    private Guid _channelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new MessageService(_db, _eventBusMock.Object, NullLogger<MessageService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        // Create a channel and add caller as member
        var channel = new Channel { Name = "test", CreatedByUserId = _caller.UserId };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Owner
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
    public async Task WhenSendMessageThenMessageIsReturned()
    {
        var dto = new SendMessageDto { Content = "Hello world" };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Hello world", result.Content);
        Assert.AreEqual(_channelId, result.ChannelId);
    }

    [TestMethod]
    public async Task WhenSendMessageThenEventIsPublished()
    {
        var dto = new SendMessageDto { Content = "Hello" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<Events.MessageSentEvent>(), _caller, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenSendMessageThenChannelActivityIsUpdated()
    {
        var dto = new SendMessageDto { Content = "Activity test" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        var channel = await _db.Channels.FindAsync(_channelId);
        Assert.IsNotNull(channel!.LastActivityAt);
    }

    [TestMethod]
    public async Task WhenSendMessageWithEmptyContentThenThrows()
    {
        var dto = new SendMessageDto { Content = "" };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendMessageAsync(_channelId, dto, _caller));
    }

    [TestMethod]
    public async Task WhenSendMessageAsNonMemberThenThrows()
    {
        var nonMember = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var dto = new SendMessageDto { Content = "Should fail" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SendMessageAsync(_channelId, dto, nonMember));
    }

    [TestMethod]
    public async Task WhenEditMessageThenContentIsUpdated()
    {
        var sendDto = new SendMessageDto { Content = "Original" };
        var msg = await _service.SendMessageAsync(_channelId, sendDto, _caller);

        var editDto = new EditMessageDto { Content = "Edited" };
        var result = await _service.EditMessageAsync(msg.Id, editDto, _caller);

        Assert.AreEqual("Edited", result.Content);
        Assert.IsTrue(result.IsEdited);
    }

    [TestMethod]
    public async Task WhenEditMessageByOtherUserThenThrows()
    {
        var sendDto = new SendMessageDto { Content = "Original" };
        var msg = await _service.SendMessageAsync(_channelId, sendDto, _caller);

        var otherUser = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var editDto = new EditMessageDto { Content = "Hacked" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.EditMessageAsync(msg.Id, editDto, otherUser));
    }

    [TestMethod]
    public async Task WhenDeleteMessageThenMessageIsSoftDeleted()
    {
        var sendDto = new SendMessageDto { Content = "Delete me" };
        var msg = await _service.SendMessageAsync(_channelId, sendDto, _caller);

        await _service.DeleteMessageAsync(msg.Id, _caller);

        var message = await _db.Messages.FindAsync(msg.Id);
        Assert.IsTrue(message!.IsDeleted);
        Assert.IsNotNull(message.DeletedAt);
    }

    [TestMethod]
    public async Task WhenGetMessagesThenPaginatedResultIsReturned()
    {
        for (var i = 0; i < 5; i++)
        {
            await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = $"msg {i}" }, _caller);
        }

        var result = await _service.GetMessagesAsync(_channelId, 1, 3, _caller);

        Assert.AreEqual(3, result.Items.Count);
        Assert.AreEqual(5, result.TotalItems);
        Assert.AreEqual(2, result.TotalPages);
    }

    [TestMethod]
    public async Task WhenSearchMessagesThenMatchingMessagesAreReturned()
    {
        await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = "hello world" }, _caller);
        await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = "goodbye" }, _caller);
        await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = "hello again" }, _caller);

        var result = await _service.SearchMessagesAsync(_channelId, "hello", 1, 50, _caller);

        Assert.AreEqual(2, result.TotalItems);
    }

    [TestMethod]
    public async Task WhenGetNonExistentMessageThenReturnsNull()
    {
        var result = await _service.GetMessageAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenSendReplyThenReplyTypeIsSet()
    {
        var original = await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = "Original" }, _caller);
        var reply = await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = "Reply", ReplyToMessageId = original.Id }, _caller);

        Assert.AreEqual("Reply", reply.Type);
        Assert.AreEqual(original.Id, reply.ReplyToMessageId);
    }

    // ── Mention parsing tests ───────────────────────────────────────

    [TestMethod]
    public async Task WhenSendMessageWithAtAllThenAllMentionIsStored()
    {
        var dto = new SendMessageDto { Content = "Hey @all check this out" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(1, mentions.Count);
        Assert.AreEqual(MentionType.All, mentions[0].Type);
        Assert.IsNull(mentions[0].MentionedUserId);
    }

    [TestMethod]
    public async Task WhenSendMessageWithAtChannelThenChannelMentionIsStored()
    {
        var dto = new SendMessageDto { Content = "Attention @channel please read" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(1, mentions.Count);
        Assert.AreEqual(MentionType.Channel, mentions[0].Type);
        Assert.IsNull(mentions[0].MentionedUserId);
    }

    [TestMethod]
    public async Task WhenSendMessageWithMultipleMentionsThenAllAreStored()
    {
        var dto = new SendMessageDto { Content = "@all and @channel in one message" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(2, mentions.Count);
        Assert.IsTrue(mentions.Any(m => m.Type == MentionType.All));
        Assert.IsTrue(mentions.Any(m => m.Type == MentionType.Channel));
    }

    [TestMethod]
    public async Task WhenSendMessageWithAtAllThenStartIndexIsCorrect()
    {
        var dto = new SendMessageDto { Content = "Hey @all done" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        var mention = await _db.MessageMentions.SingleAsync();
        Assert.AreEqual(4, mention.StartIndex);
        Assert.AreEqual(4, mention.Length);
    }

    [TestMethod]
    public async Task WhenSendMessageWithNoMentionsThenNoMentionsStored()
    {
        var dto = new SendMessageDto { Content = "Just a regular message" };

        await _service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(0, mentions.Count);
    }

    [TestMethod]
    public async Task WhenSendMessageWithAtUsernameThenUserMentionIsStored()
    {
        var targetUserId = Guid.NewGuid();
        var userDirectoryMock = new Mock<IUserDirectory>();
        userDirectoryMock
            .Setup(ud => ud.FindUserIdByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUserId);

        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            userDirectoryMock.Object);

        var dto = new SendMessageDto { Content = "Hey @alice check this" };

        await service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(1, mentions.Count);
        Assert.AreEqual(MentionType.User, mentions[0].Type);
        Assert.AreEqual(targetUserId, mentions[0].MentionedUserId);
    }

    [TestMethod]
    public async Task WhenSendMessageWithUnknownUsernameThenNoMentionStored()
    {
        var userDirectoryMock = new Mock<IUserDirectory>();
        userDirectoryMock
            .Setup(ud => ud.FindUserIdByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            userDirectoryMock.Object);

        var dto = new SendMessageDto { Content = "Hey @nonexistent check this" };

        await service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(0, mentions.Count);
    }

    [TestMethod]
    public async Task WhenSendMessageWithMixedMentionsThenAllTypesAreStored()
    {
        var aliceId = Guid.NewGuid();
        var userDirectoryMock = new Mock<IUserDirectory>();
        userDirectoryMock
            .Setup(ud => ud.FindUserIdByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(aliceId);
        userDirectoryMock
            .Setup(ud => ud.FindUserIdByUsernameAsync("all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null); // @all should be handled as MentionType.All, not user lookup

        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            userDirectoryMock.Object);

        var dto = new SendMessageDto { Content = "@all please review @alice" };

        await service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.OrderBy(m => m.StartIndex).ToListAsync();
        Assert.AreEqual(2, mentions.Count);
        Assert.AreEqual(MentionType.All, mentions[0].Type);
        Assert.AreEqual(MentionType.User, mentions[1].Type);
        Assert.AreEqual(aliceId, mentions[1].MentionedUserId);
    }

    [TestMethod]
    public async Task WhenSendMessageWithNoUserDirectoryThenUsernamesAreIgnored()
    {
        // No IUserDirectory injected — @username mentions should be silently skipped
        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            userDirectory: null);

        var dto = new SendMessageDto { Content = "Hey @alice and @all" };

        await service.SendMessageAsync(_channelId, dto, _caller);

        var mentions = await _db.MessageMentions.ToListAsync();
        // Only @all should be stored; @alice should be skipped (no user directory)
        Assert.AreEqual(1, mentions.Count);
        Assert.AreEqual(MentionType.All, mentions[0].Type);
    }

    [TestMethod]
    public async Task WhenSendMessageWithMentionsThenNotificationServiceIsCalled()
    {
        var mentionNotifierMock = new Mock<IMentionNotificationService>();
        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            mentionNotifier: mentionNotifierMock.Object);

        var dto = new SendMessageDto { Content = "Hey @all check this" };

        await service.SendMessageAsync(_channelId, dto, _caller);

        mentionNotifierMock.Verify(
            mn => mn.DispatchMentionNotificationsAsync(
                It.IsAny<Guid>(), _channelId, _caller.UserId,
                It.Is<IReadOnlyList<MessageMention>>(m => m.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenSendMessageWithNoMentionsThenNotificationServiceIsNotCalled()
    {
        var mentionNotifierMock = new Mock<IMentionNotificationService>();
        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            mentionNotifier: mentionNotifierMock.Object);

        var dto = new SendMessageDto { Content = "No mentions here" };

        await service.SendMessageAsync(_channelId, dto, _caller);

        mentionNotifierMock.Verify(
            mn => mn.DispatchMentionNotificationsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<MessageMention>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenEditMessageWithNewMentionsThenOldMentionsAreReplacedAndNotificationDispatched()
    {
        var mentionNotifierMock = new Mock<IMentionNotificationService>();
        var service = new MessageService(
            _db, _eventBusMock.Object, NullLogger<MessageService>.Instance,
            mentionNotifier: mentionNotifierMock.Object);

        // Send original without mentions
        var msg = await service.SendMessageAsync(_channelId, new SendMessageDto { Content = "Original" }, _caller);
        var originalMentions = await _db.MessageMentions.CountAsync();
        Assert.AreEqual(0, originalMentions);

        // Edit to include @all
        await service.EditMessageAsync(msg.Id, new EditMessageDto { Content = "Updated @all" }, _caller);

        var newMentions = await _db.MessageMentions.ToListAsync();
        Assert.AreEqual(1, newMentions.Count);
        Assert.AreEqual(MentionType.All, newMentions[0].Type);

        // Notification should fire for the edit too
        mentionNotifierMock.Verify(
            mn => mn.DispatchMentionNotificationsAsync(
                msg.Id, _channelId, _caller.UserId,
                It.Is<IReadOnlyList<MessageMention>>(m => m.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
