using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

/// <summary>
/// Tests for video call management hub methods on <see cref="CoreHub"/>:
/// InviteToCallAsync and TransferHostAsync — now routed through IChatApiClient (gRPC).
/// </summary>
[TestClass]
public class CoreHubCallManagementTests
{
    private Mock<IChatApiClient> _chatApiClientMock = null!;
    private StubGroupManager _groups = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _chatApiClientMock = new Mock<IChatApiClient>();
        _groups = new StubGroupManager();
        _userId = Guid.CreateVersion7();
    }

    // ══════════════════════════════════════════════════════════════
    // InviteToCallAsync — Happy Path
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();

        await hub.InviteToCallAsync(callId, targetUserId);

        _chatApiClientMock.Verify(s => s.InviteToCallAsync(
            callId, targetUserId, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InviteToCallAsync_PassesCorrectUserId()
    {
        var hub = CreateHub();
        var callId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();
        Guid? capturedUserId = null;

        _chatApiClientMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, CancellationToken>((_, _, uid, _) => capturedUserId = uid)
            .ReturnsAsync(true);

        await hub.InviteToCallAsync(callId, targetUserId);

        Assert.IsNotNull(capturedUserId);
        Assert.AreEqual(_userId, capturedUserId!.Value);
    }

    [TestMethod]
    public async Task InviteToCallAsync_WithDifferentCallIds_DelegatesEach()
    {
        var hub = CreateHub();
        var callId1 = Guid.CreateVersion7();
        var callId2 = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();

        await hub.InviteToCallAsync(callId1, targetUserId);
        await hub.InviteToCallAsync(callId2, targetUserId);

        _chatApiClientMock.Verify(s => s.InviteToCallAsync(
            callId1, targetUserId, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
        _chatApiClientMock.Verify(s => s.InviteToCallAsync(
            callId2, targetUserId, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // InviteToCallAsync — Error Handling
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_ServiceThrowsUnauthorized_ReturnsAccessDenied()
    {
        _chatApiClientMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Only host can invite"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_ServiceThrowsInvalidOp_ReturnsOperationError()
    {
        _chatApiClientMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_ServiceThrowsArgument_ReturnsInvalidParams()
    {
        _chatApiClientMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("User already in call"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_NonHostAttempt_UnauthorizedMappedToHubException()
    {
        _chatApiClientMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Caller is not the host of this call"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task InviteToCallAsync_UserAlreadyInCall_InvalidOpMappedToHubException()
    {
        _chatApiClientMock
            .Setup(s => s.InviteToCallAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User is already a participant in this call"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.InviteToCallAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════
    // TransferHostAsync — Happy Path
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TransferHostAsync_DelegatesToChatApiClient()
    {
        var hub = CreateHub();
        var callId = Guid.CreateVersion7();
        var newHostUserId = Guid.CreateVersion7();

        await hub.TransferHostAsync(callId, newHostUserId);

        _chatApiClientMock.Verify(s => s.TransferCallHostAsync(
            callId, newHostUserId, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TransferHostAsync_PassesCorrectUserId()
    {
        var hub = CreateHub();
        var callId = Guid.CreateVersion7();
        var newHostUserId = Guid.CreateVersion7();
        Guid? capturedUserId = null;

        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, CancellationToken>((_, _, uid, _) => capturedUserId = uid)
            .ReturnsAsync(true);

        await hub.TransferHostAsync(callId, newHostUserId);

        Assert.IsNotNull(capturedUserId);
        Assert.AreEqual(_userId, capturedUserId!.Value);
    }

    [TestMethod]
    public async Task TransferHostAsync_WithDifferentTargets_DelegatesCorrectly()
    {
        var hub = CreateHub();
        var callId = Guid.CreateVersion7();
        var newHost1 = Guid.CreateVersion7();
        var newHost2 = Guid.CreateVersion7();

        await hub.TransferHostAsync(callId, newHost1);
        await hub.TransferHostAsync(callId, newHost2);

        _chatApiClientMock.Verify(s => s.TransferCallHostAsync(
            callId, newHost1, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
        _chatApiClientMock.Verify(s => s.TransferCallHostAsync(
            callId, newHost2, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // TransferHostAsync — Error Handling
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TransferHostAsync_ServiceThrowsUnauthorized_ReturnsAccessDenied()
    {
        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Only the host can transfer"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_ServiceThrowsInvalidOp_ReturnsOperationError()
    {
        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Target not in call"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_ServiceThrowsArgument_ReturnsInvalidParams()
    {
        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Cannot transfer to self"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_NonHostAttempt_UnauthorizedMappedToHubException()
    {
        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Caller is not the host of this call"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("Access denied.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_TargetNotActiveParticipant_InvalidOpMapped()
    {
        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Target user is not an active participant"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    [TestMethod]
    public async Task TransferHostAsync_CallNotFound_InvalidOpMapped()
    {
        _chatApiClientMock
            .Setup(s => s.TransferCallHostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var hub = CreateHub();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.TransferHostAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════
    // Sequence: Invite then Transfer
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteThenTransfer_BothDelegateCorrectly()
    {
        var hub = CreateHub();
        var callId = Guid.CreateVersion7();
        var invitedUser = Guid.CreateVersion7();

        await hub.InviteToCallAsync(callId, invitedUser);
        await hub.TransferHostAsync(callId, invitedUser);

        _chatApiClientMock.Verify(s => s.InviteToCallAsync(
            callId, invitedUser, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
        _chatApiClientMock.Verify(s => s.TransferCallHostAsync(
            callId, invitedUser, _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════

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

        hub.Context = new TestHubCallerContext(_userId, "conn-call-mgmt");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = _groups;

        return hub;
    }
}
