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
        var before = DateTime.UtcNow;

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

        var before = DateTime.UtcNow;
        await _presenceService.UserDisconnectedAsync(userId, "conn-1");

        var lastSeen = await _presenceService.GetLastSeenAsync(userId);
        Assert.IsNotNull(lastSeen);
        Assert.IsTrue(lastSeen.Value >= before);
    }

    [TestMethod]
    public async Task WhenPingReceivedThenLastSeenIsUpdated()
    {
        var userId = Guid.CreateVersion7();
        var before = DateTime.UtcNow;

        await _presenceService.UpdateLastSeenAsync(userId);

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
        Assert.IsTrue(result[user1]);
        Assert.IsTrue(result[user2]);
        Assert.IsFalse(result[user3]);
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
}
