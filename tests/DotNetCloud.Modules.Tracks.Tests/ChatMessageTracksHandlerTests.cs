using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Tracks.Events;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests for <see cref="ChatMessageTracksHandler"/> verifying Chat → Tracks real-time integration.
/// </summary>
[TestClass]
public class ChatMessageTracksHandlerTests
{
    private Mock<ITracksRealtimeService> _realtimeServiceMock = null!;
    private ChatMessageTracksHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _realtimeServiceMock = new Mock<ITracksRealtimeService>();
        _handler = new ChatMessageTracksHandler(
            _realtimeServiceMock.Object,
            NullLogger<ChatMessageTracksHandler>.Instance);
    }

    [TestMethod]
    public void ImplementsAllChatEventHandlerInterfaces()
    {
        Assert.IsInstanceOfType<IEventHandler<MessageSentEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<ChannelCreatedEvent>>(_handler);
        Assert.IsInstanceOfType<IEventHandler<ChannelDeletedEvent>>(_handler);
    }

    [TestMethod]
    public async Task HandleMessageSentEvent_BroadcastsActivityToTracks()
    {
        var messageId = Guid.NewGuid();
        var senderUserId = Guid.NewGuid();
        var evt = new MessageSentEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            SenderUserId = senderUserId,
            Content = "Hello team!",
            MessageType = "Text"
        };

        await _handler.HandleAsync(evt);

        _realtimeServiceMock.Verify(s => s.BroadcastActivityAsync(
            Guid.Empty,
            senderUserId,
            "chat_message_sent",
            "ChatMessage",
            messageId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleChannelCreatedEvent_BroadcastsActivityToTracks()
    {
        var channelId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();
        var evt = new ChannelCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channelId,
            ChannelName = "project-alpha",
            ChannelType = "Public",
            CreatedByUserId = createdByUserId
        };

        await _handler.HandleAsync(evt);

        _realtimeServiceMock.Verify(s => s.BroadcastActivityAsync(
            Guid.Empty,
            createdByUserId,
            "chat_channel_created",
            "ChatChannel",
            channelId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleChannelDeletedEvent_BroadcastsActivityToTracks()
    {
        var channelId = Guid.NewGuid();
        var deletedByUserId = Guid.NewGuid();
        var evt = new ChannelDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = channelId,
            ChannelName = "old-channel",
            DeletedByUserId = deletedByUserId
        };

        await _handler.HandleAsync(evt);

        _realtimeServiceMock.Verify(s => s.BroadcastActivityAsync(
            Guid.Empty,
            deletedByUserId,
            "chat_channel_deleted",
            "ChatChannel",
            channelId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleMessageSentEvent_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var evt = new MessageSentEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            SenderUserId = Guid.NewGuid(),
            Content = "test",
            MessageType = "Text"
        };

        await _handler.HandleAsync(evt, cts.Token);

        _realtimeServiceMock.Verify(s => s.BroadcastActivityAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task HandleMultipleEvents_AllBroadcastCorrectly()
    {
        var msgEvt = new MessageSentEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            SenderUserId = Guid.NewGuid(),
            Content = "Hello",
            MessageType = "Text"
        };

        var channelEvt = new ChannelCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ChannelId = Guid.NewGuid(),
            ChannelName = "test",
            ChannelType = "Public",
            CreatedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(msgEvt);
        await _handler.HandleAsync(channelEvt);

        _realtimeServiceMock.Verify(s => s.BroadcastActivityAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            "chat_message_sent",
            "ChatMessage",
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _realtimeServiceMock.Verify(s => s.BroadcastActivityAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            "chat_channel_created",
            "ChatChannel",
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
