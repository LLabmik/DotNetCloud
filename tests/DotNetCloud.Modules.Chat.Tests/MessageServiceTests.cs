using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
}
