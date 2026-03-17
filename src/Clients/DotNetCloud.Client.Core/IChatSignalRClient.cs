namespace DotNetCloud.Client.Core;

/// <summary>
/// Abstraction for a desktop chat SignalR client used by SyncTray features.
/// </summary>
public interface IChatSignalRClient
{
    /// <summary>
    /// Raised when unread counts change for a chat channel.
    /// </summary>
    event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <summary>
    /// Raised when a new chat message is received.
    /// </summary>
    event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <summary>
    /// Connects to the chat real-time transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Joins the SignalR broadcast group for a channel so the client receives real-time messages.
    /// </summary>
    /// <param name="channelId">The channel to join.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task JoinChannelGroupAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves the SignalR broadcast group for a channel.
    /// </summary>
    /// <param name="channelId">The channel to leave.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LeaveChannelGroupAsync(Guid channelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event payload for per-channel unread updates.
/// </summary>
/// <param name="ChannelId">Chat channel identifier.</param>
/// <param name="UnreadCount">Unread count for the channel.</param>
/// <param name="HasMention">Whether the channel currently contains unread mentions.</param>
public sealed record ChatUnreadCountUpdatedEventArgs(string ChannelId, int UnreadCount, bool HasMention);

/// <summary>
/// Event payload for newly received chat messages.
/// </summary>
/// <param name="ChannelId">Chat channel identifier.</param>
/// <param name="ChannelDisplayName">Display name of the channel.</param>
/// <param name="SenderDisplayName">Display name of the sender.</param>
/// <param name="MessagePreview">Short message preview.</param>
/// <param name="MessageId">Server-assigned message identifier.</param>
/// <param name="SentAt">Server timestamp when the message was sent (UTC).</param>
/// <param name="IsMention">Whether the message contains a mention for the current user.</param>
public sealed record ChatMessageReceivedEventArgs(
    string ChannelId,
    string ChannelDisplayName,
    string SenderDisplayName,
    string MessagePreview,
    Guid MessageId,
    DateTime SentAt,
    bool IsMention);
