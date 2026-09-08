using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Services.ModuleApis;
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
    public async Task WhenUserHasTrackedGroupsThenOnConnectedAddsConnectionToEachGroup()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddGroupMembership(userId, "chat:channel-a");
        tracker.AddGroupMembership(userId, "chat:channel-b");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var hub = new CoreHub(
            tracker,
            presence,
            Mock.Of<DotNetCloud.Core.Services.ModuleApis.IChatApiClient>(),
            Mock.Of<IRealtimeBroadcaster>(),
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

    [TestMethod]
    public async Task OnConnectedAsync_FirstConnection_NotifiesInProcessSubscribersOnline()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var notifierMock = new Mock<IChatMessageNotifier>();

        var hub = CreateHub(tracker, presence, notifierMock, userId, "conn-1");

        await hub.OnConnectedAsync();

        notifierMock.Verify(m => m.NotifyUserPresenceChanged(
            It.Is<UserPresenceChangedNotification>(n => n.UserId == userId && n.IsOnline)),
            Times.Once);
    }

    [TestMethod]
    public async Task OnConnectedAsync_SubsequentConnection_DoesNotNotify()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(userId, "existing-connection");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var notifierMock = new Mock<IChatMessageNotifier>();

        var hub = CreateHub(tracker, presence, notifierMock, userId, "conn-2");

        await hub.OnConnectedAsync();

        // The user was already online, so no presence transition should be raised.
        notifierMock.Verify(m => m.NotifyUserPresenceChanged(
            It.IsAny<UserPresenceChangedNotification>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OnDisconnectedAsync_LastConnection_NotifiesInProcessSubscribersOffline()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(userId, "conn-1");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var notifierMock = new Mock<IChatMessageNotifier>();

        var hub = CreateHub(tracker, presence, notifierMock, userId, "conn-1");

        await hub.OnDisconnectedAsync(exception: null);

        notifierMock.Verify(m => m.NotifyUserPresenceChanged(
            It.Is<UserPresenceChangedNotification>(n => n.UserId == userId && !n.IsOnline)),
            Times.Once);
    }

    [TestMethod]
    public async Task OnDisconnectedAsync_RemainingConnection_DoesNotNotify()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(userId, "conn-1");
        tracker.AddConnection(userId, "conn-2");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var notifierMock = new Mock<IChatMessageNotifier>();

        var hub = CreateHub(tracker, presence, notifierMock, userId, "conn-2");

        await hub.OnDisconnectedAsync(exception: null);

        // Another connection remains, so the user is still online — no transition.
        notifierMock.Verify(m => m.NotifyUserPresenceChanged(
            It.IsAny<UserPresenceChangedNotification>()),
            Times.Never);
    }

    private static CoreHub CreateHub(
        UserConnectionTracker tracker,
        PresenceService presence,
        Mock<IChatMessageNotifier> notifierMock,
        Guid userId,
        string connectionId)
    {
        var othersProxy = new Mock<IClientProxy>();
        othersProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Others).Returns(othersProxy.Object);

        var hub = new CoreHub(
            tracker,
            presence,
            Mock.Of<DotNetCloud.Core.Services.ModuleApis.IChatApiClient>(),
            Mock.Of<IRealtimeBroadcaster>(),
            NullLogger<CoreHub>.Instance,
            eventBus: null,
            chatMessageNotifier: notifierMock.Object);

        hub.Context = new TestHubCallerContext(userId, connectionId);
        hub.Clients = clients.Object;
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
