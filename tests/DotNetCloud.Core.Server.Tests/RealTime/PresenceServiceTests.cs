using DotNetCloud.Core.Server.RealTime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class PresenceServiceTests
{
    private UserConnectionTracker _tracker = null!;
    private PresenceService _presenceService = null!;

    [TestInitialize]
    public void Setup()
    {
        _tracker = new UserConnectionTracker();
        _presenceService = new PresenceService(
            _tracker,
            NullLogger<PresenceService>.Instance);
    }

    [TestMethod]
    public async Task WhenUserNotConnectedThenIsOnlineReturnsFalse()
    {
        var result = await _presenceService.IsOnlineAsync(Guid.NewGuid());

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WhenUserConnectedThenIsOnlineReturnsTrue()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");

        var result = await _presenceService.IsOnlineAsync(userId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task WhenUserConnectedThenLastSeenIsUpdated()
    {
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await _presenceService.UserConnectedAsync(userId, "conn-1");

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenUserDisconnectedThenLastSeenIsUpdated()
    {
        var userId = Guid.NewGuid();
        await _presenceService.UserConnectedAsync(userId, "conn-1");

        var before = DateTime.UtcNow;
        await _presenceService.UserDisconnectedAsync(userId, "conn-1");

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenPingReceivedThenLastSeenIsUpdated()
    {
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await _presenceService.UpdateLastSeenAsync(userId);

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenUserNeverSeenThenGetLastSeenReturnsNull()
    {
        var lastSeen = await _presenceService.GetLastSeenAsync(Guid.NewGuid());

        Assert.IsNull(lastSeen);
    }

    [TestMethod]
    public async Task WhenMultipleUsersOnlineThenGetOnlineStatusReturnsCorrectMap()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        _tracker.AddConnection(user1, "conn-1");
        _tracker.AddConnection(user2, "conn-2");

        var result = await _presenceService.GetOnlineStatusAsync([user1, user2, user3]);

        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result[user1]);
        Assert.IsTrue(result[user2]);
        Assert.IsFalse(result[user3]);
    }

    [TestMethod]
    public async Task WhenUsersOnlineThenGetOnlineUsersReturnsAll()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
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
        _tracker.AddConnection(Guid.NewGuid(), "conn-1");
        _tracker.AddConnection(Guid.NewGuid(), "conn-2");
        _tracker.AddConnection(Guid.NewGuid(), "conn-3");

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
}
