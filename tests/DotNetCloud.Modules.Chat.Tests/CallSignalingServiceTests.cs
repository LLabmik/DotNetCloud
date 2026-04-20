using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="CallSignalingService"/>.
/// Covers SDP/ICE relay, payload validation, call state gating, participant membership,
/// media state changes, screen share events, and call-scoped group management.
/// </summary>
[TestClass]
public class CallSignalingServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IRealtimeBroadcaster> _broadcasterMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallSignalingService _service = null!;
    private CallerContext _caller = null!;
    private CallerContext _target = null!;
    private Guid _callId;
    private Guid _channelId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);
        _broadcasterMock = new Mock<IRealtimeBroadcaster>();
        _eventBusMock = new Mock<IEventBus>();

        _service = new CallSignalingService(
            _db,
            _eventBusMock.Object,
            NullLogger<CallSignalingService>.Instance,
            _broadcasterMock.Object);

        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _target = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _channelId = Guid.NewGuid();
        _callId = Guid.NewGuid();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private void SeedActiveCall(Guid callId, Guid channelId, VideoCallState state, params (Guid userId, bool active)[] participants)
    {
        var initiatorId = participants.Length > 0 ? participants[0].userId : Guid.NewGuid();
        _db.VideoCalls.Add(new VideoCall
        {
            Id = callId,
            ChannelId = channelId,
            InitiatorUserId = initiatorId,
            HostUserId = initiatorId,
            State = state,
            MediaType = CallMediaType.Video,
            MaxParticipants = participants.Length,
            CreatedAtUtc = DateTime.UtcNow,
            StartedAtUtc = state >= VideoCallState.Active ? DateTime.UtcNow : null
        });

        foreach (var (userId, active) in participants)
        {
            _db.CallParticipants.Add(new CallParticipant
            {
                VideoCallId = callId,
                UserId = userId,
                Role = userId == participants[0].userId ? CallParticipantRole.Host : CallParticipantRole.Participant,
                JoinedAtUtc = DateTime.UtcNow,
                LeftAtUtc = active ? null : DateTime.UtcNow,
                HasAudio = true,
                HasVideo = true
            });
        }

        _db.SaveChanges();
    }

    // ── SendOfferAsync ───────────────────────────────────────────

    [TestMethod]
    public async Task SendOffer_ValidCall_RelaysToTargetUser()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\no=- 123 IN IP4 0.0.0.0\r\n", _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveCallOffer",
            It.Is<object>(o => o.ToString()!.Contains(_callId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendOffer_ConnectingState_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Connecting,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveCallOffer",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendOffer_RingingState_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Ringing,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveCallOffer",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendOffer_CallNotFound_ThrowsInvalidOperation()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendOfferAsync(Guid.NewGuid(), _target.UserId, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_TerminalState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Ended,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_MissedState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Missed,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_RejectedState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Rejected,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_FailedState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Failed,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_CallerNotParticipant_ThrowsUnauthorized()
    {
        var outsider = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", outsider));
    }

    [TestMethod]
    public async Task SendOffer_TargetNotParticipant_ThrowsUnauthorized()
    {
        var outsider = Guid.NewGuid();
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendOfferAsync(_callId, outsider, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_TargetLeftCall_ThrowsUnauthorized()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, false));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller));
    }

    [TestMethod]
    public async Task SendOffer_EmptySdp_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "", _caller));
    }

    [TestMethod]
    public async Task SendOffer_WhitespaceSdp_ThrowsArgument()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "   ", _caller));
    }

    [TestMethod]
    public async Task SendOffer_OversizedSdp_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var oversizedSdp = new string('x', CallSignalingService.MaxSdpPayloadBytes + 1);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, oversizedSdp, _caller));
    }

    [TestMethod]
    public async Task SendOffer_ExactMaxSdp_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var exactSdp = new string('v', CallSignalingService.MaxSdpPayloadBytes);
        await _service.SendOfferAsync(_callId, _target.UserId, exactSdp, _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveCallOffer",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendOffer_NullCaller_ThrowsArgumentNull()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", null!));
    }

    [TestMethod]
    public async Task SendOffer_NoBroadcaster_DoesNotThrow()
    {
        var serviceNoBroadcaster = new CallSignalingService(
            _db, _eventBusMock.Object, NullLogger<CallSignalingService>.Instance);

        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await serviceNoBroadcaster.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller);
        // Should not throw even without broadcaster
    }

    // ── SendAnswerAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task SendAnswer_ValidCall_RelaysToTargetUser()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendAnswerAsync(_callId, _caller.UserId, "v=0\r\nanswer\r\n", _target);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _caller.UserId,
            "ReceiveCallAnswer",
            It.Is<object>(o => o.ToString()!.Contains(_callId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendAnswer_TerminalState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Ended,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendAnswerAsync(_callId, _caller.UserId, "v=0\r\n", _target));
    }

    [TestMethod]
    public async Task SendAnswer_EmptySdp_ThrowsArgument()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendAnswerAsync(_callId, _caller.UserId, "", _target));
    }

    [TestMethod]
    public async Task SendAnswer_OversizedSdp_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var oversized = new string('x', CallSignalingService.MaxSdpPayloadBytes + 1);
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendAnswerAsync(_callId, _caller.UserId, oversized, _target));
    }

    [TestMethod]
    public async Task SendAnswer_CallerNotParticipant_ThrowsUnauthorized()
    {
        var outsider = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendAnswerAsync(_callId, _caller.UserId, "v=0\r\n", outsider));
    }

    [TestMethod]
    public async Task SendAnswer_NullCaller_ThrowsArgumentNull()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.SendAnswerAsync(_callId, _target.UserId, "v=0\r\n", null!));
    }

    // ── SendIceCandidateAsync ────────────────────────────────────

    [TestMethod]
    public async Task SendIceCandidate_ValidCall_RelaysToTargetUser()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var candidate = "{\"candidate\":\"candidate:1 1 UDP 2130706431 192.168.1.1 5060 typ host\"}";
        await _service.SendIceCandidateAsync(_callId, _target.UserId, candidate, _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveIceCandidate",
            It.Is<object>(o => o.ToString()!.Contains(_callId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendIceCandidate_ConnectingState_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Connecting,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendIceCandidateAsync(_callId, _target.UserId, "{\"candidate\":\"a\"}", _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveIceCandidate",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendIceCandidate_EmptyCandidate_ThrowsArgument()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, "", _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_WhitespaceCandidate_ThrowsArgument()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, "  \t  ", _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_OversizedCandidate_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var oversized = new string('x', CallSignalingService.MaxIceCandidateBytes + 1);
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, oversized, _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_ExactMaxSize_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var exact = new string('c', CallSignalingService.MaxIceCandidateBytes);
        await _service.SendIceCandidateAsync(_callId, _target.UserId, exact, _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveIceCandidate",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendIceCandidate_CallNotFound_ThrowsInvalidOperation()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendIceCandidateAsync(Guid.NewGuid(), _target.UserId, "{}", _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_TerminalState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Failed,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, "{}", _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_CallerNotParticipant_ThrowsUnauthorized()
    {
        var outsider = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, "{}", outsider));
    }

    [TestMethod]
    public async Task SendIceCandidate_TargetNotParticipant_ThrowsUnauthorized()
    {
        var outsider = Guid.NewGuid();
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendIceCandidateAsync(_callId, outsider, "{}", _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_NullCaller_ThrowsArgumentNull()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, "{}", null!));
    }

    // ── SendMediaStateChangeAsync ────────────────────────────────

    [TestMethod]
    public async Task SendMediaStateChange_Audio_BroadcastsToCallGroup()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "Audio", false, _caller);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"call-{_callId}",
            "MediaStateChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChange_Video_BroadcastsToCallGroup()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "Video", true, _caller);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"call-{_callId}",
            "MediaStateChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChange_AudioMute_UpdatesParticipantRecord()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "Audio", false, _caller);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == _callId && cp.UserId == _caller.UserId);
        Assert.IsFalse(participant.HasAudio);
    }

    [TestMethod]
    public async Task SendMediaStateChange_VideoToggle_UpdatesParticipantRecord()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "Video", false, _caller);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == _callId && cp.UserId == _caller.UserId);
        Assert.IsFalse(participant.HasVideo);
    }

    [TestMethod]
    public async Task SendMediaStateChange_ScreenShareStarted_PublishesEvent()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "ScreenShare", true, _caller);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.Is<ScreenShareStartedEvent>(ev => ev.CallId == _callId && ev.UserId == _caller.UserId),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChange_ScreenShareEnded_PublishesEvent()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "ScreenShare", false, _caller);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.Is<ScreenShareEndedEvent>(ev => ev.CallId == _callId && ev.UserId == _caller.UserId),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMediaStateChange_ScreenShareUpdatesRecord()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "ScreenShare", true, _caller);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == _callId && cp.UserId == _caller.UserId);
        Assert.IsTrue(participant.HasScreenShare);
    }

    [TestMethod]
    public async Task SendMediaStateChange_AudioUnmute_DoesNotPublishScreenShareEvents()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "Audio", true, _caller);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<ScreenShareStartedEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<ScreenShareEndedEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMediaStateChange_InvalidMediaType_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendMediaStateChangeAsync(_callId, "InvalidType", true, _caller));
    }

    [TestMethod]
    public async Task SendMediaStateChange_EmptyMediaType_ThrowsArgument()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendMediaStateChangeAsync(_callId, "", true, _caller));
    }

    [TestMethod]
    public async Task SendMediaStateChange_WhitespaceMediaType_ThrowsArgument()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendMediaStateChangeAsync(_callId, "  ", true, _caller));
    }

    [TestMethod]
    public async Task SendMediaStateChange_NullCaller_ThrowsArgumentNull()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.SendMediaStateChangeAsync(_callId, "Audio", true, null!));
    }

    [TestMethod]
    public async Task SendMediaStateChange_TerminalState_ThrowsInvalidOperation()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Ended,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SendMediaStateChangeAsync(_callId, "Audio", true, _caller));
    }

    [TestMethod]
    public async Task SendMediaStateChange_NotParticipant_ThrowsUnauthorized()
    {
        var outsider = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => _service.SendMediaStateChangeAsync(_callId, "Audio", true, outsider));
    }

    [TestMethod]
    public async Task SendMediaStateChange_CaseInsensitiveMediaType_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "audio", false, _caller);
        await _service.SendMediaStateChangeAsync(_callId, "VIDEO", true, _caller);
        await _service.SendMediaStateChangeAsync(_callId, "screenShare", true, _caller);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"call-{_callId}",
            "MediaStateChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task SendMediaStateChange_NoBroadcaster_StillUpdatesDbAndPublishesEvents()
    {
        var serviceNoBroadcaster = new CallSignalingService(
            _db, _eventBusMock.Object, NullLogger<CallSignalingService>.Instance);

        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        await serviceNoBroadcaster.SendMediaStateChangeAsync(_callId, "ScreenShare", true, _caller);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == _callId && cp.UserId == _caller.UserId);
        Assert.IsTrue(participant.HasScreenShare);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<ScreenShareStartedEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Call Group Management ────────────────────────────────────

    [TestMethod]
    public async Task AddToCallGroup_WithBroadcaster_AddsUser()
    {
        var userId = Guid.NewGuid();
        var callId = Guid.NewGuid();

        await _service.AddToCallGroupAsync(callId, userId);

        _broadcasterMock.Verify(b => b.AddToGroupAsync(
            userId,
            $"call-{callId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveFromCallGroup_WithBroadcaster_RemovesUser()
    {
        var userId = Guid.NewGuid();
        var callId = Guid.NewGuid();

        await _service.RemoveFromCallGroupAsync(callId, userId);

        _broadcasterMock.Verify(b => b.RemoveFromGroupAsync(
            userId,
            $"call-{callId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddToCallGroup_NoBroadcaster_NoOp()
    {
        var serviceNoBroadcaster = new CallSignalingService(
            _db, _eventBusMock.Object, NullLogger<CallSignalingService>.Instance);

        await serviceNoBroadcaster.AddToCallGroupAsync(Guid.NewGuid(), Guid.NewGuid());
        // Should complete without error
    }

    [TestMethod]
    public async Task RemoveFromCallGroup_NoBroadcaster_NoOp()
    {
        var serviceNoBroadcaster = new CallSignalingService(
            _db, _eventBusMock.Object, NullLogger<CallSignalingService>.Instance);

        await serviceNoBroadcaster.RemoveFromCallGroupAsync(Guid.NewGuid(), Guid.NewGuid());
        // Should complete without error
    }

    [TestMethod]
    public void CallGroup_ReturnsCorrectFormat()
    {
        var callId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var group = CallSignalingService.CallGroup(callId);
        Assert.AreEqual("call-12345678-1234-1234-1234-123456789012", group);
    }

    // ── Multi-Participant Scenarios ──────────────────────────────

    [TestMethod]
    public async Task SendOffer_ThreeParticipants_RelaysToCorrectTarget()
    {
        var user3 = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true), (user3.UserId, true));

        await _service.SendOfferAsync(_callId, user3.UserId, "v=0\r\n", _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            user3.UserId,
            "ReceiveCallOffer",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Ensure other target was NOT sent to
        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveCallOffer",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMediaStateChange_ThreeParticipants_BroadcastsToAll()
    {
        var user3 = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true), (user3.UserId, true));

        await _service.SendMediaStateChangeAsync(_callId, "Audio", false, _caller);

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            $"call-{_callId}",
            "MediaStateChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── OnHold State ─────────────────────────────────────────────

    [TestMethod]
    public async Task SendOffer_OnHoldState_Succeeds()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.OnHold,
            (_caller.UserId, true), (_target.UserId, true));

        await _service.SendOfferAsync(_callId, _target.UserId, "v=0\r\n", _caller);

        _broadcasterMock.Verify(b => b.SendToUserAsync(
            _target.UserId,
            "ReceiveCallOffer",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Size Limit Constants ─────────────────────────────────────

    [TestMethod]
    public void MaxSdpPayloadBytes_Is64KB()
    {
        int actual = CallSignalingService.MaxSdpPayloadBytes;
        Assert.AreEqual(64 * 1024, actual);
    }

    [TestMethod]
    public void MaxIceCandidateBytes_Is4KB()
    {
        int actual = CallSignalingService.MaxIceCandidateBytes;
        Assert.AreEqual(4 * 1024, actual);
    }

    // ── Unicode Payload Size Validation ──────────────────────────

    [TestMethod]
    public async Task SendOffer_MultibyteSdpExceedsLimit_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        // Each emoji is 4 bytes in UTF-8, so fewer chars exceed the byte limit
        var emojiCount = (CallSignalingService.MaxSdpPayloadBytes / 4) + 1;
        var multibyteSdp = string.Concat(Enumerable.Repeat("\U0001F3A5", emojiCount));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendOfferAsync(_callId, _target.UserId, multibyteSdp, _caller));
    }

    [TestMethod]
    public async Task SendIceCandidate_MultibyteCandidateExceedsLimit_ThrowsArgument()
    {
        SeedActiveCall(_callId, _channelId, VideoCallState.Active,
            (_caller.UserId, true), (_target.UserId, true));

        var emojiCount = (CallSignalingService.MaxIceCandidateBytes / 4) + 1;
        var multibyteCandidate = string.Concat(Enumerable.Repeat("\U0001F3A5", emojiCount));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendIceCandidateAsync(_callId, _target.UserId, multibyteCandidate, _caller));
    }
}
