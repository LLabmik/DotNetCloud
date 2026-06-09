using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

/// <summary>
/// Tests for video call signaling methods on <see cref="CoreHub"/>:
/// SendCallOfferAsync, SendCallAnswerAsync, SendIceCandidateAsync,
/// SendMediaStateChangeAsync, JoinCallGroupAsync, LeaveCallGroupAsync.
/// Now routed through IChatApiClient (gRPC).
/// </summary>
[TestClass]
public class CoreHubSignalingTests
{
    private Mock<IChatApiClient> _chatApiClientMock = null!;
    private StubGroupManager _groups = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _chatApiClientMock = new Mock<IChatApiClient>();
        _groups = new StubGroupManager();
        _userId = Guid.NewGuid();
    }

    // ── SendCallOfferAsync ───────────────────────────────────────

    [TestMethod]
    public async Task SendCallOfferAsync_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.SendCallOfferAsync(callId, targetUserId, "v=0\r\nsdp-offer\r\n");

        _chatApiClientMock.Verify(s => s.SendCallOfferAsync(
            callId, targetUserId, "v=0\r\nsdp-offer\r\n", _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendCallOfferAsync_ServiceThrowsUnauthorized_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendCallOfferAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("User not participant"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallOfferAsync_ServiceThrowsInvalidOp_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendCallOfferAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallOfferAsync_ServiceThrowsArgument_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendCallOfferAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("SDP too large"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    // ── SendCallAnswerAsync ──────────────────────────────────────

    [TestMethod]
    public async Task SendCallAnswerAsync_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.SendCallAnswerAsync(callId, targetUserId, "v=0\r\nsdp-answer\r\n");

        _chatApiClientMock.Verify(s => s.SendCallAnswerAsync(
            callId, targetUserId, "v=0\r\nsdp-answer\r\n", _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendCallAnswerAsync_ServiceThrowsUnauthorized_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendCallAnswerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not participant"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), "answer"));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallAnswerAsync_ServiceThrowsInvalidOp_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendCallAnswerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Terminal state"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), "answer"));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    // ── SendIceCandidateAsync ────────────────────────────────────

    [TestMethod]
    public async Task SendIceCandidateAsync_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var candidate = "{\"candidate\":\"a]]\"}";

        await hub.SendIceCandidateAsync(callId, targetUserId, candidate);

        _chatApiClientMock.Verify(s => s.SendIceCandidateAsync(
            callId, targetUserId, candidate, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendIceCandidateAsync_ServiceThrowsUnauthorized_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendIceCandidateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not in call"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendIceCandidateAsync(Guid.NewGuid(), Guid.NewGuid(), "{}"));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task SendIceCandidateAsync_ServiceThrowsArgument_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendIceCandidateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("ICE candidate too large"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendIceCandidateAsync(Guid.NewGuid(), Guid.NewGuid(), "{}"));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    // ── SendMediaStateChangeAsync ────────────────────────────────

    [TestMethod]
    public async Task SendMediaStateChangeAsync_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();

        await hub.SendMediaStateChangeAsync(callId, "Audio", false);

        _chatApiClientMock.Verify(s => s.SendMediaStateChangeAsync(
            callId, "Audio", false, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_Video_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();

        await hub.SendMediaStateChangeAsync(callId, "Video", true);

        _chatApiClientMock.Verify(s => s.SendMediaStateChangeAsync(
            callId, "Video", true, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_ScreenShare_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();

        await hub.SendMediaStateChangeAsync(callId, "ScreenShare", true);

        _chatApiClientMock.Verify(s => s.SendMediaStateChangeAsync(
            callId, "ScreenShare", true, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_ServiceThrowsInvalidOp_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendMediaStateChangeAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call ended"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMediaStateChangeAsync(Guid.NewGuid(), "Audio", true));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_ServiceThrowsArgument_ReturnsHubException()
    {
        _chatApiClientMock
            .Setup(s => s.SendMediaStateChangeAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid media type"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMediaStateChangeAsync(Guid.NewGuid(), "InvalidType", true));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    // ── JoinCallGroupAsync ───────────────────────────────────────

    [TestMethod]
    public async Task JoinCallGroupAsync_AddsConnectionToCallGroup()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();

        await hub.JoinCallGroupAsync(callId);

        Assert.IsTrue(_groups.Operations.Any(o =>
            o.ConnectionId == "conn-signaling" &&
            o.GroupName == $"call-{callId}" &&
            o.Action == "Add"));
    }

    [TestMethod]
    public async Task JoinCallGroupAsync_TracksGroupMembership()
    {
        var tracker = new UserConnectionTracker();
        var hub = CreateHub(tracker: tracker);
        var callId = Guid.NewGuid();

        await hub.JoinCallGroupAsync(callId);

        var groups = tracker.GetGroups(_userId);
        Assert.IsTrue(groups.Contains($"call-{callId}"));
    }

    // ── LeaveCallGroupAsync ──────────────────────────────────────

    [TestMethod]
    public async Task LeaveCallGroupAsync_RemovesConnectionFromCallGroup()
    {
        var hub = CreateHub();
        var callId = Guid.NewGuid();

        await hub.LeaveCallGroupAsync(callId);

        Assert.IsTrue(_groups.Operations.Any(o =>
            o.ConnectionId == "conn-signaling" &&
            o.GroupName == $"call-{callId}" &&
            o.Action == "Remove"));
    }

    [TestMethod]
    public async Task LeaveCallGroupAsync_RemovesGroupMembership()
    {
        var tracker = new UserConnectionTracker();
        var hub = CreateHub(tracker: tracker);
        var callId = Guid.NewGuid();

        // First join, then leave
        await hub.JoinCallGroupAsync(callId);
        await hub.LeaveCallGroupAsync(callId);

        var groups = tracker.GetGroups(_userId);
        Assert.IsFalse(groups.Contains($"call-{callId}"));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private CoreHub CreateHub(UserConnectionTracker? tracker = null)
    {
        tracker ??= new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            _chatApiClientMock.Object,
            Mock.Of<IRealtimeBroadcaster>(),
            NullLogger<CoreHub>.Instance);

        hub.Context = new TestHubCallerContext(_userId, "conn-signaling");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = _groups;

        return hub;
    }
}
