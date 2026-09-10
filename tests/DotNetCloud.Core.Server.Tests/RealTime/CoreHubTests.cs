using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
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
            It.Is<UserPresenceChangedNotification>(n => n.UserId == userId && n.Status == PresenceState.Online)),
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
            It.Is<UserPresenceChangedNotification>(n => n.UserId == userId && n.Status == PresenceState.Offline)),
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

    [TestMethod]
    public async Task GetPresenceStatusAsync_ReturnsStatesForRequestedUsers()
    {
        var online = Guid.CreateVersion7();
        var dnd = Guid.CreateVersion7();
        var offline = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        tracker.AddConnection(online, "conn-online");
        tracker.AddConnection(dnd, "conn-dnd");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        presence.CacheDoNotDisturb(dnd, enabled: true);
        var hub = CreateHub(
            tracker,
            presence,
            new Mock<IChatMessageNotifier>(),
            Guid.CreateVersion7(),
            "conn-caller");

        var result = await hub.GetPresenceStatusAsync([online, dnd, offline]);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(PresenceState.Online, result[online]);
        Assert.AreEqual(PresenceState.DoNotDisturb, result[dnd]);
        Assert.AreEqual(PresenceState.Offline, result[offline]);
    }

    [TestMethod]
    public async Task GetPresenceStatusAsync_EmptyInput_ReturnsEmpty()
    {
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var hub = CreateHub(
            tracker,
            presence,
            new Mock<IChatMessageNotifier>(),
            Guid.CreateVersion7(),
            "conn-caller");

        var result = await hub.GetPresenceStatusAsync([]);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetPresenceStatusAsync_NullInput_ThrowsArgumentNullException()
    {
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var hub = CreateHub(
            tracker,
            presence,
            new Mock<IChatMessageNotifier>(),
            Guid.CreateVersion7(),
            "conn-caller");

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => hub.GetPresenceStatusAsync(null!));
    }

    [TestMethod]
    public async Task PingAsync_ReportsActivity_FlipsIdleUserBackToOnline()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        var time = new MutableTimeProvider();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance, timeProvider: time);
        presence.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        var notifierMock = new Mock<IChatMessageNotifier>();

        tracker.AddConnection(userId, "conn-1");
        await presence.UserConnectedAsync(userId, "conn-1");

        // Go idle past the threshold, then ping (real activity) and expect an online transition.
        time.Advance(TimeSpan.FromMinutes(2));
        await presence.RecomputeAsync(userId);
        Assert.AreEqual(PresenceState.Away, presence.GetDisplayState(userId));

        PresenceState? raised = null;
        presence.PresenceStateChanged += (_, state) => raised = state;

        var hub = CreateHub(tracker, presence, notifierMock, userId, "conn-1");
        await hub.PingAsync();

        Assert.AreEqual(PresenceState.Online, presence.GetDisplayState(userId));
        Assert.AreEqual(PresenceState.Online, raised, "PingAsync (activity) should raise the state-changed event");
    }

    [TestMethod]
    public async Task OnConnectedAsync_FirstConnection_BroadcastsUserPresenceWithStatus()
    {
        var userId = Guid.CreateVersion7();
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var notifierMock = new Mock<IChatMessageNotifier>();

        var othersProxy = new Mock<IClientProxy>();
        othersProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hub = CreateHub(tracker, presence, notifierMock, userId, "conn-1");
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Others).Returns(othersProxy.Object);
        hub.Clients = clients.Object;
        hub.Groups = new StubGroupManager();

        await hub.OnConnectedAsync();

        othersProxy.Verify(p => p.SendCoreAsync(
            "UserPresence",
            It.Is<object?[]>(a => a.Length == 1 && a[0] != null),
            It.IsAny<CancellationToken>()),
            Times.Once);
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

    /// <summary>
    /// A simple controllable <see cref="TimeProvider"/> for deterministic idle/away tests.
    /// </summary>
    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
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
