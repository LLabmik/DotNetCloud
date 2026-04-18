using System.Security.Claims;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

/// <summary>
/// Tests for video call signaling methods on <see cref="CoreHub"/>:
/// SendCallOfferAsync, SendCallAnswerAsync, SendIceCandidateAsync,
/// SendMediaStateChangeAsync, JoinCallGroupAsync, LeaveCallGroupAsync.
/// </summary>
[TestClass]
public class CoreHubSignalingTests
{
    private Mock<ICallSignalingService> _signalingMock = null!;
    private StubGroupManager _groups = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _signalingMock = new Mock<ICallSignalingService>();
        _groups = new StubGroupManager();
        _userId = Guid.NewGuid();
    }

    // ── SendCallOfferAsync ───────────────────────────────────────

    [TestMethod]
    public async Task SendCallOfferAsync_DelegatesToSignalingService()
    {
        var hub = CreateHubWithSignaling();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.SendCallOfferAsync(callId, targetUserId, "v=0\r\nsdp-offer\r\n");

        _signalingMock.Verify(s => s.SendOfferAsync(
            callId,
            targetUserId,
            "v=0\r\nsdp-offer\r\n",
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendCallOfferAsync_ServiceThrowsUnauthorized_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendOfferAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("User not participant"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallOfferAsync_ServiceThrowsInvalidOp_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendOfferAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallOfferAsync_ServiceThrowsArgument_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendOfferAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("SDP too large"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    // ── SendCallAnswerAsync ──────────────────────────────────────

    [TestMethod]
    public async Task SendCallAnswerAsync_DelegatesToSignalingService()
    {
        var hub = CreateHubWithSignaling();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.SendCallAnswerAsync(callId, targetUserId, "v=0\r\nsdp-answer\r\n");

        _signalingMock.Verify(s => s.SendAnswerAsync(
            callId,
            targetUserId,
            "v=0\r\nsdp-answer\r\n",
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendCallAnswerAsync_ServiceThrowsUnauthorized_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendAnswerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not participant"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), "answer"));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallAnswerAsync_ServiceThrowsInvalidOp_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendAnswerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Terminal state"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), "answer"));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    // ── SendIceCandidateAsync ────────────────────────────────────

    [TestMethod]
    public async Task SendIceCandidateAsync_DelegatesToSignalingService()
    {
        var hub = CreateHubWithSignaling();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var candidate = "{\"candidate\":\"a]]\"}";

        await hub.SendIceCandidateAsync(callId, targetUserId, candidate);

        _signalingMock.Verify(s => s.SendIceCandidateAsync(
            callId,
            targetUserId,
            candidate,
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendIceCandidateAsync_ServiceThrowsUnauthorized_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendIceCandidateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not in call"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendIceCandidateAsync(Guid.NewGuid(), Guid.NewGuid(), "{}"));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task SendIceCandidateAsync_ServiceThrowsArgument_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendIceCandidateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("ICE candidate too large"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendIceCandidateAsync(Guid.NewGuid(), Guid.NewGuid(), "{}"));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    // ── SendMediaStateChangeAsync ────────────────────────────────

    [TestMethod]
    public async Task SendMediaStateChangeAsync_DelegatesToSignalingService()
    {
        var hub = CreateHubWithSignaling();
        var callId = Guid.NewGuid();

        await hub.SendMediaStateChangeAsync(callId, "Audio", false);

        _signalingMock.Verify(s => s.SendMediaStateChangeAsync(
            callId,
            "Audio",
            false,
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_Video_DelegatesToSignalingService()
    {
        var hub = CreateHubWithSignaling();
        var callId = Guid.NewGuid();

        await hub.SendMediaStateChangeAsync(callId, "Video", true);

        _signalingMock.Verify(s => s.SendMediaStateChangeAsync(
            callId,
            "Video",
            true,
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_ScreenShare_DelegatesToSignalingService()
    {
        var hub = CreateHubWithSignaling();
        var callId = Guid.NewGuid();

        await hub.SendMediaStateChangeAsync(callId, "ScreenShare", true);

        _signalingMock.Verify(s => s.SendMediaStateChangeAsync(
            callId,
            "ScreenShare",
            true,
            It.Is<DotNetCloud.Core.Authorization.CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_ServiceThrowsInvalidOp_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendMediaStateChangeAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call ended"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMediaStateChangeAsync(Guid.NewGuid(), "Audio", true));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_ServiceThrowsArgument_ReturnsHubException()
    {
        _signalingMock
            .Setup(s => s.SendMediaStateChangeAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid media type"));

        var hub = CreateHubWithSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMediaStateChangeAsync(Guid.NewGuid(), "InvalidType", true));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    // ── JoinCallGroupAsync ───────────────────────────────────────

    [TestMethod]
    public async Task JoinCallGroupAsync_AddsConnectionToCallGroup()
    {
        var hub = CreateHubWithSignaling();
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
        var hub = CreateHubWithSignaling(tracker: tracker);
        var callId = Guid.NewGuid();

        await hub.JoinCallGroupAsync(callId);

        var groups = tracker.GetGroups(_userId);
        Assert.IsTrue(groups.Contains($"call-{callId}"));
    }

    // ── LeaveCallGroupAsync ──────────────────────────────────────

    [TestMethod]
    public async Task LeaveCallGroupAsync_RemovesConnectionFromCallGroup()
    {
        var hub = CreateHubWithSignaling();
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
        var hub = CreateHubWithSignaling(tracker: tracker);
        var callId = Guid.NewGuid();

        // First join, then leave
        await hub.JoinCallGroupAsync(callId);
        await hub.LeaveCallGroupAsync(callId);

        var groups = tracker.GetGroups(_userId);
        Assert.IsFalse(groups.Contains($"call-{callId}"));
    }

    // ── EnsureCallSignalingAvailable ─────────────────────────────

    [TestMethod]
    public async Task SendCallOfferAsync_NoSignalingService_ThrowsHubException()
    {
        var hub = CreateHubWithoutSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallOfferAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("Call signaling services are not available.", ex.Message);
    }

    [TestMethod]
    public async Task SendCallAnswerAsync_NoSignalingService_ThrowsHubException()
    {
        var hub = CreateHubWithoutSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendCallAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), "sdp"));

        Assert.AreEqual("Call signaling services are not available.", ex.Message);
    }

    [TestMethod]
    public async Task SendIceCandidateAsync_NoSignalingService_ThrowsHubException()
    {
        var hub = CreateHubWithoutSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendIceCandidateAsync(Guid.NewGuid(), Guid.NewGuid(), "{}"));

        Assert.AreEqual("Call signaling services are not available.", ex.Message);
    }

    [TestMethod]
    public async Task SendMediaStateChangeAsync_NoSignalingService_ThrowsHubException()
    {
        var hub = CreateHubWithoutSignaling();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMediaStateChangeAsync(Guid.NewGuid(), "Audio", true));

        Assert.AreEqual("Call signaling services are not available.", ex.Message);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private CoreHub CreateHubWithSignaling(UserConnectionTracker? tracker = null)
    {
        tracker ??= new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            messageService: null,
            channelMemberService: null,
            reactionService: null,
            typingIndicatorService: null,
            chatRealtimeService: null,
            NullLogger<CoreHub>.Instance,
            callSignalingService: _signalingMock.Object);

        hub.Context = new TestHubCallerContext(_userId, "conn-signaling");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = _groups;

        return hub;
    }

    private CoreHub CreateHubWithoutSignaling()
    {
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            messageService: null,
            channelMemberService: null,
            reactionService: null,
            typingIndicatorService: null,
            chatRealtimeService: null,
            NullLogger<CoreHub>.Instance,
            callSignalingService: null);

        hub.Context = new TestHubCallerContext(_userId, "conn-no-signaling");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = new StubGroupManager();

        return hub;
    }
}
