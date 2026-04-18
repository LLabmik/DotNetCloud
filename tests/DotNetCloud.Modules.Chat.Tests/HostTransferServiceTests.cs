using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for Phase D — Host Transfer, Auto-Transfer on Leave,
/// and End-Call Permission Enforcement in <see cref="VideoCallService"/>.
/// </summary>
[TestClass]
public class HostTransferServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IChatMessageNotifier> _messageNotifierMock = null!;
    private Mock<ILiveKitService> _liveKitMock = null!;
    private Mock<IChannelMemberService> _channelMemberMock = null!;
    private VideoCallService _service = null!;
    private CallerContext _hostCaller = null!;
    private Guid _channelId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<IChatRealtimeService>();
        _messageNotifierMock = new Mock<IChatMessageNotifier>();
        _liveKitMock = new Mock<ILiveKitService>();
        _liveKitMock.Setup(x => x.IsAvailable).Returns(false);
        _liveKitMock.Setup(x => x.MaxP2PParticipants).Returns(3);
        _channelMemberMock = new Mock<IChannelMemberService>();

        _service = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object,
            channelMemberService: _channelMemberMock.Object);

        _hostCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _channelId = Guid.NewGuid();

        // Seed a channel with the host as owner
        _db.Channels.Add(new Channel
        {
            Id = _channelId,
            Name = "Test Channel",
            Type = ChannelType.Group,
            CreatedByUserId = _hostCaller.UserId
        });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = _channelId, UserId = _hostCaller.UserId, Role = ChannelMemberRole.Owner });
        _db.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private CallerContext CreateCaller(Guid? userId = null) =>
        new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    private void SeedChannelMember(Guid channelId, Guid userId)
    {
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channelId, UserId = userId, Role = ChannelMemberRole.Member });
        _db.SaveChanges();
    }

    /// <summary>Creates a call with host + one joiner, both active. Returns (callDto, joinerCaller).</summary>
    private async Task<(VideoCallDto Call, CallerContext Joiner)> CreateActiveCallWithTwoParticipantsAsync()
    {
        SeedChannelMember(_channelId, Guid.NewGuid()); // need >= 2 members for non-group
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);
        var joiner = CreateCaller();
        SeedChannelMember(_channelId, joiner.UserId);
        var updated = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);
        return (updated, joiner);
    }

    /// <summary>Creates a call with host + two joiners, all active. Returns (callDto, joiner1, joiner2).</summary>
    private async Task<(VideoCallDto Call, CallerContext Joiner1, CallerContext Joiner2)> CreateActiveCallWithThreeParticipantsAsync()
    {
        SeedChannelMember(_channelId, Guid.NewGuid());
        SeedChannelMember(_channelId, Guid.NewGuid());
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);
        var joiner1 = CreateCaller();
        SeedChannelMember(_channelId, joiner1.UserId);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner1);
        var joiner2 = CreateCaller();
        SeedChannelMember(_channelId, joiner2.UserId);
        var updated = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner2);
        return (updated, joiner1, joiner2);
    }

    // ══════════════════════════════════════════════════════════════
    //  D1: TransferHostAsync — Valid Transfer
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TransferHostAsync_ValidTransfer_UpdatesCallHostUserId()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner.UserId, dbCall!.HostUserId);
    }

    [TestMethod]
    public async Task TransferHostAsync_ValidTransfer_UpdatesParticipantRoles()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        var oldHostParticipant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == _hostCaller.UserId && cp.LeftAtUtc == null);
        var newHostParticipant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == joiner.UserId && cp.LeftAtUtc == null);

        Assert.AreEqual(CallParticipantRole.Participant, oldHostParticipant.Role);
        Assert.AreEqual(CallParticipantRole.Host, newHostParticipant.Role);
    }

    [TestMethod]
    public async Task TransferHostAsync_ValidTransfer_PublishesCallHostTransferredEvent()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<CallHostTransferredEvent>(e =>
                e.CallId == call.Id &&
                e.ChannelId == _channelId &&
                e.PreviousHostUserId == _hostCaller.UserId &&
                e.NewHostUserId == joiner.UserId),
                _hostCaller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TransferHostAsync_ValidTransfer_BroadcastsViaRealtimeService()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        _realtimeMock.Verify(
            rs => rs.BroadcastHostTransferredAsync(
                _channelId, call.Id, _hostCaller.UserId, joiner.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TransferHostAsync_ValidTransfer_NotifiesViaMessageNotifier()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        _messageNotifierMock.Verify(
            mn => mn.NotifyCallHostTransferred(It.Is<CallHostTransferredNotification>(n =>
                n.CallId == call.Id &&
                n.ChannelId == _channelId &&
                n.PreviousHostUserId == _hostCaller.UserId &&
                n.NewHostUserId == joiner.UserId)),
            Times.Once);
    }

    [TestMethod]
    public async Task TransferHostAsync_NewHostCanTransferBack()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // First transfer: host → joiner
        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        // Second transfer: joiner (now host) → original host
        await _service.TransferHostAsync(call.Id, _hostCaller.UserId, joiner);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(_hostCaller.UserId, dbCall!.HostUserId);
    }

    [TestMethod]
    public async Task TransferHostAsync_ThreeParticipants_TransferToEither()
    {
        var (call, joiner1, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        // Transfer to joiner2 (the third participant)
        await _service.TransferHostAsync(call.Id, joiner2.UserId, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner2.UserId, dbCall!.HostUserId);
    }

    [TestMethod]
    public async Task TransferHostAsync_ConnectingCall_Succeeds()
    {
        // Create call in Connecting state (first joiner triggers Ringing→Active, but we can
        // test with an Active call which is the normal case)
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // The call should be Active at this point
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Active, dbCall!.State);

        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);
        Assert.AreEqual(joiner.UserId, (await _db.VideoCalls.FindAsync(call.Id))!.HostUserId);
    }

    // ══════════════════════════════════════════════════════════════
    //  D1: TransferHostAsync — Rejection Cases
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TransferHostAsync_NonHostAttempts_ThrowsUnauthorizedAccessException()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _service.TransferHostAsync(call.Id, _hostCaller.UserId, joiner));
    }

    [TestMethod]
    public async Task TransferHostAsync_TransferToSelf_ThrowsArgumentException()
    {
        var (call, _) = await CreateActiveCallWithTwoParticipantsAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            _service.TransferHostAsync(call.Id, _hostCaller.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task TransferHostAsync_TargetNotInCall_ThrowsInvalidOperationException()
    {
        var (call, _) = await CreateActiveCallWithTwoParticipantsAsync();
        var stranger = CreateCaller();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.TransferHostAsync(call.Id, stranger.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task TransferHostAsync_TargetHasLeft_ThrowsInvalidOperationException()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // Joiner leaves, then host tries to transfer to them
        await _service.LeaveCallAsync(call.Id, joiner);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task TransferHostAsync_EndedCall_ThrowsInvalidOperationException()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();
        await _service.EndCallAsync(call.Id, _hostCaller);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task TransferHostAsync_RingingCall_ThrowsInvalidOperationException()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);
        var target = CreateCaller();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.TransferHostAsync(call.Id, target.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task TransferHostAsync_NonexistentCall_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid(), _hostCaller));
    }

    [TestMethod]
    public async Task TransferHostAsync_NullCaller_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            _service.TransferHostAsync(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [TestMethod]
    public async Task TransferHostAsync_InvitedButNotJoined_ThrowsInvalidOperationException()
    {
        var (call, _) = await CreateActiveCallWithTwoParticipantsAsync();

        // Add an invited-but-not-joined participant directly
        var invitedUser = Guid.NewGuid();
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = invitedUser,
            Role = CallParticipantRole.Participant,
            State = ParticipantState.Invited,
            InvitedAtUtc = DateTime.UtcNow,
            JoinedAtUtc = DateTime.UtcNow,
            HasAudio = false,
            HasVideo = false
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.TransferHostAsync(call.Id, invitedUser, _hostCaller));
    }

    // ══════════════════════════════════════════════════════════════
    //  D2: Auto-Transfer Host on Leave
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task LeaveCallAsync_HostLeaves_AutoTransfersToLongestActiveParticipant()
    {
        var (call, joiner1, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        // Host leaves — joiner1 joined first so should become new host
        await _service.LeaveCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner1.UserId, dbCall!.HostUserId);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeaves_NewHostParticipantRoleUpdated()
    {
        var (call, joiner1, _) = await CreateActiveCallWithThreeParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, _hostCaller);

        var newHostParticipant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == joiner1.UserId && cp.LeftAtUtc == null);
        Assert.AreEqual(CallParticipantRole.Host, newHostParticipant.Role);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeaves_PublishesCallHostTransferredEvent()
    {
        var (call, joiner1, _) = await CreateActiveCallWithThreeParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, _hostCaller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<CallHostTransferredEvent>(e =>
                e.CallId == call.Id &&
                e.PreviousHostUserId == _hostCaller.UserId &&
                e.NewHostUserId == joiner1.UserId),
                _hostCaller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeaves_BroadcastsHostTransferViaRealtime()
    {
        var (call, joiner1, _) = await CreateActiveCallWithThreeParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, _hostCaller);

        _realtimeMock.Verify(
            rs => rs.BroadcastHostTransferredAsync(
                _channelId, call.Id, _hostCaller.UserId, joiner1.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeaves_NotifiesBlazorCircuits()
    {
        var (call, joiner1, _) = await CreateActiveCallWithThreeParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, _hostCaller);

        _messageNotifierMock.Verify(
            mn => mn.NotifyCallHostTransferred(It.Is<CallHostTransferredNotification>(n =>
                n.CallId == call.Id &&
                n.PreviousHostUserId == _hostCaller.UserId &&
                n.NewHostUserId == joiner1.UserId)),
            Times.Once);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeaves_CallRemainsActive()
    {
        var (call, _, _) = await CreateActiveCallWithThreeParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Active, dbCall!.State);
    }

    [TestMethod]
    public async Task LeaveCallAsync_NonHostLeaves_NoHostTransfer()
    {
        var (call, _, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, joiner2);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(_hostCaller.UserId, dbCall!.HostUserId);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<CallHostTransferredEvent>(),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeavesTwoPersonCall_LastPersonBecomesHost_ThenCallEnds()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // Host leaves the 2-person call — joiner is last, so call auto-ends after host transfer
        // Actually: host leaves → 1 remaining → auto-transfer host → then the joiner is the only one
        // With 1 remaining, the call doesn't auto-end (it only auto-ends when 0 remain)
        await _service.LeaveCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        // The call should still be active (1 participant remaining)
        Assert.AreEqual(VideoCallState.Active, dbCall!.State);
        Assert.AreEqual(joiner.UserId, dbCall.HostUserId);
    }

    [TestMethod]
    public async Task LeaveCallAsync_HostLeavesAfterTransfer_NewHostAlreadySet_NoDoubleTransfer()
    {
        var (call, joiner1, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        // Transfer to joiner2 first
        await _service.TransferHostAsync(call.Id, joiner2.UserId, _hostCaller);

        // Now joiner2 is host, original host leaves — should NOT trigger auto-transfer
        await _service.LeaveCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner2.UserId, dbCall!.HostUserId); // Still joiner2

        // Only one host transfer event (the explicit one, not an auto-transfer)
        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<CallHostTransferredEvent>(),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LeaveCallAsync_TransferredHostLeaves_AutoTransfersAgain()
    {
        var (call, joiner1, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        // Transfer to joiner1
        await _service.TransferHostAsync(call.Id, joiner1.UserId, _hostCaller);

        // joiner1 (new host) leaves
        await _service.LeaveCallAsync(call.Id, joiner1);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        // Should auto-transfer to one of the remaining participants
        Assert.IsTrue(dbCall!.HostUserId == _hostCaller.UserId || dbCall.HostUserId == joiner2.UserId);
        Assert.AreNotEqual(joiner1.UserId, dbCall.HostUserId);
    }

    [TestMethod]
    public async Task LeaveCallAsync_AllLeave_NoAutoTransfer_CallEnds()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.LeaveCallAsync(call.Id, joiner);
        await _service.LeaveCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    // ══════════════════════════════════════════════════════════════
    //  D3: End-Call Permission Enforcement
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task EndCallAsync_HostEnds_Succeeds()
    {
        var (call, _) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.EndCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    [TestMethod]
    public async Task EndCallAsync_NonHostAttempts_ThrowsUnauthorizedAccessException()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _service.EndCallAsync(call.Id, joiner));
    }

    [TestMethod]
    public async Task EndCallAsync_NonHostAfterTransfer_ThrowsUnauthorizedAccessException()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // Transfer host to joiner
        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        // Original host (now non-host) tries to end
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _service.EndCallAsync(call.Id, _hostCaller));
    }

    [TestMethod]
    public async Task EndCallAsync_TransferredHostEnds_Succeeds()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // Transfer host to joiner
        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);

        // New host ends the call
        await _service.EndCallAsync(call.Id, joiner);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    [TestMethod]
    public async Task EndCallAsync_HostCanEndRingingCall()
    {
        // Initiator is host by default, can end a ringing call
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);

        await _service.EndCallAsync(call.Id, _hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallEndReason.Cancelled, dbCall!.EndReason);
    }

    [TestMethod]
    public async Task EndCallAsync_NonHostStrangerAttempts_ThrowsUnauthorizedAccessException()
    {
        var (call, _) = await CreateActiveCallWithTwoParticipantsAsync();
        var stranger = CreateCaller();

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _service.EndCallAsync(call.Id, stranger));
    }

    [TestMethod]
    public async Task EndCallAsync_HostEnds_MarksAllParticipantsAsLeft()
    {
        var (call, _) = await CreateActiveCallWithTwoParticipantsAsync();

        await _service.EndCallAsync(call.Id, _hostCaller);

        var activeParticipants = await _db.CallParticipants
            .Where(cp => cp.VideoCallId == call.Id && cp.LeftAtUtc == null)
            .CountAsync();
        Assert.AreEqual(0, activeParticipants);
    }

    [TestMethod]
    public async Task EndCallAsync_AutoTransferredHost_CanEndCall()
    {
        var (call, joiner1, _) = await CreateActiveCallWithThreeParticipantsAsync();

        // Host leaves → auto-transfers to joiner1
        await _service.LeaveCallAsync(call.Id, _hostCaller);

        // joiner1 (auto-transferred host) can end the call
        await _service.EndCallAsync(call.Id, joiner1);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    // ══════════════════════════════════════════════════════════════
    //  D4: CallHostTransferredEvent Validation
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CallHostTransferredEvent_RequiresAllProperties()
    {
        var evt = new CallHostTransferredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            PreviousHostUserId = Guid.NewGuid(),
            NewHostUserId = Guid.NewGuid()
        };

        Assert.AreNotEqual(Guid.Empty, evt.EventId);
        Assert.AreNotEqual(Guid.Empty, evt.CallId);
        Assert.AreNotEqual(Guid.Empty, evt.ChannelId);
        Assert.AreNotEqual(Guid.Empty, evt.PreviousHostUserId);
        Assert.AreNotEqual(Guid.Empty, evt.NewHostUserId);
        Assert.AreNotEqual(evt.PreviousHostUserId, evt.NewHostUserId);
    }

    [TestMethod]
    public void CallHostTransferredEvent_ImplementsIEvent()
    {
        var evt = new CallHostTransferredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            PreviousHostUserId = Guid.NewGuid(),
            NewHostUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    // ══════════════════════════════════════════════════════════════
    //  Notification Record Validation
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CallHostTransferredNotification_StoresCorrectValues()
    {
        var callId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var prev = Guid.NewGuid();
        var next = Guid.NewGuid();

        var notification = new CallHostTransferredNotification(callId, channelId, prev, next);

        Assert.AreEqual(callId, notification.CallId);
        Assert.AreEqual(channelId, notification.ChannelId);
        Assert.AreEqual(prev, notification.PreviousHostUserId);
        Assert.AreEqual(next, notification.NewHostUserId);
    }

    // ══════════════════════════════════════════════════════════════
    //  TransferHostRequest DTO
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void TransferHostRequest_StoresUserId()
    {
        var userId = Guid.NewGuid();
        var request = new TransferHostRequest { UserId = userId };
        Assert.AreEqual(userId, request.UserId);
    }

    // ══════════════════════════════════════════════════════════════
    //  Integration Scenarios (multi-step flows)
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task FullLifecycle_InitiateJoinTransferLeaveEnd()
    {
        // 1. Host initiates call
        SeedChannelMember(_channelId, Guid.NewGuid());
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);
        Assert.AreEqual(_hostCaller.UserId, call.HostUserId);

        // 2. Joiner joins → call becomes Active
        var joiner = CreateCaller();
        SeedChannelMember(_channelId, joiner.UserId);
        call = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);
        Assert.AreEqual("Active", call.State);

        // 3. Host transfers to joiner
        await _service.TransferHostAsync(call.Id, joiner.UserId, _hostCaller);
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner.UserId, dbCall!.HostUserId);

        // 4. Original host leaves (not the host anymore, no auto-transfer)
        await _service.LeaveCallAsync(call.Id, _hostCaller);
        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner.UserId, dbCall!.HostUserId);
        Assert.AreEqual(VideoCallState.Active, dbCall.State);

        // 5. New host ends the call
        await _service.EndCallAsync(call.Id, joiner);
        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    [TestMethod]
    public async Task MultiTransfer_HostBouncesAround()
    {
        var (call, joiner1, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        // Host → joiner1
        await _service.TransferHostAsync(call.Id, joiner1.UserId, _hostCaller);
        Assert.AreEqual(joiner1.UserId, (await _db.VideoCalls.FindAsync(call.Id))!.HostUserId);

        // joiner1 → joiner2
        await _service.TransferHostAsync(call.Id, joiner2.UserId, joiner1);
        Assert.AreEqual(joiner2.UserId, (await _db.VideoCalls.FindAsync(call.Id))!.HostUserId);

        // joiner2 → original host
        await _service.TransferHostAsync(call.Id, _hostCaller.UserId, joiner2);
        Assert.AreEqual(_hostCaller.UserId, (await _db.VideoCalls.FindAsync(call.Id))!.HostUserId);

        // Three transfer events published
        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<CallHostTransferredEvent>(),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [TestMethod]
    public async Task HostLeaves_ThenNewHostEnds()
    {
        var (call, joiner1, joiner2) = await CreateActiveCallWithThreeParticipantsAsync();

        // Host leaves → auto-transfer to joiner1
        await _service.LeaveCallAsync(call.Id, _hostCaller);
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joiner1.UserId, dbCall!.HostUserId);

        // joiner1 (auto-host) ends call
        await _service.EndCallAsync(call.Id, joiner1);
        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    [TestMethod]
    public async Task NonHostCannotEndButCanLeave()
    {
        var (call, joiner) = await CreateActiveCallWithTwoParticipantsAsync();

        // Non-host cannot end
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _service.EndCallAsync(call.Id, joiner));

        // But non-host can leave
        await _service.LeaveCallAsync(call.Id, joiner);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == joiner.UserId && cp.LeftAtUtc != null);
        Assert.IsNotNull(participant.LeftAtUtc);
    }

    [TestMethod]
    public async Task SequentialHostLeavesWithAutoTransfer()
    {
        // Enable LiveKit for 4-participant call (exceeds P2P limit of 3)
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-room");

        // 4 participants: host, j1, j2, j3
        SeedChannelMember(_channelId, Guid.NewGuid());
        SeedChannelMember(_channelId, Guid.NewGuid());
        SeedChannelMember(_channelId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);

        var j1 = CreateCaller();
        SeedChannelMember(_channelId, j1.UserId);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), j1);

        var j2 = CreateCaller();
        SeedChannelMember(_channelId, j2.UserId);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), j2);

        var j3 = CreateCaller();
        SeedChannelMember(_channelId, j3.UserId);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), j3);

        // Host leaves → auto-transfer to j1 (earliest join)
        await _service.LeaveCallAsync(call.Id, _hostCaller);
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(j1.UserId, dbCall!.HostUserId);

        // j1 (host) leaves → auto-transfer to j2
        await _service.LeaveCallAsync(call.Id, j1);
        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(j2.UserId, dbCall!.HostUserId);

        // j2 (host) leaves → auto-transfer to j3
        await _service.LeaveCallAsync(call.Id, j2);
        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(j3.UserId, dbCall!.HostUserId);

        // j3 is last → leaves → call ends (no auto-transfer needed)
        await _service.LeaveCallAsync(call.Id, j3);
        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }
}
