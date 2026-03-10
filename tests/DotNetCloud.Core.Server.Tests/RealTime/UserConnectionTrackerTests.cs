using DotNetCloud.Core.Server.RealTime;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class UserConnectionTrackerTests
{
    private UserConnectionTracker _tracker = null!;

    [TestInitialize]
    public void Setup()
    {
        _tracker = new UserConnectionTracker();
    }

    [TestMethod]
    public void WhenNoConnectionsThenIsOnlineReturnsFalse()
    {
        var userId = Guid.NewGuid();

        Assert.IsFalse(_tracker.IsOnline(userId));
    }

    [TestMethod]
    public void WhenFirstConnectionAddedThenReturnsTrue()
    {
        var userId = Guid.NewGuid();

        var isFirst = _tracker.AddConnection(userId, "conn-1");

        Assert.IsTrue(isFirst);
    }

    [TestMethod]
    public void WhenSecondConnectionAddedThenReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");

        var isFirst = _tracker.AddConnection(userId, "conn-2");

        Assert.IsFalse(isFirst);
    }

    [TestMethod]
    public void WhenConnectionAddedThenUserIsOnline()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");

        Assert.IsTrue(_tracker.IsOnline(userId));
    }

    [TestMethod]
    public void WhenAllConnectionsRemovedThenUserIsOffline()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");

        _tracker.RemoveConnection("conn-1");

        Assert.IsFalse(_tracker.IsOnline(userId));
    }

    [TestMethod]
    public void WhenOneOfTwoConnectionsRemovedThenUserIsStillOnline()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddConnection(userId, "conn-2");

        _tracker.RemoveConnection("conn-1");

        Assert.IsTrue(_tracker.IsOnline(userId));
    }

    [TestMethod]
    public void WhenLastConnectionRemovedThenIsLastConnectionIsTrue()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");

        var result = _tracker.RemoveConnection("conn-1");

        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Value.UserId);
        Assert.IsTrue(result.Value.IsLastConnection);
    }

    [TestMethod]
    public void WhenNonLastConnectionRemovedThenIsLastConnectionIsFalse()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddConnection(userId, "conn-2");

        var result = _tracker.RemoveConnection("conn-1");

        Assert.IsNotNull(result);
        Assert.AreEqual(userId, result.Value.UserId);
        Assert.IsFalse(result.Value.IsLastConnection);
    }

    [TestMethod]
    public void WhenUnknownConnectionRemovedThenReturnsNull()
    {
        var result = _tracker.RemoveConnection("unknown-conn");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenConnectionAddedThenGetConnectionsReturnsIt()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddConnection(userId, "conn-2");

        var connections = _tracker.GetConnections(userId);

        Assert.AreEqual(2, connections.Count);
        CollectionAssert.Contains(connections.ToList(), "conn-1");
        CollectionAssert.Contains(connections.ToList(), "conn-2");
    }

    [TestMethod]
    public void WhenNoConnectionsThenGetConnectionsReturnsEmpty()
    {
        var connections = _tracker.GetConnections(Guid.NewGuid());

        Assert.AreEqual(0, connections.Count);
    }

    [TestMethod]
    public void WhenConnectionAddedThenGetUserIdReturnsCorrectUser()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");

        var result = _tracker.GetUserId("conn-1");

        Assert.AreEqual(userId, result);
    }

    [TestMethod]
    public void WhenUnknownConnectionThenGetUserIdReturnsNull()
    {
        var result = _tracker.GetUserId("unknown");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void WhenMultipleUsersOnlineThenGetOnlineUsersReturnsAll()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        _tracker.AddConnection(user1, "conn-1");
        _tracker.AddConnection(user2, "conn-2");

        var onlineUsers = _tracker.GetOnlineUsers();

        Assert.AreEqual(2, onlineUsers.Count);
        Assert.IsTrue(onlineUsers.Contains(user1));
        Assert.IsTrue(onlineUsers.Contains(user2));
    }

    [TestMethod]
    public void WhenConnectionsExistThenGetTotalConnectionCountIsCorrect()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        _tracker.AddConnection(user1, "conn-1");
        _tracker.AddConnection(user1, "conn-2");
        _tracker.AddConnection(user2, "conn-3");

        Assert.AreEqual(3, _tracker.GetTotalConnectionCount());
    }

    [TestMethod]
    public void WhenUsersOnlineThenGetOnlineUserCountIsCorrect()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        _tracker.AddConnection(user1, "conn-1");
        _tracker.AddConnection(user1, "conn-2");
        _tracker.AddConnection(user2, "conn-3");

        Assert.AreEqual(2, _tracker.GetOnlineUserCount());
    }

    [TestMethod]
    public void WhenNoUsersOnlineThenCountsAreZero()
    {
        Assert.AreEqual(0, _tracker.GetTotalConnectionCount());
        Assert.AreEqual(0, _tracker.GetOnlineUserCount());
        Assert.AreEqual(0, _tracker.GetOnlineUsers().Count);
    }

    [TestMethod]
    public void WhenGroupMembershipAddedThenGetGroupsReturnsGroup()
    {
        var userId = Guid.NewGuid();

        _tracker.AddGroupMembership(userId, "chat:channel-1");

        var groups = _tracker.GetGroups(userId);
        Assert.AreEqual(1, groups.Count);
        CollectionAssert.Contains(groups.ToList(), "chat:channel-1");
    }

    [TestMethod]
    public void WhenGroupMembershipRemovedThenGetGroupsReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        _tracker.AddGroupMembership(userId, "chat:channel-1");

        _tracker.RemoveGroupMembership(userId, "chat:channel-1");

        Assert.AreEqual(0, _tracker.GetGroups(userId).Count);
    }

    [TestMethod]
    public void WhenUserGoesOfflineThenGroupMembershipIsRetained()
    {
        var userId = Guid.NewGuid();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddGroupMembership(userId, "chat:channel-1");

        _tracker.RemoveConnection("conn-1");

        var groups = _tracker.GetGroups(userId);
        Assert.AreEqual(1, groups.Count);
        CollectionAssert.Contains(groups.ToList(), "chat:channel-1");
    }

    [TestMethod]
    public void WhenConnectionIdIsNullThenAddConnectionThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _tracker.AddConnection(Guid.NewGuid(), null!));
    }

    [TestMethod]
    public void WhenConnectionIdIsNullThenRemoveConnectionThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _tracker.RemoveConnection(null!));
    }

    [TestMethod]
    public void WhenConnectionIdIsNullThenGetUserIdThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _tracker.GetUserId(null!));
    }

    [TestMethod]
    public void WhenGroupNameIsNullThenAddGroupMembershipThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _tracker.AddGroupMembership(Guid.NewGuid(), null!));
    }

    [TestMethod]
    public void WhenGroupNameIsNullThenRemoveGroupMembershipThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _tracker.RemoveGroupMembership(Guid.NewGuid(), null!));
    }
}
