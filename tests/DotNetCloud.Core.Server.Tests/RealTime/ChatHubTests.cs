using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Chat;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class ChatHubTests
{
    [TestMethod]
    public async Task WhenSendMessageCalledThenBroadcastsNewMessage()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var message = new ChatMessageDto
        {
            Id = Guid.CreateVersion7(),
            ChannelId = channelId,
            SenderUserId = userId,
            Content = "hello",
            Type = "Text"
        };

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(s => s.SendMessageAsync(channelId, userId, "hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        var result = await hub.SendMessageAsync(channelId, "hello");

        Assert.AreEqual(message.Id, result.Id);
        broadcasterMock.Verify(r => r.BroadcastAsync(
            $"chat-channel-{channelId}", "NewMessage",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenMarkReadCalledThenBroadcastsUnreadCountForCaller()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();

        var unreadCounts = new List<ChatUnreadCountDto>
        {
            new() { ChannelId = channelId, UnreadCount = 4, MentionCount = 1 }
        };

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(c => c.MarkChannelAsReadAsync(channelId, messageId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        chatApiMock
            .Setup(c => c.GetUnreadCountsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChatUnreadCountDto>)unreadCounts);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        await hub.MarkReadAsync(channelId, messageId);

        chatApiMock.Verify(c => c.MarkChannelAsReadAsync(channelId, messageId, userId, It.IsAny<CancellationToken>()), Times.Once);
        broadcasterMock.Verify(b => b.SendToUserAsync(userId, "UnreadCountUpdated", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenAddReactionCalledThenBroadcastsUpdatedReactions()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();

        // Pre-populate the hub's static message→channel map
        TrackMessageInHub(messageId, channelId);

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(c => c.AddReactionAsync(messageId, userId, "👍", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        await hub.AddReactionAsync(messageId, "👍");

        chatApiMock.Verify(c => c.AddReactionAsync(messageId, userId, "👍", It.IsAny<CancellationToken>()), Times.Once);
        broadcasterMock.Verify(b => b.BroadcastAsync(
            $"chat-channel-{channelId}", "ReactionUpdated",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenStartTypingCalledThenBroadcastsTypingIndicator()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(c => c.NotifyTypingAsync(channelId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        await hub.StartTypingAsync(channelId, "Ben");

        chatApiMock.Verify(c => c.NotifyTypingAsync(channelId, userId, It.IsAny<CancellationToken>()), Times.Once);
        broadcasterMock.Verify(b => b.BroadcastAsync(
            $"chat-channel-{channelId}", "TypingIndicator",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenStopTypingCalledThenBroadcastsTypingStopped()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();

        var chatApiMock = new Mock<IChatApiClient>();
        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        await hub.StopTypingAsync(channelId);

        broadcasterMock.Verify(b => b.BroadcastAsync(
            $"chat-channel-{channelId}", "TypingIndicator",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenRemoveReactionCalledThenBroadcastsUpdatedReactions()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();

        // Pre-populate the hub's static message→channel map
        TrackMessageInHub(messageId, channelId);

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(c => c.RemoveReactionAsync(messageId, userId, "👍", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        await hub.RemoveReactionAsync(messageId, "👍");

        chatApiMock.Verify(c => c.RemoveReactionAsync(messageId, userId, "👍", It.IsAny<CancellationToken>()), Times.Once);
        broadcasterMock.Verify(b => b.BroadcastAsync(
            $"chat-channel-{channelId}", "ReactionUpdated",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenEditMessageCalledThenBroadcastsEditedMessage()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();
        var newContent = "updated content";
        var message = new ChatMessageDto
        {
            Id = messageId,
            ChannelId = channelId,
            SenderUserId = userId,
            Content = newContent,
            Type = "Text"
        };

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(c => c.EditMessageAsync(messageId, userId, newContent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        var result = await hub.EditMessageAsync(messageId, newContent);

        Assert.AreEqual(newContent, result.Content);
        broadcasterMock.Verify(b => b.BroadcastAsync(
            $"chat-channel-{channelId}", "MessageEdited",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenDeleteMessageCalledThenBroadcastsDeletion()
    {
        var userId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();

        // Pre-populate the hub's static message→channel map
        TrackMessageInHub(messageId, channelId);

        var chatApiMock = new Mock<IChatApiClient>();
        chatApiMock
            .Setup(c => c.DeleteMessageAsync(messageId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var broadcasterMock = new Mock<IRealtimeBroadcaster>();

        var hub = CreateHub(userId, chatApiMock, broadcasterMock);

        await hub.DeleteMessageAsync(messageId);

        chatApiMock.Verify(c => c.DeleteMessageAsync(messageId, userId, It.IsAny<CancellationToken>()), Times.Once);
        broadcasterMock.Verify(b => b.BroadcastAsync(
            $"chat-channel-{channelId}", "MessageDeleted",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void TrackMessageInHub(Guid messageId, Guid channelId)
    {
        var field = typeof(ChatHub).GetField("MessageChannelMap", BindingFlags.Static | BindingFlags.NonPublic);
        var dict = (ConcurrentDictionary<Guid, Guid>)field!.GetValue(null)!;
        dict[messageId] = channelId;
    }

    private static ChatHub CreateHub(
        Guid userId,
        Mock<IChatApiClient>? chatApiClientMock = null,
        Mock<IRealtimeBroadcaster>? broadcasterMock = null)
    {
        chatApiClientMock ??= new Mock<IChatApiClient>();
        broadcasterMock ??= new Mock<IRealtimeBroadcaster>();

        var hub = new ChatHub(
            chatApiClientMock.Object,
            broadcasterMock.Object,
            NullLogger<ChatHub>.Instance);

        var mockCallerContext = new Mock<HubCallerContext>();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test");
        var principal = new ClaimsPrincipal(identity);
        mockCallerContext.Setup(c => c.User).Returns(principal);
        mockCallerContext.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);
        mockCallerContext.Setup(c => c.ConnectionId).Returns("test-connection-id");

        // Wire up the features collection so HubFeatures works
        var featureCollection = new FeatureCollection();
        mockCallerContext.Setup(c => c.Features).Returns(featureCollection);

        hub.Context = mockCallerContext.Object;

        // Set up Clients for broadcasting
        var mockClients = new Mock<IHubCallerClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        var mockSingleClientProxy = new Mock<ISingleClientProxy>();
        mockClients.Setup(c => c.All).Returns(mockSingleClientProxy.Object);
        mockClients.Setup(c => c.Others).Returns(mockSingleClientProxy.Object);
        mockClients.Setup(c => c.Caller).Returns(mockSingleClientProxy.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockSingleClientProxy.Object);
        mockClients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(mockSingleClientProxy.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockSingleClientProxy.Object);
        mockClients.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>())).Returns(mockSingleClientProxy.Object);
        hub.Clients = mockClients.Object;

        return hub;
    }
}
