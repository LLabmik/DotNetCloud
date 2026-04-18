using System.Security.Claims;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class CoreHubTests
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

        reactionService.Verify(s => s.AddReactionAsync(messageId, "👍", It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastReactionUpdatedAsync(channelId, messageId, It.IsAny<IReadOnlyList<MessageReactionDto>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenStartTypingCalledThenPublishesTypingAndBroadcasts()
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

        typingService.Verify(s => s.NotifyTypingAsync(channelId, It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        realtimeService.Verify(r => r.BroadcastTypingAsync(channelId, userId, "Ben", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenSetPresenceCalledThenBroadcastsPresenceAndPublishesEvent()
    {
        var userId = Guid.NewGuid();

        var messageService = new Mock<IMessageService>();
        var channelMemberService = new Mock<IChannelMemberService>();
        var reactionService = new Mock<IReactionService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var realtimeService = new Mock<IChatRealtimeService>();
        var eventBus = new Mock<IEventBus>();

        var hub = CreateHub(
            userId,
            messageService.Object,
            channelMemberService.Object,
            reactionService.Object,
            typingService.Object,
            realtimeService.Object,
            eventBus.Object);

        var result = await hub.SetPresenceAsync("Away", "At lunch");

        Assert.AreEqual("Away", result.Status);
        Assert.AreEqual("At lunch", result.StatusMessage);
        realtimeService.Verify(r => r.BroadcastPresenceChangedAsync(
            It.Is<PresenceDto>(p => p.UserId == userId && p.Status == "Away" && p.StatusMessage == "At lunch"),
            It.IsAny<CancellationToken>()),
            Times.Once);
        eventBus.Verify(e => e.PublishAsync(
            It.Is<PresenceChangedEvent>(ev => ev.UserId == userId && ev.Status == "Away" && ev.StatusMessage == "At lunch"),
            It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenUserHasTrackedGroupsThenOnConnectedAddsConnectionToEachGroup()
    {
        var userId = Guid.NewGuid();
        var tracker = new UserConnectionTracker();
        tracker.AddGroupMembership(userId, "chat:channel-a");
        tracker.AddGroupMembership(userId, "chat:channel-b");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var hub = new CoreHub(
            tracker,
            presence,
            messageService: null,
            channelMemberService: null,
            reactionService: null,
            typingIndicatorService: null,
            chatRealtimeService: null,
            NullLogger<CoreHub>.Instance);

        var othersProxy = new Mock<IClientProxy>();
        othersProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Others).Returns(othersProxy.Object);

        var groups = new StubGroupManager();

        hub.Context = new TestHubCallerContext(userId, "conn-1");
        hub.Clients = clients.Object;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.IsTrue(groups.Operations.Any(o => o.ConnectionId == "conn-1" && o.GroupName == "chat:channel-a" && o.Action == "Add"));
        Assert.IsTrue(groups.Operations.Any(o => o.ConnectionId == "conn-1" && o.GroupName == "chat:channel-b" && o.Action == "Add"));
    }

    private static CoreHub CreateHub(
        Guid userId,
        IMessageService messageService,
        IChannelMemberService channelMemberService,
        IReactionService reactionService,
        ITypingIndicatorService typingService,
        IChatRealtimeService realtimeService,
        IEventBus? eventBus = null)
    {
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            messageService,
            channelMemberService,
            reactionService,
            typingService,
            realtimeService,
            NullLogger<CoreHub>.Instance,
            eventBus: eventBus);

        hub.Context = new TestHubCallerContext(userId, "conn-chat");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = new StubGroupManager();

        return hub;
    }
}

internal sealed class TestHubCallerContext : HubCallerContext
{
    private readonly ClaimsPrincipal _user;
    private readonly IDictionary<object, object?> _items;
    private readonly IFeatureCollection _features;

    public TestHubCallerContext(Guid userId, string connectionId)
    {
        ConnectionId = connectionId;
        _user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ],
        "Test"));
        _items = new Dictionary<object, object?>();
        _features = new FeatureCollection();
    }

    public override string ConnectionId { get; }

    public override string? UserIdentifier => _user.FindFirstValue(ClaimTypes.NameIdentifier);

    public override ClaimsPrincipal? User => _user;

    public override IDictionary<object, object?> Items => _items;

    public override IFeatureCollection Features => _features;

    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort()
    {
    }
}
