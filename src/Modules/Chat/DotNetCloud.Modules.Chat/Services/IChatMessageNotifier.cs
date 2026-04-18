using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Payload for an incoming call notification raised via <see cref="IChatMessageNotifier"/>.
/// </summary>
public sealed record CallRingingNotification(
    Guid CallId,
    Guid ChannelId,
    Guid InitiatorUserId,
    string MediaType);

/// <summary>
/// Payload for a call-accepted notification raised via <see cref="IChatMessageNotifier"/>.
/// </summary>
public sealed record CallAcceptedNotification(
    Guid CallId,
    Guid ChannelId,
    Guid AcceptedByUserId,
    string AcceptedByDisplayName);

/// <summary>
/// Payload for a call-ended notification raised via <see cref="IChatMessageNotifier"/>.
/// </summary>
public sealed record CallEndedNotification(
    Guid CallId,
    Guid ChannelId,
    string EndReason,
    int? DurationSeconds);

/// <summary>
/// Payload for a mid-call invite notification raised via <see cref="IChatMessageNotifier"/>.
/// Notifies the target user that they have been invited to join an ongoing call.
/// </summary>
public sealed record CallInviteReceivedNotification(
    Guid CallId,
    Guid ChannelId,
    Guid InvitedByUserId,
    string? InvitedByDisplayName,
    string MediaType,
    bool IsMidCallInvite,
    int ParticipantCount);

/// <summary>
/// Payload for a host transfer notification raised via <see cref="IChatMessageNotifier"/>.
/// Notifies all call participants that the host role has changed.
/// </summary>
public sealed record CallHostTransferredNotification(
    Guid CallId,
    Guid ChannelId,
    Guid PreviousHostUserId,
    Guid NewHostUserId);

/// <summary>
/// Payload for a media state change notification raised via <see cref="IChatMessageNotifier"/>.
/// Notifies all call participants that a peer's media state (audio, video, screen share) changed.
/// </summary>
public sealed record MediaStateChangedNotification(
    Guid CallId,
    Guid UserId,
    string MediaType,
    bool Enabled);

/// <summary>
/// Payload for a participant-left notification raised via <see cref="IChatMessageNotifier"/>.
/// Notifies remaining call participants that a peer has left the call.
/// </summary>
public sealed record CallParticipantLeftNotification(
    Guid CallId,
    Guid ChannelId,
    Guid UserId);

/// <summary>
/// Payload for a user presence change notification raised via <see cref="IChatMessageNotifier"/>.
/// </summary>
public sealed record UserPresenceChangedNotification(
    Guid UserId,
    bool IsOnline);

/// <summary>
/// Payload for a WebRTC signaling message (offer, answer, or ICE candidate) relayed in-process.
/// </summary>
public sealed record CallSignalNotification(
    Guid CallId,
    Guid FromUserId,
    Guid ToUserId,
    string Type,
    string Payload);

/// <summary>
/// In-process pub/sub for chat message events within the server process.
/// Enables Blazor Server components to receive real-time message updates
/// from any source (REST API, other Blazor circuits, SignalR hub).
/// </summary>
public interface IChatMessageNotifier
{
    /// <summary>Raised when a new message is sent to a channel.</summary>
    event Action<Guid, MessageDto>? MessageReceived;

    /// <summary>Raised when a message is edited.</summary>
    event Action<Guid, MessageDto>? MessageEdited;

    /// <summary>Raised when a message is deleted.</summary>
    event Action<Guid, Guid>? MessageDeleted;

    /// <summary>Raised when a call starts ringing in a channel.</summary>
    event Action<CallRingingNotification>? CallRinging;

    /// <summary>Raised when a call is accepted by a participant.</summary>
    event Action<CallAcceptedNotification>? CallAccepted;

    /// <summary>Raised when a call ends (any reason: hangup, timeout, rejection, failure).</summary>
    event Action<CallEndedNotification>? CallEnded;

    /// <summary>Raised when a WebRTC signaling message is received (offer, answer, ICE candidate).</summary>
    event Action<CallSignalNotification>? CallSignalReceived;

    /// <summary>Raised when a user is invited to join an ongoing call.</summary>
    event Action<CallInviteReceivedNotification>? CallInviteReceived;

    /// <summary>Raised when the host of a call is transferred to another participant.</summary>
    event Action<CallHostTransferredNotification>? CallHostTransferred;

    /// <summary>Raised when a user comes online or goes offline.</summary>
    event Action<UserPresenceChangedNotification>? UserPresenceChanged;

    /// <summary>Raised when a call participant's media state changes (audio, video, screen share).</summary>
    event Action<MediaStateChangedNotification>? MediaStateChanged;

    /// <summary>Raised when a participant leaves an active call.</summary>
    event Action<CallParticipantLeftNotification>? CallParticipantLeft;

    /// <summary>Notifies all subscribers that a new message was sent.</summary>
    void NotifyMessageReceived(Guid channelId, MessageDto message);

    /// <summary>Notifies all subscribers that a message was edited.</summary>
    void NotifyMessageEdited(Guid channelId, MessageDto message);

    /// <summary>Notifies all subscribers that a message was deleted.</summary>
    void NotifyMessageDeleted(Guid channelId, Guid messageId);

    /// <summary>Notifies all subscribers that a call is ringing in a channel.</summary>
    void NotifyCallRinging(CallRingingNotification notification);

    /// <summary>Notifies all subscribers that a call was accepted.</summary>
    void NotifyCallAccepted(CallAcceptedNotification notification);

    /// <summary>Notifies all subscribers that a call has ended.</summary>
    void NotifyCallEnded(CallEndedNotification notification);

    /// <summary>Notifies all subscribers of a WebRTC signaling message.</summary>
    void NotifyCallSignal(CallSignalNotification notification);

    /// <summary>Notifies all subscribers that a user has been invited to join an ongoing call.</summary>
    void NotifyCallInviteReceived(CallInviteReceivedNotification notification);

    /// <summary>Notifies all subscribers that the host of a call has been transferred.</summary>
    void NotifyCallHostTransferred(CallHostTransferredNotification notification);

    /// <summary>Notifies all subscribers that a user's presence has changed.</summary>
    void NotifyUserPresenceChanged(UserPresenceChangedNotification notification);

    /// <summary>Notifies all subscribers that a participant's media state changed.</summary>
    void NotifyMediaStateChanged(MediaStateChangedNotification notification);

    /// <summary>Notifies all subscribers that a participant has left a call.</summary>
    void NotifyCallParticipantLeft(CallParticipantLeftNotification notification);
}

/// <summary>
/// In-memory implementation of <see cref="IChatMessageNotifier"/>.
/// Thread-safe via delegate combination semantics.
/// </summary>
public sealed class InProcessChatMessageNotifier : IChatMessageNotifier
{
    /// <inheritdoc />
    public event Action<Guid, MessageDto>? MessageReceived;

    /// <inheritdoc />
    public event Action<Guid, MessageDto>? MessageEdited;

    /// <inheritdoc />
    public event Action<Guid, Guid>? MessageDeleted;

    /// <inheritdoc />
    public event Action<CallRingingNotification>? CallRinging;

    /// <inheritdoc />
    public event Action<CallAcceptedNotification>? CallAccepted;

    /// <inheritdoc />
    public event Action<CallEndedNotification>? CallEnded;

    /// <inheritdoc />
    public event Action<CallSignalNotification>? CallSignalReceived;

    /// <inheritdoc />
    public event Action<CallInviteReceivedNotification>? CallInviteReceived;

    /// <inheritdoc />
    public event Action<CallHostTransferredNotification>? CallHostTransferred;

    /// <inheritdoc />
    public event Action<UserPresenceChangedNotification>? UserPresenceChanged;

    /// <inheritdoc />
    public void NotifyMessageReceived(Guid channelId, MessageDto message)
        => MessageReceived?.Invoke(channelId, message);

    /// <inheritdoc />
    public void NotifyMessageEdited(Guid channelId, MessageDto message)
        => MessageEdited?.Invoke(channelId, message);

    /// <inheritdoc />
    public void NotifyMessageDeleted(Guid channelId, Guid messageId)
        => MessageDeleted?.Invoke(channelId, messageId);

    /// <inheritdoc />
    public void NotifyCallRinging(CallRingingNotification notification)
        => CallRinging?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyCallAccepted(CallAcceptedNotification notification)
        => CallAccepted?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyCallEnded(CallEndedNotification notification)
        => CallEnded?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyCallSignal(CallSignalNotification notification)
        => CallSignalReceived?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyCallInviteReceived(CallInviteReceivedNotification notification)
        => CallInviteReceived?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyCallHostTransferred(CallHostTransferredNotification notification)
        => CallHostTransferred?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyUserPresenceChanged(UserPresenceChangedNotification notification)
        => UserPresenceChanged?.Invoke(notification);

    /// <inheritdoc />
    public event Action<MediaStateChangedNotification>? MediaStateChanged;

    /// <inheritdoc />
    public event Action<CallParticipantLeftNotification>? CallParticipantLeft;

    /// <inheritdoc />
    public void NotifyMediaStateChanged(MediaStateChangedNotification notification)
        => MediaStateChanged?.Invoke(notification);

    /// <inheritdoc />
    public void NotifyCallParticipantLeft(CallParticipantLeftNotification notification)
        => CallParticipantLeft?.Invoke(notification);
}
