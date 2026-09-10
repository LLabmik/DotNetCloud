using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.RealTime;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class PresenceServiceTests
{
    private UserConnectionTracker _tracker = null!;
    private MutableTimeProvider _time = null!;
    private PresenceService _presenceService = null!;
    private readonly List<(Guid UserId, PresenceState State)> _raisedChanges = [];

    [TestInitialize]
    public void Setup()
    {
        _tracker = new UserConnectionTracker();
        _time = new MutableTimeProvider();
        _presenceService = new PresenceService(
            _tracker,
            NullLogger<PresenceService>.Instance,
            dbContextFactory: null,
            timeProvider: _time);
        _raisedChanges.Clear();
        _presenceService.PresenceStateChanged += (userId, state) => _raisedChanges.Add((userId, state));
    }

    [TestMethod]
    public async Task WhenUserNotConnectedThenIsOnlineReturnsFalse()
    {
        var result = await _presenceService.IsOnlineAsync(Guid.CreateVersion7());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WhenUserConnectedThenIsOnlineReturnsTrue()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");

        var result = await _presenceService.IsOnlineAsync(userId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task WhenUserConnectedThenLastSeenIsUpdated()
    {
        var userId = Guid.CreateVersion7();
        var before = _time.GetUtcNow().UtcDateTime;

        await _presenceService.UserConnectedAsync(userId, "conn-1");

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenUserDisconnectedThenLastSeenIsUpdated()
    {
        var userId = Guid.CreateVersion7();
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        var before = _time.GetUtcNow().UtcDateTime;
        await _presenceService.UserDisconnectedAsync(userId, "conn-1");

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenActivityReportedThenLastSeenIsUpdated()
    {
        var userId = Guid.CreateVersion7();
        var before = _time.GetUtcNow().UtcDateTime;

        await _presenceService.ReportActivityAsync(userId);

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenUserNeverSeenThenGetLastSeenReturnsNull()
    {
        var lastSeen = await _presenceService.GetLastSeenAsync(Guid.CreateVersion7());

        Assert.IsNull(lastSeen);
    }

    [TestMethod]
    public async Task WhenMultipleUsersOnlineThenGetOnlineStatusReturnsCorrectMap()
    {
        var user1 = Guid.CreateVersion7();
        var user2 = Guid.CreateVersion7();
        var user3 = Guid.CreateVersion7();
        _tracker.AddConnection(user1, "conn-1");
        _tracker.AddConnection(user2, "conn-2");

        var result = await _presenceService.GetOnlineStatusAsync([user1, user2, user3]);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(PresenceState.Online, result[user1]);
        Assert.AreEqual(PresenceState.Online, result[user2]);
        Assert.AreEqual(PresenceState.Offline, result[user3]);
    }

    [TestMethod]
    public async Task WhenUsersOnlineThenGetOnlineUsersReturnsAll()
    {
        var user1 = Guid.CreateVersion7();
        var user2 = Guid.CreateVersion7();
        _tracker.AddConnection(user1, "conn-1");
        _tracker.AddConnection(user2, "conn-2");

        var onlineUsers = await _presenceService.GetOnlineUsersAsync();

        Assert.AreEqual(2, onlineUsers.Count);
        Assert.IsTrue(onlineUsers.Contains(user1));
        Assert.IsTrue(onlineUsers.Contains(user2));
    }

    [TestMethod]
    public async Task WhenConnectionsExistThenGetActiveConnectionCountIsCorrect()
    {
        _tracker.AddConnection(Guid.CreateVersion7(), "conn-1");
        _tracker.AddConnection(Guid.CreateVersion7(), "conn-2");
        _tracker.AddConnection(Guid.CreateVersion7(), "conn-3");

        var count = await _presenceService.GetActiveConnectionCountAsync();

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task WhenNoConnectionsThenGetActiveConnectionCountIsZero()
    {
        var count = await _presenceService.GetActiveConnectionCountAsync();

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task WhenNullUserIdsThenGetOnlineStatusThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _presenceService.GetOnlineStatusAsync(null!));
    }

    [TestMethod]
    public async Task WhenSetPresenceThenCustomStatusMessageIsPersisted()
    {
        var userId = Guid.CreateVersion7();

        var presence = await _presenceService.SetPresenceAsync(userId, "Away", "At lunch");
        var fetched = await _presenceService.GetPresenceAsync(userId);

        Assert.AreEqual("Away", presence.Status);
        Assert.AreEqual("At lunch", presence.StatusMessage);
        Assert.AreEqual("Away", fetched.Status);
        Assert.AreEqual("At lunch", fetched.StatusMessage);
    }

    [TestMethod]
    public async Task WhenSetPresenceWithInvalidStatusThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _presenceService.SetPresenceAsync(Guid.CreateVersion7(), "Invisible", "testing"));
    }

    // ── 4-state derivation ─────────────────────────────────────────

    [TestMethod]
    public async Task ConnectWithoutDnd_ReturnsOnline()
    {
        var userId = Guid.CreateVersion7();

        var state = await _presenceService.UserConnectedAsync(userId, "conn-1");

        Assert.AreEqual(PresenceState.Online, state);
        Assert.AreEqual(PresenceState.Online, _presenceService.GetDisplayState(userId));
        Assert.AreEqual("Online", (await _presenceService.GetPresenceAsync(userId)).Status);
    }

    [TestMethod]
    public async Task ConnectWithCachedDnd_ReturnsDoNotDisturb()
    {
        var userId = Guid.CreateVersion7();
        _presenceService.CacheDoNotDisturb(userId, enabled: true);
        _tracker.AddConnection(userId, "conn-0");

        var state = await _presenceService.UserConnectedAsync(userId, "conn-1");

        Assert.AreEqual(PresenceState.DoNotDisturb, state);
    }

    [TestMethod]
    public async Task Connect_DoesNotRaiseStateChangeEvent()
    {
        var userId = Guid.CreateVersion7();

        await _presenceService.UserConnectedAsync(userId, "conn-1");

        Assert.AreEqual(0, _raisedChanges.Count);
    }

    [TestMethod]
    public async Task Disconnect_ReturnsOffline_EvenWhenDndEnabled()
    {
        var userId = Guid.CreateVersion7();
        _presenceService.CacheDoNotDisturb(userId, enabled: true);
        _tracker.AddConnection(userId, "conn-0");
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        var state = await _presenceService.UserDisconnectedAsync(userId, "conn-1");

        Assert.AreEqual(PresenceState.Offline, state);
        Assert.AreEqual(PresenceState.Offline, _presenceService.GetDisplayState(userId));
    }

    [TestMethod]
    public async Task RecomputeAfterIdleThreshold_TransitionsOnlineToAway_AndRaisesEvent()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        _time.Advance(TimeSpan.FromMinutes(2));
        var state = await _presenceService.RecomputeAsync(userId);

        Assert.AreEqual(PresenceState.Away, state);
        Assert.AreEqual(PresenceState.Away, _presenceService.GetDisplayState(userId));
        Assert.IsTrue(_raisedChanges.Any(c => c.UserId == userId && c.State == PresenceState.Away));
    }

    [TestMethod]
    public async Task ReportActivity_FlippsAwayBackToOnline_AndRaisesEvent()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        _time.Advance(TimeSpan.FromMinutes(2));
        await _presenceService.RecomputeAsync(userId);
        Assert.AreEqual(PresenceState.Away, _presenceService.GetDisplayState(userId));
        _raisedChanges.Clear();

        await _presenceService.ReportActivityAsync(userId);

        Assert.AreEqual(PresenceState.Online, _presenceService.GetDisplayState(userId));
        Assert.IsTrue(_raisedChanges.Any(c => c.UserId == userId && c.State == PresenceState.Online));
    }

    [TestMethod]
    public async Task ReportActivity_WhenDndEnabled_StaysDoNotDisturb()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _presenceService.CacheDoNotDisturb(userId, enabled: true);
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _raisedChanges.Clear();

        await _presenceService.ReportActivityAsync(userId);

        Assert.AreEqual(PresenceState.DoNotDisturb, _presenceService.GetDisplayState(userId));
        Assert.AreEqual(0, _raisedChanges.Count, "Activity should not change a DND user's state.");
    }

    [TestMethod]
    public async Task ReportActivity_WhenOffline_DoesNotRaiseEvent()
    {
        var userId = Guid.CreateVersion7();

        await _presenceService.ReportActivityAsync(userId);

        Assert.AreEqual(PresenceState.Offline, _presenceService.GetDisplayState(userId));
        Assert.AreEqual(0, _raisedChanges.Count);
    }

    [TestMethod]
    public async Task EnableDnd_WhileOnline_TransitionsToDoNotDisturb_AndRaisesEvent()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        _presenceService.SetDoNotDisturb(userId, enabled: true);

        Assert.AreEqual(PresenceState.DoNotDisturb, _presenceService.GetDisplayState(userId));
        Assert.IsTrue(_raisedChanges.Any(c => c.UserId == userId && c.State == PresenceState.DoNotDisturb));
    }

    [TestMethod]
    public async Task DisableDnd_WhileOnlineAndInactive_ReturnsToAway()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _time.Advance(TimeSpan.FromMinutes(2));
        await _presenceService.RecomputeAsync(userId); // Away

        _presenceService.SetDoNotDisturb(userId, enabled: true);
        Assert.AreEqual(PresenceState.DoNotDisturb, _presenceService.GetDisplayState(userId));

        _presenceService.SetDoNotDisturb(userId, enabled: false);

        // Still inactive (no new activity since idle) → back to Away, not Online.
        Assert.AreEqual(PresenceState.Away, _presenceService.GetDisplayState(userId));
    }

    [TestMethod]
    public async Task EnableDnd_WhileOffline_DoesNotChangePresence()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _tracker.RemoveConnection("conn-1");
        await _presenceService.UserDisconnectedAsync(userId, "conn-1");

        _presenceService.SetDoNotDisturb(userId, enabled: true);

        Assert.AreEqual(PresenceState.Offline, _presenceService.GetDisplayState(userId));
        Assert.IsFalse(_raisedChanges.Any(c => c.UserId == userId),
            "Offline DND toggles must not broadcast (gray wins).");
    }

    [TestMethod]
    public async Task GetOnlineStatus_ReportsDoNotDisturbForDndOnlineUser()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _presenceService.CacheDoNotDisturb(userId, enabled: true);
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        var result = await _presenceService.GetOnlineStatusAsync([userId]);

        Assert.AreEqual(PresenceState.DoNotDisturb, result[userId]);
    }

    [TestMethod]
    public async Task GetOnlineStatus_ReportsAwayForIdleOnlineUser()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _presenceService.UpdateIdleThreshold(TimeSpan.FromMinutes(1));
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _time.Advance(TimeSpan.FromMinutes(2));

        var result = await _presenceService.GetOnlineStatusAsync([userId]);

        Assert.AreEqual(PresenceState.Away, result[userId]);
    }

    [TestMethod]
    public async Task RecomputeForUnknownUser_ReturnsOffline()
    {
        var state = await _presenceService.RecomputeAsync(Guid.CreateVersion7());

        Assert.AreEqual(PresenceState.Offline, state);
    }

    [TestMethod]
    public async Task UpdateIdleThreshold_IgnoresZeroOrNegative()
    {
        _presenceService.UpdateIdleThreshold(TimeSpan.Zero);

        // Should have fallen back to the default (a positive threshold) — verify no crash and
        // that a long idle still flips to Away using the default.
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        await _presenceService.UserConnectedAsync(userId, "conn-1");
        _time.Advance(PresenceService.DefaultIdleThreshold + TimeSpan.FromMinutes(1));

        var state = await _presenceService.RecomputeAsync(userId);

        Assert.AreEqual(PresenceState.Away, state);
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
