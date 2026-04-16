using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Server-side signaling coordinator for WebRTC call sessions.
/// Validates call state and participant membership before relaying SDP/ICE payloads
/// between peers via <see cref="IRealtimeBroadcaster"/>.
/// </summary>
internal sealed class CallSignalingService : ICallSignalingService
{
    /// <summary>Maximum allowed size of an SDP payload in bytes (64 KB).</summary>
    internal const int MaxSdpPayloadBytes = 64 * 1024;

    /// <summary>Maximum allowed size of an ICE candidate payload in bytes (4 KB).</summary>
    internal const int MaxIceCandidateBytes = 4 * 1024;

    private readonly ChatDbContext _db;
    private readonly IRealtimeBroadcaster? _broadcaster;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CallSignalingService> _logger;

    public CallSignalingService(
        ChatDbContext db,
        IEventBus eventBus,
        ILogger<CallSignalingService> logger,
        IRealtimeBroadcaster? broadcaster = null)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    /// <inheritdoc />
    public async Task SendOfferAsync(
        Guid callId,
        Guid targetUserId,
        string sdpOffer,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ValidateSdpPayload(sdpOffer, nameof(sdpOffer));

        var call = await GetActiveCallOrThrowAsync(callId, cancellationToken);
        await EnsureParticipantAsync(callId, caller.UserId, cancellationToken);
        await EnsureParticipantAsync(callId, targetUserId, cancellationToken);

        _logger.LogDebug(
            "Relaying SDP offer from {FromUserId} to {ToUserId} for call {CallId}",
            caller.UserId, targetUserId, callId);

        if (_broadcaster is not null)
        {
            await _broadcaster.SendToUserAsync(
                targetUserId,
                "ReceiveCallOffer",
                new
                {
                    CallId = callId,
                    FromUserId = caller.UserId,
                    SdpOffer = sdpOffer
                },
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SendAnswerAsync(
        Guid callId,
        Guid targetUserId,
        string sdpAnswer,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ValidateSdpPayload(sdpAnswer, nameof(sdpAnswer));

        var call = await GetActiveCallOrThrowAsync(callId, cancellationToken);
        await EnsureParticipantAsync(callId, caller.UserId, cancellationToken);
        await EnsureParticipantAsync(callId, targetUserId, cancellationToken);

        _logger.LogDebug(
            "Relaying SDP answer from {FromUserId} to {ToUserId} for call {CallId}",
            caller.UserId, targetUserId, callId);

        if (_broadcaster is not null)
        {
            await _broadcaster.SendToUserAsync(
                targetUserId,
                "ReceiveCallAnswer",
                new
                {
                    CallId = callId,
                    FromUserId = caller.UserId,
                    SdpAnswer = sdpAnswer
                },
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SendIceCandidateAsync(
        Guid callId,
        Guid targetUserId,
        string iceCandidate,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ValidateIceCandidate(iceCandidate);

        var call = await GetActiveCallOrThrowAsync(callId, cancellationToken);
        await EnsureParticipantAsync(callId, caller.UserId, cancellationToken);
        await EnsureParticipantAsync(callId, targetUserId, cancellationToken);

        _logger.LogDebug(
            "Relaying ICE candidate from {FromUserId} to {ToUserId} for call {CallId}",
            caller.UserId, targetUserId, callId);

        if (_broadcaster is not null)
        {
            await _broadcaster.SendToUserAsync(
                targetUserId,
                "ReceiveIceCandidate",
                new
                {
                    CallId = callId,
                    FromUserId = caller.UserId,
                    IceCandidate = iceCandidate
                },
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SendMediaStateChangeAsync(
        Guid callId,
        string mediaType,
        bool enabled,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("Media type cannot be empty.", nameof(mediaType));
        }

        if (!Enum.TryParse<CallMediaType>(mediaType, ignoreCase: true, out var parsedMediaType))
        {
            throw new ArgumentException($"Invalid media type: {mediaType}", nameof(mediaType));
        }

        var call = await GetActiveCallOrThrowAsync(callId, cancellationToken);
        await EnsureParticipantAsync(callId, caller.UserId, cancellationToken);

        // Update participant's media state in the database
        var participant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == callId
                && cp.UserId == caller.UserId
                && cp.LeftAtUtc == null,
                cancellationToken);

        if (participant is not null)
        {
            switch (parsedMediaType)
            {
                case CallMediaType.Audio:
                    participant.HasAudio = enabled;
                    break;
                case CallMediaType.Video:
                    participant.HasVideo = enabled;
                    break;
                case CallMediaType.ScreenShare:
                    participant.HasScreenShare = enabled;
                    break;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug(
            "User {UserId} changed {MediaType} to {Enabled} in call {CallId}",
            caller.UserId, mediaType, enabled, callId);

        // Broadcast to all participants in the call group
        if (_broadcaster is not null)
        {
            var callGroup = CallGroup(callId);
            await _broadcaster.BroadcastAsync(
                callGroup,
                "MediaStateChanged",
                new
                {
                    CallId = callId,
                    UserId = caller.UserId,
                    MediaType = mediaType,
                    Enabled = enabled
                },
                cancellationToken);
        }

        // Publish screen share events if applicable
        if (parsedMediaType == CallMediaType.ScreenShare)
        {
            if (enabled)
            {
                await _eventBus.PublishAsync(new ScreenShareStartedEvent
                {
                    EventId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    CallId = callId,
                    ChannelId = call.ChannelId,
                    UserId = caller.UserId
                }, caller, cancellationToken);
            }
            else
            {
                await _eventBus.PublishAsync(new ScreenShareEndedEvent
                {
                    EventId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    CallId = callId,
                    ChannelId = call.ChannelId,
                    UserId = caller.UserId
                }, caller, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Adds a user to the call-scoped SignalR group.
    /// Called when a participant joins a call.
    /// </summary>
    internal async Task AddToCallGroupAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (_broadcaster is null) return;
        await _broadcaster.AddToGroupAsync(userId, CallGroup(callId), cancellationToken);
    }

    /// <summary>
    /// Removes a user from the call-scoped SignalR group.
    /// Called when a participant leaves or the call ends.
    /// </summary>
    internal async Task RemoveFromCallGroupAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (_broadcaster is null) return;
        await _broadcaster.RemoveFromGroupAsync(userId, CallGroup(callId), cancellationToken);
    }

    /// <summary>
    /// Returns the SignalR group name for a given call.
    /// </summary>
    internal static string CallGroup(Guid callId) => $"call-{callId}";

    private async Task<VideoCall> GetActiveCallOrThrowAsync(Guid callId, CancellationToken cancellationToken)
    {
        var call = await _db.VideoCalls
            .AsNoTracking()
            .FirstOrDefaultAsync(vc => vc.Id == callId, cancellationToken)
            ?? throw new InvalidOperationException($"Video call {callId} not found.");

        if (CallStateValidator.IsTerminalState(call.State))
        {
            throw new InvalidOperationException(
                $"Cannot signal on call {callId} in terminal state {call.State}.");
        }

        return call;
    }

    private async Task EnsureParticipantAsync(Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        var isParticipant = await _db.CallParticipants
            .AnyAsync(cp => cp.VideoCallId == callId
                && cp.UserId == userId
                && cp.LeftAtUtc == null,
                cancellationToken);

        if (!isParticipant)
        {
            throw new UnauthorizedAccessException(
                $"User {userId} is not an active participant of call {callId}.");
        }
    }

    private static void ValidateSdpPayload(string payload, string paramName)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("SDP payload cannot be empty.", paramName);
        }

        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaxSdpPayloadBytes)
        {
            throw new ArgumentException(
                $"SDP payload exceeds maximum allowed size of {MaxSdpPayloadBytes} bytes.", paramName);
        }
    }

    private static void ValidateIceCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("ICE candidate cannot be empty.", nameof(candidate));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(candidate) > MaxIceCandidateBytes)
        {
            throw new ArgumentException(
                $"ICE candidate exceeds maximum allowed size of {MaxIceCandidateBytes} bytes.", nameof(candidate));
        }
    }
}
