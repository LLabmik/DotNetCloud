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
/// Comprehensive tests for Phase C — Mid-Call Participant Addition (InviteToCallAsync).
/// Covers: Host validation, duplicate detection, auto-channel membership, SignalR notification,
/// in-process notification, domain event publishing, and edge cases.
/// </summary>
[TestClass]
public class InviteToCallServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IChatMessageNotifier> _messageNotifierMock = null!;
    private Mock<ILiveKitService> _liveKitMock = null!;
    private Mock<IChannelMemberService> _channelMemberServiceMock = null!;
    private VideoCallService _service = null!;
    private CallerContext _hostCaller = null!;
    private CallerContext _participantCaller = null!;
    private Guid _channelId;
    private Guid _targetUserId;

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
        _channelMemberServiceMock = new Mock<IChannelMemberService>();

        _hostCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _participantCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _targetUserId = Guid.NewGuid();
        _channelId = Guid.NewGuid();

        _service = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object,
            channelMemberService: _channelMemberServiceMock.Object);

        // Seed a channel with host and participant members
        _db.Channels.Add(new Channel
        {
            Id = _channelId,
            Name = "Test Channel",
            Type = ChannelType.Group,
            CreatedByUserId = _hostCaller.UserId
        });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = _channelId, UserId = _hostCaller.UserId, Role = ChannelMemberRole.Owner });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = _channelId, UserId = _participantCaller.UserId, Role = ChannelMemberRole.Member });
        _db.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    /// <summary>Creates an active call with the host as the sole active participant.</summary>
    private async Task<VideoCallDto> CreateActiveCallAsync()
    {
        var call = await _service.InitiateCallAsync(
            _channelId,
            new StartCallRequest { MediaType = "Video" },
            _hostCaller);

        // Join with the participant to transition the call to Active state
        await _service.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            _participantCaller);

        return call;
    }

    /// <summary>Creates a ringing call (not yet active).</summary>
    private async Task<VideoCallDto> CreateRingingCallAsync()
    {
        return await _service.InitiateCallAsync(
            _channelId,
            new StartCallRequest { MediaType = "Video" },
            _hostCaller);
    }

    /// <summary>Adds a user as a channel member in the test database.</summary>
    private void SeedChannelMember(Guid userId)
    {
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = _channelId,
            UserId = userId,
            Role = ChannelMemberRole.Member
        });
        _db.SaveChanges();
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallAsync — Success Cases
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_HostInvitesUser_CreatesParticipantRecord()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        var participant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == _targetUserId);

        Assert.IsNotNull(participant);
        Assert.AreEqual(CallParticipantRole.Participant, participant.Role);
        Assert.AreEqual(ParticipantState.Invited, participant.State);
        Assert.IsNotNull(participant.InvitedAtUtc);
    }

    [TestMethod]
    public async Task InviteToCallAsync_HostInvitesUser_ParticipantHasMediaDisabled()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == _targetUserId);

        Assert.IsFalse(participant.HasAudio);
        Assert.IsFalse(participant.HasVideo);
        Assert.IsFalse(participant.HasScreenShare);
    }

    [TestMethod]
    public async Task InviteToCallAsync_PublishesCallParticipantInvitedEvent()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<CallParticipantInvitedEvent>(e =>
                e.CallId == call.Id &&
                e.ChannelId == _channelId &&
                e.InvitedUserId == _targetUserId &&
                e.InvitedByUserId == _hostCaller.UserId),
                _hostCaller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InviteToCallAsync_SendsSignalRNotificationToTarget()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        _realtimeMock.Verify(
            rt => rt.SendCallInviteAsync(
                _targetUserId,
                call.Id,
                _channelId,
                _hostCaller.UserId,
                It.IsAny<string?>(),
                "Video",
                true,
                It.Is<int>(c => c > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InviteToCallAsync_SendsInProcessNotification()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        _messageNotifierMock.Verify(
            notifier => notifier.NotifyCallInviteReceived(It.Is<CallInviteReceivedNotification>(n =>
                n.CallId == call.Id &&
                n.ChannelId == _channelId &&
                n.InvitedByUserId == _hostCaller.UserId &&
                n.MediaType == "Video" &&
                n.IsMidCallInvite == true &&
                n.ParticipantCount > 0)),
            Times.Once);
    }

    [TestMethod]
    public async Task InviteToCallAsync_ConnectingState_Succeeds()
    {
        // Initiate a call (ringing state), then manually set to Connecting
        var call = await CreateRingingCallAsync();
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        dbCall!.State = VideoCallState.Connecting;
        await _db.SaveChangesAsync();

        SeedChannelMember(_targetUserId);

        // Should not throw — Connecting state is valid for invites
        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        var participant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == _targetUserId);
        Assert.IsNotNull(participant);
    }

    [TestMethod]
    public async Task InviteToCallAsync_MultipleInvites_AllRecorded()
    {
        var call = await CreateActiveCallAsync();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        SeedChannelMember(_targetUserId);
        SeedChannelMember(user2);
        SeedChannelMember(user3);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);
        await _service.InviteToCallAsync(call.Id, user2, _hostCaller);
        await _service.InviteToCallAsync(call.Id, user3, _hostCaller);

        var invitedCount = await _db.CallParticipants
            .CountAsync(cp => cp.VideoCallId == call.Id && cp.State == ParticipantState.Invited);

        Assert.AreEqual(3, invitedCount);
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallAsync — Auto-Add Channel Member
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_NonChannelMember_AutoAddsMember()
    {
        var call = await CreateActiveCallAsync();
        // Do NOT seed _targetUserId as a channel member

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        // Verify the channel member service was called to add the user
        _channelMemberServiceMock.Verify(
            svc => svc.AddMemberAsync(_channelId, _targetUserId, _hostCaller, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InviteToCallAsync_ExistingChannelMember_DoesNotAddAgain()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        // Channel member service should NOT be called since user is already a member
        _channelMemberServiceMock.Verify(
            svc => svc.AddMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task InviteToCallAsync_NonChannelMember_StillCreatesParticipant()
    {
        var call = await CreateActiveCallAsync();
        // Do NOT seed _targetUserId as a channel member

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        var participant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == _targetUserId);

        Assert.IsNotNull(participant);
        Assert.AreEqual(ParticipantState.Invited, participant.State);
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallAsync — Host Validation (Authorization)
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_NonHostCaller_ThrowsUnauthorizedException()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        // participantCaller is not the host
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _service.InviteToCallAsync(call.Id, _targetUserId, _participantCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_NonHostCaller_DoesNotCreateParticipant()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        try
        {
            await _service.InviteToCallAsync(call.Id, _targetUserId, _participantCaller);
        }
        catch (UnauthorizedAccessException) { }

        var participant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == _targetUserId);

        Assert.IsNull(participant);
    }

    [TestMethod]
    public async Task InviteToCallAsync_NonHostCaller_DoesNotPublishEvent()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        try
        {
            await _service.InviteToCallAsync(call.Id, _targetUserId, _participantCaller);
        }
        catch (UnauthorizedAccessException) { }

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<CallParticipantInvitedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallAsync — Invalid Call States
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_RingingState_ThrowsInvalidOperationException()
    {
        var call = await CreateRingingCallAsync();
        SeedChannelMember(_targetUserId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_EndedCall_ThrowsInvalidOperationException()
    {
        var call = await CreateActiveCallAsync();
        await _service.EndCallAsync(call.Id, _hostCaller);
        SeedChannelMember(_targetUserId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_MissedCall_ThrowsInvalidOperationException()
    {
        var call = await CreateRingingCallAsync();
        // Set to Missed state manually
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        dbCall!.State = VideoCallState.Missed;
        dbCall.EndedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SeedChannelMember(_targetUserId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_RejectedCall_ThrowsInvalidOperationException()
    {
        var call = await CreateRingingCallAsync();
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        dbCall!.State = VideoCallState.Rejected;
        dbCall.EndedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SeedChannelMember(_targetUserId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller));
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallAsync — Duplicate / Invalid Target
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_AlreadyActiveParticipant_ThrowsInvalidOperationException()
    {
        var call = await CreateActiveCallAsync();

        // participantCaller is already in the call (joined in CreateActiveCallAsync)
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InviteToCallAsync(call.Id, _participantCaller.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_InviteSelf_ThrowsArgumentException()
    {
        var call = await CreateActiveCallAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            _service.InviteToCallAsync(call.Id, _hostCaller.UserId, _hostCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_NonExistentCall_ThrowsInvalidOperationException()
    {
        SeedChannelMember(_targetUserId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InviteToCallAsync(Guid.NewGuid(), _targetUserId, _hostCaller));
    }

    [TestMethod]
    public async Task InviteToCallAsync_NullCaller_ThrowsArgumentNullException()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            _service.InviteToCallAsync(call.Id, _targetUserId, null!));
    }

    [TestMethod]
    public async Task InviteToCallAsync_PreviouslyLeftParticipant_CanBeReinvited()
    {
        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        // Add target as a participant who already left
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = _targetUserId,
            Role = CallParticipantRole.Participant,
            State = ParticipantState.Left,
            JoinedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            LeftAtUtc = DateTime.UtcNow.AddMinutes(-2)
        });
        await _db.SaveChangesAsync();

        // Re-invite should succeed since they are no longer active
        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        var invitedRecords = await _db.CallParticipants
            .Where(cp => cp.VideoCallId == call.Id && cp.UserId == _targetUserId && cp.State == ParticipantState.Invited)
            .CountAsync();

        Assert.AreEqual(1, invitedRecords);
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallAsync — No Channel Member Service Available
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCallAsync_NoChannelMemberService_NonMember_ThrowsInvalidOperationException()
    {
        // Create a service without channel member service
        var serviceWithoutMemberService = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object);

        var call = await serviceWithoutMemberService.InitiateCallAsync(
            _channelId, new StartCallRequest { MediaType = "Video" }, _hostCaller);

        // Join with participant to make call Active
        await serviceWithoutMemberService.JoinCallAsync(
            call.Id, new JoinCallRequest { WithAudio = true }, _participantCaller);

        // Target is NOT a channel member and no member service is available
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            serviceWithoutMemberService.InviteToCallAsync(call.Id, _targetUserId, _hostCaller));
    }

    // ══════════════════════════════════════════════════════════════
    //  ParticipantState & InvitedAtUtc — Model Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void ParticipantState_DefaultsToJoined()
    {
        var participant = new CallParticipant();
        Assert.AreEqual(ParticipantState.Joined, participant.State);
    }

    [TestMethod]
    public void ParticipantState_InvitedAtUtc_DefaultsToNull()
    {
        var participant = new CallParticipant();
        Assert.IsNull(participant.InvitedAtUtc);
    }

    [TestMethod]
    public void ParticipantState_EnumValuesExist()
    {
        Assert.AreEqual(0, Convert.ToInt32(ParticipantState.Invited));
        Assert.AreEqual(1, Convert.ToInt32(ParticipantState.Joined));
        Assert.AreEqual(2, Convert.ToInt32(ParticipantState.Left));
        Assert.AreEqual(3, Convert.ToInt32(ParticipantState.Rejected));
    }

    [TestMethod]
    public void CallParticipant_InvitedState_HasInvitedAtUtc()
    {
        var now = DateTime.UtcNow;
        var participant = new CallParticipant
        {
            State = ParticipantState.Invited,
            InvitedAtUtc = now
        };

        Assert.AreEqual(ParticipantState.Invited, participant.State);
        Assert.AreEqual(now, participant.InvitedAtUtc);
    }

    // ══════════════════════════════════════════════════════════════
    //  CallParticipantInvitedEvent — Event Model Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CallParticipantInvitedEvent_PropertiesSet()
    {
        var callId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var invitedByUserId = Guid.NewGuid();

        var evt = new CallParticipantInvitedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = callId,
            ChannelId = channelId,
            InvitedUserId = invitedUserId,
            InvitedByUserId = invitedByUserId
        };

        Assert.AreEqual(callId, evt.CallId);
        Assert.AreEqual(channelId, evt.ChannelId);
        Assert.AreEqual(invitedUserId, evt.InvitedUserId);
        Assert.AreEqual(invitedByUserId, evt.InvitedByUserId);
    }

    // ══════════════════════════════════════════════════════════════
    //  CallInviteReceivedNotification — DTO Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CallInviteReceivedNotification_PropertiesSet()
    {
        var notification = new CallInviteReceivedNotification(
            CallId: Guid.NewGuid(),
            ChannelId: Guid.NewGuid(),
            InvitedByUserId: Guid.NewGuid(),
            InvitedByDisplayName: "Alice",
            MediaType: "Video",
            IsMidCallInvite: true,
            ParticipantCount: 3,
            TargetUserId: Guid.NewGuid());

        Assert.IsTrue(notification.IsMidCallInvite);
        Assert.AreEqual("Video", notification.MediaType);
        Assert.AreEqual(3, notification.ParticipantCount);
        Assert.AreEqual("Alice", notification.InvitedByDisplayName);
    }

    [TestMethod]
    public void CallInviteReceivedNotification_NullDisplayName_Allowed()
    {
        var notification = new CallInviteReceivedNotification(
            CallId: Guid.NewGuid(),
            ChannelId: Guid.NewGuid(),
            InvitedByUserId: Guid.NewGuid(),
            InvitedByDisplayName: null,
            MediaType: "Audio",
            IsMidCallInvite: false,
            ParticipantCount: 1,
            TargetUserId: Guid.NewGuid());

        Assert.IsNull(notification.InvitedByDisplayName);
        Assert.IsFalse(notification.IsMidCallInvite);
    }

    // ══════════════════════════════════════════════════════════════
    //  InviteToCallRequest — DTO Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void InviteToCallRequest_UserId_CanBeSet()
    {
        var userId = Guid.NewGuid();
        var request = new InviteToCallRequest { UserId = userId };
        Assert.AreEqual(userId, request.UserId);
    }

    [TestMethod]
    public void InviteToCallRequest_DefaultUserId_IsEmpty()
    {
        var request = new InviteToCallRequest();
        Assert.AreEqual(Guid.Empty, request.UserId);
    }

    // ══════════════════════════════════════════════════════════════
    //  InProcessChatMessageNotifier — CallInviteReceived Event
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void InProcessChatMessageNotifier_CallInviteReceived_FiresEvent()
    {
        var notifier = new InProcessChatMessageNotifier();
        CallInviteReceivedNotification? received = null;

        notifier.CallInviteReceived += n => received = n;

        var notification = new CallInviteReceivedNotification(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Bob", "Video", true, 2, Guid.NewGuid());

        notifier.NotifyCallInviteReceived(notification);

        Assert.IsNotNull(received);
        Assert.AreEqual(notification.CallId, received.CallId);
        Assert.IsTrue(received.IsMidCallInvite);
    }

    [TestMethod]
    public void InProcessChatMessageNotifier_CallInviteReceived_NoSubscribers_DoesNotThrow()
    {
        var notifier = new InProcessChatMessageNotifier();

        var notification = new CallInviteReceivedNotification(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Bob", "Video", true, 2, Guid.NewGuid());

        // Should not throw when no subscribers
        notifier.NotifyCallInviteReceived(notification);
    }

    // ══════════════════════════════════════════════════════════════
    //  ChatRealtimeService — SendCallInviteAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ChatRealtimeService_SendCallInviteAsync_NoBroadcaster_DoesNotThrow()
    {
        // Create realtime service without a broadcaster (standalone mode)
        var service = new ChatRealtimeService(
            NullLogger<ChatRealtimeService>.Instance,
            broadcaster: null);

        // Should be a no-op without throwing
        await service.SendCallInviteAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Alice", "Video", true, 3, CancellationToken.None);
    }

    [TestMethod]
    public async Task ChatRealtimeService_SendCallInviteAsync_WithBroadcaster_SendsToUser()
    {
        var broadcasterMock = new Mock<DotNetCloud.Core.Capabilities.IRealtimeBroadcaster>();
        var service = new ChatRealtimeService(
            NullLogger<ChatRealtimeService>.Instance,
            broadcaster: broadcasterMock.Object);

        var targetUserId = Guid.NewGuid();
        var callId = Guid.NewGuid();

        await service.SendCallInviteAsync(
            targetUserId, callId, Guid.NewGuid(),
            Guid.NewGuid(), "Alice", "Video", true, 3, CancellationToken.None);

        broadcasterMock.Verify(
            b => b.SendToUserAsync(
                targetUserId,
                "CallInviteReceived",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    //  Integration: Invite → Join Workflow
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InviteToCall_ThenJoin_WorksCorrectly()
    {
        // Increase P2P limit to allow more participants
        _liveKitMock.Setup(x => x.MaxP2PParticipants).Returns(10);

        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        // Now the invited user joins the call
        var targetCaller = new CallerContext(_targetUserId, ["user"], CallerType.User);
        var joinedCall = await _service.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            targetCaller);

        // Should have 3 participants now: host, participant, and invited-then-joined target
        var activeParticipants = joinedCall.Participants
            .Where(p => p.LeftAtUtc == null)
            .ToList();

        Assert.IsTrue(activeParticipants.Count >= 3);
        Assert.IsTrue(activeParticipants.Any(p => p.UserId == _targetUserId));
    }

    [TestMethod]
    public async Task InviteToCall_ThenLeave_ThenReinvite_Succeeds()
    {
        // Increase P2P limit to allow more participants
        _liveKitMock.Setup(x => x.MaxP2PParticipants).Returns(10);

        var call = await CreateActiveCallAsync();
        SeedChannelMember(_targetUserId);

        // Invite and join
        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);
        var targetCaller = new CallerContext(_targetUserId, ["user"], CallerType.User);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, targetCaller);

        // Leave the call
        await _service.LeaveCallAsync(call.Id, targetCaller);

        // Re-invite should succeed since they left
        await _service.InviteToCallAsync(call.Id, _targetUserId, _hostCaller);

        var invitedRecords = await _db.CallParticipants
            .CountAsync(cp => cp.VideoCallId == call.Id
                && cp.UserId == _targetUserId
                && cp.LeftAtUtc == null);

        Assert.IsTrue(invitedRecords > 0);
    }
}
