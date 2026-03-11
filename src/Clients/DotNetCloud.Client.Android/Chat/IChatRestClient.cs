namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// REST API client for chat operations beyond the minimal quick-reply interface.
/// Used by the Android app for channel/message listing and full message management.
/// </summary>
public interface IChatRestClient
{
    // ── Channels ─────────────────────────────────────────────────────

    /// <summary>Returns all channels visible to the current user.</summary>
    Task<IReadOnlyList<ChannelSummary>> GetChannelsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);

    // ── Messages ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a page of messages for a channel, ordered newest-first.
    /// Pass <paramref name="beforeId"/> to paginate backwards.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid? beforeId = null, int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Sends a new message to a channel.</summary>
    Task<ChatMessage> SendMessageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, string content,
        CancellationToken ct = default);

    /// <summary>Marks all messages in a channel as read up to <paramref name="messageId"/>.</summary>
    Task MarkReadAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid messageId,
        CancellationToken ct = default);

    /// <summary>Notifies the server that the current user is typing in a channel.</summary>
    Task NotifyTypingAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId,
        CancellationToken ct = default);
}

/// <summary>Summary of a chat channel for channel-list display.</summary>
/// <param name="Id">Channel ID.</param>
/// <param name="Name">Display name.</param>
/// <param name="UnreadCount">Number of unread messages.</param>
/// <param name="HasMention">Whether unread messages contain a mention for the current user.</param>
/// <param name="LastMessagePreview">Preview text of the most recent message.</param>
/// <param name="LastMessageAt">When the most recent message was sent (UTC), or <c>null</c>.</param>
public sealed record ChannelSummary(
    Guid Id,
    string Name,
    int UnreadCount,
    bool HasMention,
    string? LastMessagePreview,
    DateTimeOffset? LastMessageAt);

/// <summary>A single chat message returned from the server.</summary>
/// <param name="Id">Message ID.</param>
/// <param name="ChannelId">Channel the message belongs to.</param>
/// <param name="SenderName">Display name of the sender.</param>
/// <param name="Content">Plain-text message body.</param>
/// <param name="SentAt">When the message was sent (UTC).</param>
/// <param name="IsEdited">Whether the message has been edited.</param>
public sealed record ChatMessage(
    Guid Id,
    Guid ChannelId,
    string SenderName,
    string Content,
    DateTimeOffset SentAt,
    bool IsEdited);
