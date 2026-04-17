using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages video call lifecycle: initiation, joining, leaving, ending, rejection, and history queries.
/// </summary>
internal sealed class VideoCallService : IVideoCallService
{
    /// <summary>
    /// Duration in seconds after which a ringing call is considered missed.
    /// </summary>
    internal const int RingTimeoutSeconds = 30;

    private readonly ChatDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IChatRealtimeService? _realtimeService;
    private readonly IChatMessageNotifier? _messageNotifier;
    private readonly ILiveKitService _liveKitService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<VideoCallService> _logger;

    public VideoCallService(
        ChatDbContext db,
        IEventBus eventBus,
        ILogger<VideoCallService> logger,
        ILiveKitService liveKitService,
        IChatRealtimeService? realtimeService = null,
        IChatMessageNotifier? messageNotifier = null,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _realtimeService = realtimeService;
        _messageNotifier = messageNotifier;
        _liveKitService = liveKitService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VideoCallDto> InitiateCallAsync(
        Guid channelId,
        StartCallRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!Enum.TryParse<CallMediaType>(request.MediaType, ignoreCase: true, out var mediaType))
        {
            throw new ArgumentException($"Invalid media type: {request.MediaType}", nameof(request));
        }

        // Ensure no active call already exists in this channel
        var existingActiveCall = await _db.VideoCalls
            .AnyAsync(vc => vc.ChannelId == channelId
                && (vc.State == VideoCallState.Ringing
                    || vc.State == VideoCallState.Connecting
                    || vc.State == VideoCallState.Active),
                cancellationToken);

        if (existingActiveCall)
        {
            throw new InvalidOperationException("An active call already exists in this channel.");
        }

        // Determine if this is a group call (more than 2 members in channel)
        var memberCount = await _db.ChannelMembers
            .CountAsync(cm => cm.ChannelId == channelId, cancellationToken);

        var isGroupCall = memberCount > 2;

        var call = new VideoCall
        {
            ChannelId = channelId,
            InitiatorUserId = caller.UserId,
            State = VideoCallState.Ringing,
            MediaType = mediaType,
            IsGroupCall = isGroupCall,
            MaxParticipants = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        var initiatorParticipant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = caller.UserId,
            Role = CallParticipantRole.Initiator,
            JoinedAtUtc = DateTime.UtcNow,
            HasAudio = true,
            HasVideo = mediaType == CallMediaType.Video
        };

        _db.VideoCalls.Add(call);
        _db.CallParticipants.Add(initiatorParticipant);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new VideoCallInitiatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = call.Id,
            ChannelId = channelId,
            InitiatorUserId = caller.UserId,
            MediaType = mediaType.ToString(),
            IsGroupCall = isGroupCall
        }, caller, cancellationToken);

        // Broadcast ringing notification to channel members
        if (_realtimeService is not null)
        {
            await _realtimeService.BroadcastChannelUpdatedAsync(
                new ChannelDto
                {
                    Id = channelId,
                    Name = string.Empty,
                    Type = string.Empty,
                    CreatedByUserId = Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                    MemberCount = memberCount
                },
                cancellationToken);
        }

        // Notify in-process Blazor circuits so members in this channel can render the incoming call UI.
        _messageNotifier?.NotifyCallRinging(new CallRingingNotification(
            CallId: call.Id,
            ChannelId: channelId,
            InitiatorUserId: caller.UserId,
            MediaType: mediaType.ToString()));

        _logger.LogInformation(
            "Video call {CallId} initiated in channel {ChannelId} by user {UserId} (MediaType={MediaType}, IsGroup={IsGroup})",
            call.Id, channelId, caller.UserId, mediaType, isGroupCall);

        return ToVideoCallDto(call, [initiatorParticipant]);
    }

    /// <inheritdoc />
    public async Task<VideoCallDto> JoinCallAsync(
        Guid callId,
        JoinCallRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var call = await _db.VideoCalls
            .FirstOrDefaultAsync(vc => vc.Id == callId, cancellationToken)
            ?? throw new InvalidOperationException($"Video call {callId} not found.");

        // Allow joining when Ringing or Active (for group calls, more people can join mid-call)
        if (call.State != VideoCallState.Ringing
            && call.State != VideoCallState.Connecting
            && call.State != VideoCallState.Active)
        {
            throw new InvalidOperationException(
                $"Cannot join call in state {call.State}. Call must be Ringing, Connecting, or Active.");
        }

        // Prevent duplicate participants
        var alreadyJoined = await _db.CallParticipants
            .AnyAsync(cp => cp.VideoCallId == callId
                && cp.UserId == caller.UserId
                && cp.LeftAtUtc == null,
                cancellationToken);

        if (alreadyJoined)
        {
            throw new InvalidOperationException("User is already in this call.");
        }

        // Auto-escalation: check if adding this participant exceeds P2P limit
        var currentActiveCount = await _db.CallParticipants
            .CountAsync(cp => cp.VideoCallId == callId && cp.LeftAtUtc == null, cancellationToken);

        var newParticipantCount = currentActiveCount + 1;

        if (newParticipantCount > _liveKitService.MaxP2PParticipants && call.LiveKitRoomId is null)
        {
            if (!_liveKitService.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"Cannot add more than {_liveKitService.MaxP2PParticipants} participants without LiveKit. " +
                    "LiveKit SFU is not configured. Contact your administrator to enable LiveKit for larger calls.");
            }

            // Escalate to LiveKit SFU
            var roomName = await _liveKitService.CreateRoomAsync(callId, _liveKitService.MaxP2PParticipants * 4, cancellationToken);
            call.LiveKitRoomId = roomName;

            _logger.LogInformation(
                "Auto-escalated call {CallId} to LiveKit room {RoomName} (participant count {Count} exceeds P2P limit {Limit})",
                callId, roomName, newParticipantCount, _liveKitService.MaxP2PParticipants);
        }

        var participant = new CallParticipant
        {
            VideoCallId = callId,
            UserId = caller.UserId,
            Role = CallParticipantRole.Participant,
            JoinedAtUtc = DateTime.UtcNow,
            HasAudio = request.WithAudio,
            HasVideo = request.WithVideo
        };

        _db.CallParticipants.Add(participant);

        // Transition Ringing → Connecting on first answer (non-initiator)
        var isFirstAnswer = call.State == VideoCallState.Ringing;
        if (isFirstAnswer)
        {
            CallStateValidator.ValidateTransition(call.State, VideoCallState.Connecting);
            call.State = VideoCallState.Connecting;
            call.StartedAtUtc = DateTime.UtcNow;

            // Immediately transition Connecting → Active
            CallStateValidator.ValidateTransition(call.State, VideoCallState.Active);
            call.State = VideoCallState.Active;
        }

        // Update max participants
        var activeParticipantCount = await _db.CallParticipants
            .CountAsync(cp => cp.VideoCallId == callId && cp.LeftAtUtc == null, cancellationToken) + 1; // +1 for new participant not yet saved
        if (activeParticipantCount > call.MaxParticipants)
        {
            call.MaxParticipants = activeParticipantCount;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (isFirstAnswer)
        {
            await _eventBus.PublishAsync(new VideoCallAnsweredEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CallId = callId,
                ChannelId = call.ChannelId,
                AnsweredByUserId = caller.UserId
            }, caller, cancellationToken);

            _messageNotifier?.NotifyCallAccepted(new CallAcceptedNotification(
                callId, call.ChannelId, caller.UserId, caller.UserId.ToString()));
        }

        await _eventBus.PublishAsync(new ParticipantJoinedCallEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = callId,
            ChannelId = call.ChannelId,
            UserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation(
            "User {UserId} joined video call {CallId} (FirstAnswer={IsFirstAnswer})",
            caller.UserId, callId, isFirstAnswer);

        var participants = await _db.CallParticipants
            .Where(cp => cp.VideoCallId == callId)
            .ToListAsync(cancellationToken);

        return ToVideoCallDto(call, participants);
    }

    /// <inheritdoc />
    public async Task LeaveCallAsync(
        Guid callId,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var call = await _db.VideoCalls
            .FirstOrDefaultAsync(vc => vc.Id == callId, cancellationToken)
            ?? throw new InvalidOperationException($"Video call {callId} not found.");

        var participant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == callId
                && cp.UserId == caller.UserId
                && cp.LeftAtUtc == null,
                cancellationToken)
            ?? throw new InvalidOperationException("User is not an active participant in this call.");

        participant.LeftAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ParticipantLeftCallEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = callId,
            ChannelId = call.ChannelId,
            UserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("User {UserId} left video call {CallId}", caller.UserId, callId);

        // Auto-end if no active participants remain
        var remainingActive = await _db.CallParticipants
            .CountAsync(cp => cp.VideoCallId == callId && cp.LeftAtUtc == null, cancellationToken);

        if (remainingActive == 0 && !CallStateValidator.IsTerminalState(call.State))
        {
            await EndCallInternalAsync(call, VideoCallEndReason.Normal, caller, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task EndCallAsync(
        Guid callId,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var call = await _db.VideoCalls
            .FirstOrDefaultAsync(vc => vc.Id == callId, cancellationToken)
            ?? throw new InvalidOperationException($"Video call {callId} not found.");

        if (CallStateValidator.IsTerminalState(call.State))
        {
            throw new InvalidOperationException($"Call is already in terminal state {call.State}.");
        }

        // Mark all active participants as left
        var activeParticipants = await _db.CallParticipants
            .Where(cp => cp.VideoCallId == callId && cp.LeftAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var p in activeParticipants)
        {
            p.LeftAtUtc = now;
        }

        var endReason = call.State == VideoCallState.Ringing
            ? VideoCallEndReason.Cancelled
            : VideoCallEndReason.Normal;

        await EndCallInternalAsync(call, endReason, caller, cancellationToken);

        _logger.LogInformation(
            "Video call {CallId} ended by user {UserId} (Reason={EndReason})",
            callId, caller.UserId, endReason);
    }

    /// <inheritdoc />
    public async Task RejectCallAsync(
        Guid callId,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var call = await _db.VideoCalls
            .FirstOrDefaultAsync(vc => vc.Id == callId, cancellationToken)
            ?? throw new InvalidOperationException($"Video call {callId} not found.");

        if (call.State != VideoCallState.Ringing)
        {
            throw new InvalidOperationException(
                $"Can only reject a call that is Ringing. Current state: {call.State}.");
        }

        // For 1:1 calls, rejection ends the call
        if (!call.IsGroupCall)
        {
            CallStateValidator.ValidateTransition(call.State, VideoCallState.Rejected);
            call.State = VideoCallState.Rejected;
            call.EndReason = VideoCallEndReason.Rejected;
            call.EndedAtUtc = DateTime.UtcNow;

            // Mark all participants as left
            var participants = await _db.CallParticipants
                .Where(cp => cp.VideoCallId == callId && cp.LeftAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var p in participants)
            {
                p.LeftAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            await _eventBus.PublishAsync(new VideoCallEndedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CallId = callId,
                ChannelId = call.ChannelId,
                EndReason = VideoCallEndReason.Rejected.ToString(),
                DurationSeconds = null
            }, caller, cancellationToken);

            // Notify in-process Blazor circuits so the caller sees rejection immediately
            _messageNotifier?.NotifyCallEnded(new CallEndedNotification(
                callId, call.ChannelId, VideoCallEndReason.Rejected.ToString(), null));

            _logger.LogInformation(
                "Video call {CallId} rejected by user {UserId} (1:1 call ended)",
                callId, caller.UserId);
        }
        else
        {
            // For group calls, rejection just means this user won't join — no state change
            _logger.LogInformation(
                "User {UserId} rejected group video call {CallId} (call continues for others)",
                caller.UserId, callId);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CallHistoryDto>> GetCallHistoryAsync(
        Guid channelId,
        int skip,
        int take,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (skip < 0) skip = 0;
        if (take <= 0) take = 20;
        if (take > 100) take = 100;

        var calls = await _db.VideoCalls
            .AsNoTracking()
            .Where(vc => vc.ChannelId == channelId)
            .OrderByDescending(vc => vc.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var callIds = calls.Select(c => c.Id).ToList();
        var participantCounts = await _db.CallParticipants
            .AsNoTracking()
            .Where(cp => callIds.Contains(cp.VideoCallId))
            .GroupBy(cp => cp.VideoCallId)
            .Select(g => new { VideoCallId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VideoCallId, x => x.Count, cancellationToken);

        // Resolve initiator display names
        var initiatorIds = calls.Select(c => c.InitiatorUserId).Distinct();
        var displayNames = _userDirectory is not null
            ? await _userDirectory.GetDisplayNamesAsync(initiatorIds, cancellationToken)
            : new Dictionary<Guid, string>();

        return calls.Select(call => new CallHistoryDto
        {
            Id = call.Id,
            ChannelId = call.ChannelId,
            InitiatorUserId = call.InitiatorUserId,
            InitiatorDisplayName = displayNames.GetValueOrDefault(call.InitiatorUserId),
            State = call.State.ToString(),
            MediaType = call.MediaType.ToString(),
            EndReason = call.EndReason?.ToString(),
            DurationSeconds = CalculateDurationSeconds(call),
            ParticipantCount = participantCounts.GetValueOrDefault(call.Id, 0),
            CreatedAtUtc = call.CreatedAtUtc
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<VideoCallDto?> GetActiveCallAsync(
        Guid channelId,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var call = await _db.VideoCalls
            .AsNoTracking()
            .FirstOrDefaultAsync(vc => vc.ChannelId == channelId
                && (vc.State == VideoCallState.Ringing
                    || vc.State == VideoCallState.Connecting
                    || vc.State == VideoCallState.Active),
                cancellationToken);

        if (call is null)
        {
            return null;
        }

        var participants = await _db.CallParticipants
            .AsNoTracking()
            .Where(cp => cp.VideoCallId == call.Id)
            .ToListAsync(cancellationToken);

        var initiatorName = await ResolveDisplayNameAsync(call.InitiatorUserId, cancellationToken);
        return ToVideoCallDto(call, participants, initiatorName);
    }

    /// <inheritdoc />
    public async Task<VideoCallDto?> GetCallByIdAsync(
        Guid callId,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var call = await _db.VideoCalls
            .AsNoTracking()
            .FirstOrDefaultAsync(vc => vc.Id == callId, cancellationToken);

        if (call is null)
        {
            return null;
        }

        var participants = await _db.CallParticipants
            .AsNoTracking()
            .Where(cp => cp.VideoCallId == call.Id)
            .ToListAsync(cancellationToken);

        var initiatorName = await ResolveDisplayNameAsync(call.InitiatorUserId, cancellationToken);
        return ToVideoCallDto(call, participants, initiatorName);
    }

    /// <summary>
    /// Handles ring timeout: transitions ringing calls older than <see cref="RingTimeoutSeconds"/> to Missed.
    /// Called by a background timer or hosted service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of calls transitioned to Missed.</returns>
    internal async Task<int> HandleRingTimeoutsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-RingTimeoutSeconds);

        var timedOutCalls = await _db.VideoCalls
            .Where(vc => vc.State == VideoCallState.Ringing && vc.CreatedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);

        if (timedOutCalls.Count == 0)
        {
            return 0;
        }

        var systemCaller = CallerContext.CreateSystemContext();

        foreach (var call in timedOutCalls)
        {
            CallStateValidator.ValidateTransition(call.State, VideoCallState.Missed);
            call.State = VideoCallState.Missed;
            call.EndReason = VideoCallEndReason.Missed;
            call.EndedAtUtc = DateTime.UtcNow;

            // Mark all participants as left
            var activeParticipants = await _db.CallParticipants
                .Where(cp => cp.VideoCallId == call.Id && cp.LeftAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var p in activeParticipants)
            {
                p.LeftAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var call in timedOutCalls)
        {
            await _eventBus.PublishAsync(new VideoCallMissedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CallId = call.Id,
                ChannelId = call.ChannelId,
                InitiatorUserId = call.InitiatorUserId
            }, systemCaller, cancellationToken);

            _logger.LogInformation(
                "Video call {CallId} in channel {ChannelId} timed out (missed after {TimeoutSeconds}s)",
                call.Id, call.ChannelId, RingTimeoutSeconds);

            // Notify in-process Blazor circuits so ringing UI is dismissed
            _messageNotifier?.NotifyCallEnded(new CallEndedNotification(
                call.Id, call.ChannelId, VideoCallEndReason.Missed.ToString(), null));
        }

        return timedOutCalls.Count;
    }

    private async Task EndCallInternalAsync(
        VideoCall call,
        VideoCallEndReason endReason,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        var targetState = endReason == VideoCallEndReason.Missed
            ? VideoCallState.Missed
            : VideoCallState.Ended;

        CallStateValidator.ValidateTransition(call.State, targetState);

        call.State = targetState;
        call.EndReason = endReason;
        call.EndedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Clean up LiveKit room if one was created
        if (call.LiveKitRoomId is not null)
        {
            try
            {
                await _liveKitService.DeleteRoomAsync(call.LiveKitRoomId, cancellationToken);
                _logger.LogInformation("Cleaned up LiveKit room {RoomName} for ended call {CallId}", call.LiveKitRoomId, call.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up LiveKit room {RoomName} for call {CallId}", call.LiveKitRoomId, call.Id);
            }
        }

        await _eventBus.PublishAsync(new VideoCallEndedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = call.Id,
            ChannelId = call.ChannelId,
            EndReason = endReason.ToString(),
            DurationSeconds = CalculateDurationSeconds(call)
        }, caller, cancellationToken);

        // Notify in-process Blazor circuits so all participants see the call end immediately
        _messageNotifier?.NotifyCallEnded(new CallEndedNotification(
            call.Id, call.ChannelId, endReason.ToString(), CalculateDurationSeconds(call)));
    }

    private static int? CalculateDurationSeconds(VideoCall call)
    {
        if (call.StartedAtUtc is null || call.EndedAtUtc is null)
        {
            return null;
        }

        return (int)(call.EndedAtUtc.Value - call.StartedAtUtc.Value).TotalSeconds;
    }

    private async Task<string?> ResolveDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_userDirectory is null)
        {
            return null;
        }

        var names = await _userDirectory.GetDisplayNamesAsync([userId], cancellationToken);
        return names.GetValueOrDefault(userId);
    }

    private static VideoCallDto ToVideoCallDto(VideoCall call, IEnumerable<CallParticipant> participants, string? initiatorDisplayName = null)
    {
        return new VideoCallDto
        {
            Id = call.Id,
            ChannelId = call.ChannelId,
            InitiatorUserId = call.InitiatorUserId,
            InitiatorDisplayName = initiatorDisplayName,
            State = call.State.ToString(),
            MediaType = call.MediaType.ToString(),
            IsGroupCall = call.IsGroupCall,
            StartedAtUtc = call.StartedAtUtc,
            EndedAtUtc = call.EndedAtUtc,
            EndReason = call.EndReason?.ToString(),
            MaxParticipants = call.MaxParticipants,
            CreatedAtUtc = call.CreatedAtUtc,
            Participants = participants.Select(p => new CallParticipantDto
            {
                Id = p.Id,
                UserId = p.UserId,
                Role = p.Role.ToString(),
                JoinedAtUtc = p.JoinedAtUtc,
                LeftAtUtc = p.LeftAtUtc,
                HasAudio = p.HasAudio,
                HasVideo = p.HasVideo,
                HasScreenShare = p.HasScreenShare
            }).ToList()
        };
    }
}
