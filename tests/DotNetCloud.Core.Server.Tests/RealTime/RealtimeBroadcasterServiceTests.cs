using DotNetCloud.Core.Server.RealTime;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class RealtimeBroadcasterServiceTests
{
    private StubHubContext _hubContext = null!;
    private UserConnectionTracker _tracker = null!;
    private RealtimeBroadcasterService _broadcaster = null!;

    [TestInitialize]
    public void Setup()
    {
        _hubContext = new StubHubContext();
        _tracker = new UserConnectionTracker();

        _broadcaster = new RealtimeBroadcasterService(
            _hubContext,
            _tracker,
            NullLogger<RealtimeBroadcasterService>.Instance);
    }

    // --- BroadcastAsync ---

    [TestMethod]
    public async Task WhenBroadcastAsyncCalledThenSendsToGroup()
    {
        await _broadcaster.BroadcastAsync("test-group", "TestEvent", new { Data = "hello" });

        Assert.AreEqual(1, _hubContext.StubClients.GroupCalls.Count);
        Assert.AreEqual("test-group", _hubContext.StubClients.GroupCalls[0]);
        Assert.AreEqual(1, _hubContext.StubClients.LastProxy.Invocations.Count);
        Assert.AreEqual("TestEvent", _hubContext.StubClients.LastProxy.Invocations[0].Method);
    }

    [TestMethod]
    public async Task WhenBroadcastAsyncWithNullGroupThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.BroadcastAsync(null!, "TestEvent", new { }));
    }

    [TestMethod]
    public async Task WhenBroadcastAsyncWithEmptyGroupThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.BroadcastAsync("", "TestEvent", new { }));
    }

    [TestMethod]
    public async Task WhenBroadcastAsyncWithNullEventNameThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.BroadcastAsync("group", null!, new { }));
    }

    [TestMethod]
    public async Task WhenBroadcastAsyncWithNullMessageThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _broadcaster.BroadcastAsync("group", "event", null!));
    }

    // --- BroadcastToGroupExceptUserAsync ---

    [TestMethod]
    public async Task WhenBroadcastToGroupExceptUserWithConnectionsThenExcludesThemFromGroup()
    {
        var typingUser = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        _tracker.AddConnection(typingUser, "conn-typer-1");
        _tracker.AddConnection(typingUser, "conn-typer-2"); // same user, second device
        _tracker.AddConnection(otherUser, "conn-other");

        await _broadcaster.BroadcastToGroupExceptUserAsync(
            "chat-channel-g", typingUser, "TypingIndicator", new { channelId = "g", userId = typingUser });

        // The typing user's own connections are excluded from the group send.
        Assert.AreEqual(1, _hubContext.StubClients.GroupExceptCalls.Count);
        Assert.AreEqual("chat-channel-g", _hubContext.StubClients.GroupExceptCalls[0].GroupName);
        CollectionAssert.AreEquivalent(
            new[] { "conn-typer-1", "conn-typer-2" },
            _hubContext.StubClients.GroupExceptCalls[0].ExcludedConnectionIds.ToList());
        Assert.AreEqual(0, _hubContext.StubClients.GroupCalls.Count, "Should not fall back to a plain group broadcast.");
        Assert.AreEqual(1, _hubContext.StubClients.LastProxy.Invocations.Count);
        Assert.AreEqual("TypingIndicator", _hubContext.StubClients.LastProxy.Invocations[0].Method);
    }

    [TestMethod]
    public async Task WhenBroadcastToGroupExceptUserWithNoConnectionsThenFallsBackToGroupBroadcast()
    {
        var offlineUser = Guid.CreateVersion7();

        await _broadcaster.BroadcastToGroupExceptUserAsync(
            "chat-channel-g", offlineUser, "TypingIndicator", new { channelId = "g", userId = offlineUser });

        // Nobody to exclude → normal group broadcast so other members still receive it.
        Assert.AreEqual(1, _hubContext.StubClients.GroupCalls.Count);
        Assert.AreEqual("chat-channel-g", _hubContext.StubClients.GroupCalls[0]);
        Assert.AreEqual(0, _hubContext.StubClients.GroupExceptCalls.Count);
        Assert.AreEqual(1, _hubContext.StubClients.LastProxy.Invocations.Count);
        Assert.AreEqual("TypingIndicator", _hubContext.StubClients.LastProxy.Invocations[0].Method);
    }

    [TestMethod]
    public async Task WhenBroadcastToGroupExceptUserWithNullGroupThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.BroadcastToGroupExceptUserAsync(null!, Guid.CreateVersion7(), "TypingIndicator", new { }));
    }

    [TestMethod]
    public async Task WhenBroadcastToGroupExceptUserWithNullEventNameThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.BroadcastToGroupExceptUserAsync("group", Guid.CreateVersion7(), null!, new { }));
    }

    [TestMethod]
    public async Task WhenBroadcastToGroupExceptUserWithNullMessageThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _broadcaster.BroadcastToGroupExceptUserAsync("group", Guid.CreateVersion7(), "event", null!));
    }

    // --- SendToUserAsync ---

    [TestMethod]
    public async Task WhenSendToUserWithConnectionsThenSendsToConnections()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddConnection(userId, "conn-2");

        await _broadcaster.SendToUserAsync(userId, "TestEvent", new { Data = "hello" });

        Assert.AreEqual(1, _hubContext.StubClients.ClientsCalls.Count);
        Assert.AreEqual(2, _hubContext.StubClients.ClientsCalls[0].ConnectionIds.Count);
        Assert.AreEqual(1, _hubContext.StubClients.LastProxy.Invocations.Count);
        Assert.AreEqual("TestEvent", _hubContext.StubClients.LastProxy.Invocations[0].Method);
    }

    [TestMethod]
    public async Task WhenSendToUserWithNoConnectionsThenDoesNotSend()
    {
        await _broadcaster.SendToUserAsync(Guid.CreateVersion7(), "TestEvent", new { Data = "hello" });

        Assert.AreEqual(0, _hubContext.StubClients.ClientsCalls.Count);
        Assert.AreEqual(0, _hubContext.StubClients.LastProxy.Invocations.Count);
    }

    [TestMethod]
    public async Task WhenSendToUserWithNullEventNameThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.SendToUserAsync(Guid.CreateVersion7(), null!, new { }));
    }

    [TestMethod]
    public async Task WhenSendToUserWithNullMessageThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _broadcaster.SendToUserAsync(Guid.CreateVersion7(), "event", null!));
    }

    // --- SendToRoleAsync ---

    [TestMethod]
    public async Task WhenSendToRoleThenSendsToRoleGroup()
    {
        await _broadcaster.SendToRoleAsync("Administrator", "TestEvent", new { Data = "hello" });

        Assert.AreEqual(1, _hubContext.StubClients.GroupCalls.Count);
        Assert.AreEqual("role:Administrator", _hubContext.StubClients.GroupCalls[0]);
        Assert.AreEqual(1, _hubContext.StubClients.LastProxy.Invocations.Count);
        Assert.AreEqual("TestEvent", _hubContext.StubClients.LastProxy.Invocations[0].Method);
    }

    [TestMethod]
    public async Task WhenSendToRoleWithNullRoleThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.SendToRoleAsync(null!, "TestEvent", new { }));
    }

    [TestMethod]
    public async Task WhenSendToRoleWithNullEventNameThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.SendToRoleAsync("role", null!, new { }));
    }

    [TestMethod]
    public async Task WhenSendToRoleWithNullMessageThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _broadcaster.SendToRoleAsync("role", "event", null!));
    }

    // --- AddToGroupAsync ---

    [TestMethod]
    public async Task WhenAddToGroupThenAddsAllUserConnections()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddConnection(userId, "conn-2");

        await _broadcaster.AddToGroupAsync(userId, "my-group");

        var ops = _hubContext.StubGroups.Operations;
        Assert.AreEqual(2, ops.Count);
        Assert.IsTrue(ops.Any(o => o.ConnectionId == "conn-1" && o.GroupName == "my-group" && o.Action == "Add"));
        Assert.IsTrue(ops.Any(o => o.ConnectionId == "conn-2" && o.GroupName == "my-group" && o.Action == "Add"));
    }

    [TestMethod]
    public async Task WhenAddToGroupWithNoConnectionsThenDoesNothing()
    {
        var userId = Guid.CreateVersion7();

        await _broadcaster.AddToGroupAsync(userId, "my-group");

        Assert.AreEqual(0, _hubContext.StubGroups.Operations.Count);
        CollectionAssert.Contains(_tracker.GetGroups(userId).ToList(), "my-group");
    }

    [TestMethod]
    public async Task WhenAddToGroupWithNullGroupThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.AddToGroupAsync(Guid.CreateVersion7(), null!));
    }

    // --- RemoveFromGroupAsync ---

    [TestMethod]
    public async Task WhenRemoveFromGroupThenRemovesAllUserConnections()
    {
        var userId = Guid.CreateVersion7();
        _tracker.AddConnection(userId, "conn-1");
        _tracker.AddConnection(userId, "conn-2");

        await _broadcaster.RemoveFromGroupAsync(userId, "my-group");

        var ops = _hubContext.StubGroups.Operations;
        Assert.AreEqual(2, ops.Count);
        Assert.IsTrue(ops.Any(o => o.ConnectionId == "conn-1" && o.GroupName == "my-group" && o.Action == "Remove"));
        Assert.IsTrue(ops.Any(o => o.ConnectionId == "conn-2" && o.GroupName == "my-group" && o.Action == "Remove"));
    }

    [TestMethod]
    public async Task WhenRemoveFromGroupWithNullGroupThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _broadcaster.RemoveFromGroupAsync(Guid.CreateVersion7(), null!));
    }

    [TestMethod]
    public async Task WhenRemoveFromGroupThenTrackedMembershipIsRemoved()
    {
        var userId = Guid.CreateVersion7();

        await _broadcaster.AddToGroupAsync(userId, "my-group");
        await _broadcaster.RemoveFromGroupAsync(userId, "my-group");

        Assert.AreEqual(0, _tracker.GetGroups(userId).Count);
    }
}
