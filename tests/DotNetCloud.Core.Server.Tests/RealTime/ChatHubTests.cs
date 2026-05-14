using System.Security.Claims;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
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
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var message = new MessageDto
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderUserId = userId,
            Content = "hello",
            Type = "Text"
        };

        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.SendMessageAsync(channelId, It.IsAny<SendMessageDto>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        var result = await hub.SendMessageAsync(channelId, "hello");

        Assert.AreEqual(message.Id, result.Id);
        realtimeService.Verify(r => r.BroadcastNewMessageAsync(channelId, message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenMarkReadCalledThenBroadcastsUnreadCountForCaller()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var messageService = new Mock<IMessageService>();
        var channelMemberService = new Mock<IChannelMemberService>();
        channelMemberService
            .Setup(s => s.GetUnreadCountsAsync(It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UnreadCountDto { ChannelId = channelId, UnreadCount = 4, MentionCount = 1 }
            ]);

        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        await hub.MarkReadAsync(channelId, messageId);

        channelMemberService.Verify(s => s.MarkAsReadAsync(channelId, messageId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastUnreadCountAsync(userId, channelId, 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenAddReactionCalledThenBroadcastsUpdatedReactions()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var message = new MessageDto
        {
            Id = messageId,
            ChannelId = channelId,
            SenderUserId = userId,
            Content = "hello",
            Type = "Text"
        };

        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = [userId] }
        };

        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.GetMessageAsync(messageId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        reactionService
            .Setup(s => s.GetReactionsAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reactions);

        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        await hub.AddReactionAsync(messageId, "👍");

        reactionService.Verify(r => r.AddReactionAsync(messageId, "👍", It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastReactionUpdatedAsync(channelId, messageId, reactions, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenStartTypingCalledThenBroadcastsTypingIndicator()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var messageService = new Mock<IMessageService>();
        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        await hub.StartTypingAsync(channelId, "Ben");

        typingService.Verify(t => t.NotifyTypingAsync(channelId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastTypingAsync(channelId, userId, "Ben", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenStopTypingCalledThenBroadcastsTypingStopped()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var messageService = new Mock<IMessageService>();
        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        await hub.StopTypingAsync(channelId);

        realtimeService.Verify(r => r.BroadcastTypingAsync(channelId, userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenRemoveReactionCalledThenBroadcastsUpdatedReactions()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var message = new MessageDto
        {
            Id = messageId,
            ChannelId = channelId,
            SenderUserId = userId,
            Content = "hello",
            Type = "Text"
        };

        var reactions = new List<MessageReactionDto>();

        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.GetMessageAsync(messageId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        reactionService
            .Setup(s => s.GetReactionsAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reactions);

        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        await hub.RemoveReactionAsync(messageId, "👍");

        reactionService.Verify(r => r.RemoveReactionAsync(messageId, "👍", It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastReactionUpdatedAsync(channelId, messageId, reactions, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenEditMessageCalledThenBroadcastsEditedMessage()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var newContent = "updated content";
        var message = new MessageDto
        {
            Id = messageId,
            ChannelId = channelId,
            SenderUserId = userId,
            Content = newContent,
            Type = "Text"
        };

        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.EditMessageAsync(messageId, It.IsAny<EditMessageDto>(), It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        var result = await hub.EditMessageAsync(messageId, newContent);

        Assert.AreEqual(newContent, result.Content);
        realtimeService.Verify(r => r.BroadcastMessageEditedAsync(channelId, message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenDeleteMessageCalledThenBroadcastsDeletion()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var message = new MessageDto
        {
            Id = messageId,
            ChannelId = channelId,
            SenderUserId = userId,
            Content = "hello",
            Type = "Text"
        };

        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.GetMessageAsync(messageId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object);

        await hub.DeleteMessageAsync(messageId);

        messageService.Verify(s => s.DeleteMessageAsync(messageId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastMessageDeletedAsync(channelId, messageId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ChatHub CreateHub(
        Guid userId,
        IMessageService messageService,
        IChannelMemberService channelMemberService,
        IReactionService reactionService,
        ITypingIndicatorService typingService,
        IChatRealtimeService realtimeService)
    {
        var hub = new ChatHub(
            messageService,
            channelMemberService,
            reactionService,
            typingService,
            realtimeService,
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
