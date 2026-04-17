using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

/// <summary>
/// Tests for video call management hub methods on <see cref="CoreHub"/>:
/// InviteToCallAsync and TransferHostAsync (Phase F — SignalR Hub Updates).
/// </summary>
[TestClass]
public class CoreHubCallManagementTests
{
    private Mock<IVideoCallService> _videoCallServiceMock = null!;
    private StubGroupManager _groups = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _videoCallServiceMock = new Mock<IVideoCallService>();
        _groups = new StubGroupManager();
        _userId = Guid.NewGuid();
    }

    // ══════════════════════════════════════════════════════════════
    // InviteToCallAsync — Happy Path
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_DelegatesToVideoCallService()
    {
        var hub = CreateHubWithVideoCallService();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.InviteToCallAsync(callId, targetUserId);

        _videoCallServiceMock.Verify(s => s.InviteToCallAsync(
            callId,
            targetUserId,
            It.Is<CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InviteToCallAsync_PassesCorrectCallerContext()
    {
        var hub = CreateHubWithVideoCallService();
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        CallerContext? capturedCaller = null;

        _videoCallServiceMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CallerContext, CancellationToken>((_, _, caller, _) => capturedCaller = caller)
            .Returns(Task.CompletedTask);

        await hub.InviteToCallAsync(callId, targetUserId);

        Assert.IsNotNull(capturedCaller);
        Assert.AreEqual(_userId, capturedCaller!.UserId);
        Assert.AreEqual(CallerType.User, capturedCaller.Type);
    }

    [TestMethod]
    public async Task InviteToCallAsync_WithDifferentCallIds_DelegatesEach()
    {
        var hub = CreateHubWithVideoCallService();
        var callId1 = Guid.NewGuid();
        var callId2 = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.InviteToCallAsync(callId1, targetUserId);
        await hub.InviteToCallAsync(callId2, targetUserId);

        _videoCallServiceMock.Verify(s => s.InviteToCallAsync(
            callId1, targetUserId,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        _videoCallServiceMock.Verify(s => s.InviteToCallAsync(
            callId2, targetUserId,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // InviteToCallAsync — Error Handling
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_ServiceThrowsUnauthorized_ReturnsAccessDenied()
    {
        _videoCallServiceMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Only host can invite"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_ServiceThrowsInvalidOp_ReturnsOperationError()
    {
        _videoCallServiceMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_ServiceThrowsArgument_ReturnsInvalidParams()
    {
        _videoCallServiceMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("User already in call"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_NonHostAttempt_UnauthorizedMappedToHubException()
    {
        // Simulates the service rejecting a non-host caller
        _videoCallServiceMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Caller is not the host of this call"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_UserAlreadyInCall_InvalidOpMappedToHubException()
    {
        _videoCallServiceMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User is already a participant in this call"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════
    // TransferHostAsync — Happy Path
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TransferHostAsync_DelegatesToVideoCallService()
    {
        var hub = CreateHubWithVideoCallService();
        var callId = Guid.NewGuid();
        var newHostUserId = Guid.NewGuid();

        await hub.TransferHostAsync(callId, newHostUserId);

        _videoCallServiceMock.Verify(s => s.TransferHostAsync(
            callId,
            newHostUserId,
            It.Is<CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TransferHostAsync_PassesCorrectCallerContext()
    {
        var hub = CreateHubWithVideoCallService();
        var callId = Guid.NewGuid();
        var newHostUserId = Guid.NewGuid();
        CallerContext? capturedCaller = null;

        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CallerContext, CancellationToken>((_, _, caller, _) => capturedCaller = caller)
            .Returns(Task.CompletedTask);

        await hub.TransferHostAsync(callId, newHostUserId);

        Assert.IsNotNull(capturedCaller);
        Assert.AreEqual(_userId, capturedCaller!.UserId);
        Assert.AreEqual(CallerType.User, capturedCaller.Type);
    }

    [TestMethod]
    public async Task TransferHostAsync_WithDifferentTargets_DelegatesCorrectly()
    {
        var hub = CreateHubWithVideoCallService();
        var callId = Guid.NewGuid();
        var newHost1 = Guid.NewGuid();
        var newHost2 = Guid.NewGuid();

        await hub.TransferHostAsync(callId, newHost1);
        await hub.TransferHostAsync(callId, newHost2);

        _videoCallServiceMock.Verify(s => s.TransferHostAsync(
            callId, newHost1,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        _videoCallServiceMock.Verify(s => s.TransferHostAsync(
            callId, newHost2,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // TransferHostAsync — Error Handling
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TransferHostAsync_ServiceThrowsUnauthorized_ReturnsAccessDenied()
    {
        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Only the host can transfer"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_ServiceThrowsInvalidOp_ReturnsOperationError()
    {
        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Target not in call"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_ServiceThrowsArgument_ReturnsInvalidParams()
    {
        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Cannot transfer to self"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_NonHostAttempt_UnauthorizedMappedToHubException()
    {
        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Caller is not the host of this call"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_TargetNotActiveParticipant_InvalidOpMapped()
    {
        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Target user is not an active participant"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_CallNotFound_InvalidOpMapped()
    {
        _videoCallServiceMock
            .Setup(s => s.TransferHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var hub = CreateHubWithVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════
    // EnsureVideoCallServiceAvailable
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_NoVideoCallService_ThrowsHubException()
    {
        var hub = CreateHubWithoutVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Video call services are not available.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_NoVideoCallService_ThrowsHubException()
    {
        var hub = CreateHubWithoutVideoCallService();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual("Video call services are not available.", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════
    // Service Never Called on Error Paths
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_NoVideoCallService_ServiceNeverCalled()
    {
        var hub = CreateHubWithoutVideoCallService();

        try { await hub.InviteToCallAsync(Guid.NewGuid(), Guid.NewGuid()); }
        catch (HubException) { /* expected */ }

        _videoCallServiceMock.Verify(s => s.InviteToCallAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task TransferHostAsync_NoVideoCallService_ServiceNeverCalled()
    {
        var hub = CreateHubWithoutVideoCallService();

        try { await hub.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid()); }
        catch (HubException) { /* expected */ }

        _videoCallServiceMock.Verify(s => s.TransferHostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════
    // Cross-Concern: Signaling + Call Management Coexistence
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_WithSignalingAndVideoCallService_BothAvailable()
    {
        // Ensures the hub can have both signaling and video call service injected
        var signalingMock = new Mock<ICallSignalingService>();
        var hub = CreateHubWithBothServices(signalingMock.Object);
        var callId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await hub.InviteToCallAsync(callId, targetUserId);

        _videoCallServiceMock.Verify(s => s.InviteToCallAsync(
            callId, targetUserId,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TransferHostAsync_WithSignalingAndVideoCallService_BothAvailable()
    {
        var signalingMock = new Mock<ICallSignalingService>();
        var hub = CreateHubWithBothServices(signalingMock.Object);
        var callId = Guid.NewGuid();
        var newHostUserId = Guid.NewGuid();

        await hub.TransferHostAsync(callId, newHostUserId);

        _videoCallServiceMock.Verify(s => s.TransferHostAsync(
            callId, newHostUserId,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // Sequence: Invite then Transfer
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteThenTransfer_BothDelegateCorrectly()
    {
        var hub = CreateHubWithVideoCallService();
        var callId = Guid.NewGuid();
        var invitedUser = Guid.NewGuid();

        // Host invites a user
        await hub.InviteToCallAsync(callId, invitedUser);

        // Host transfers to the newly invited user
        await hub.TransferHostAsync(callId, invitedUser);

        _videoCallServiceMock.Verify(s => s.InviteToCallAsync(
            callId, invitedUser,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        _videoCallServiceMock.Verify(s => s.TransferHostAsync(
            callId, invitedUser,
            It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════

    private CoreHub CreateHubWithVideoCallService(UserConnectionTracker? tracker = null)
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
            callSignalingService: null,
            videoCallService: _videoCallServiceMock.Object);

        hub.Context = new TestHubCallerContext(_userId, "conn-call-mgmt");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = _groups;

        return hub;
    }

    private CoreHub CreateHubWithoutVideoCallService()
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
            callSignalingService: null,
            videoCallService: null);

        hub.Context = new TestHubCallerContext(_userId, "conn-no-call-mgmt");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = new StubGroupManager();

        return hub;
    }

    private CoreHub CreateHubWithBothServices(ICallSignalingService signalingService)
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
            callSignalingService: signalingService,
            videoCallService: _videoCallServiceMock.Object);

        hub.Context = new TestHubCallerContext(_userId, "conn-both-services");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = new StubGroupManager();

        return hub;
    }
}
