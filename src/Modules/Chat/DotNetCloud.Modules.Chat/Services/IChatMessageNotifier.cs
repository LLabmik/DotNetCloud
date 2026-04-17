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

    /// <summary>Notifies all subscribers that a new message was sent.</summary>
    void NotifyMessageReceived(Guid channelId, MessageDto message);

    /// <summary>Notifies all subscribers that a message was edited.</summary>
    void NotifyMessageEdited(Guid channelId, MessageDto message);

    /// <summary>Notifies all subscribers that a message was deleted.</summary>
    void NotifyMessageDeleted(Guid channelId, Guid messageId);

    /// <summary>Notifies all subscribers that a call is ringing in a channel.</summary>
    void NotifyCallRinging(CallRingingNotification notification);
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
}
