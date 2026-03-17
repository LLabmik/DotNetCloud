using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

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

    /// <summary>Notifies all subscribers that a new message was sent.</summary>
    void NotifyMessageReceived(Guid channelId, MessageDto message);

    /// <summary>Notifies all subscribers that a message was edited.</summary>
    void NotifyMessageEdited(Guid channelId, MessageDto message);

    /// <summary>Notifies all subscribers that a message was deleted.</summary>
    void NotifyMessageDeleted(Guid channelId, Guid messageId);
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
    public void NotifyMessageReceived(Guid channelId, MessageDto message)
        => MessageReceived?.Invoke(channelId, message);

    /// <inheritdoc />
    public void NotifyMessageEdited(Guid channelId, MessageDto message)
        => MessageEdited?.Invoke(channelId, message);

    /// <inheritdoc />
    public void NotifyMessageDeleted(Guid channelId, Guid messageId)
        => MessageDeleted?.Invoke(channelId, messageId);
}
