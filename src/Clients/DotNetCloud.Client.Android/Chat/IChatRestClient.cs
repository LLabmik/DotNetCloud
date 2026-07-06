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

    /// <summary>
    /// Sends a new message with attachments to a channel.
    /// </summary>
    Task<ChatMessage> SendMessageWithAttachmentsAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, string content,
        IReadOnlyList<ChatAttachment>? attachments,
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

    // ── Members ───────────────────────────────────────────────────────

    /// <summary>Returns all members of a channel.</summary>
    Task<IReadOnlyList<ChannelMemberSummary>> GetChannelMembersAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId,
        CancellationToken ct = default);

    /// <summary>Removes the current user from a channel.</summary>
    Task LeaveChannelAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId,
        CancellationToken ct = default);

    // ── Image Upload ─────────────────────────────────────────────────

    /// <summary>
    /// Uploads an image file to the server for use in a chat message.
    /// Returns the upload result containing the serving URL and metadata.
    /// </summary>
    Task<ChatImageUploadResult> UploadImageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default);

    // ── Attachments ──────────────────────────────────────────────────

    /// <summary>
    /// Attaches an uploaded file to a channel message.
    /// <paramref name="fileId"/> is the ID returned by the Files module after the upload.
    /// </summary>
    Task<ChatMessage> SendFileMessageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid fileId, string fileName,
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

/// <summary>Result of uploading an image to a chat channel.</summary>
/// <param name="Url">Serving URL path for the image (e.g., /api/v1/chat/uploads/abc123.png).</param>
/// <param name="FileName">Original file name.</param>
/// <param name="MimeType">MIME content type.</param>
/// <param name="FileSize">File size in bytes.</param>
public sealed record ChatImageUploadResult(
    string Url,
    string FileName,
    string MimeType,
    long FileSize);

/// <summary>Attachment metadata on a chat message.</summary>
/// <param name="Id">Attachment ID (server-assigned).</param>
/// <param name="FileName">File name for display.</param>
/// <param name="MimeType">MIME content type.</param>
/// <param name="FileSize">File size in bytes.</param>
/// <param name="ThumbnailUrl">URL for image/video preview (null for non-previewable types).</param>
public sealed record ChatAttachment(
    Guid Id,
    string FileName,
    string MimeType,
    long FileSize,
    string? ThumbnailUrl);

/// <summary>A single chat message returned from the server.</summary>
/// <param name="Id">Message ID.</param>
/// <param name="ChannelId">Channel the message belongs to.</param>
/// <param name="SenderUserId">User ID of the sender.</param>
/// <param name="SenderName">Display name of the sender (resolved client-side from channel members).</param>
/// <param name="Content">Plain-text message body.</param>
/// <param name="SentAt">When the message was sent (UTC).</param>
/// <param name="IsEdited">Whether the message has been edited.</param>
/// <param name="Attachments">Attachments on this message (optional).</param>
public sealed record ChatMessage(
    Guid Id,
    Guid ChannelId,
    Guid SenderUserId,
    string SenderName,
    string Content,
    DateTimeOffset SentAt,
    bool IsEdited,
    IReadOnlyList<ChatAttachment>? Attachments = null);

/// <summary>Summary of a channel member for the member list.</summary>
/// <param name="UserId">User identifier.</param>
/// <param name="DisplayName">User display name.</param>
/// <param name="Role">Role in the channel: Owner, Admin, or Member.</param>
/// <param name="IsOnline">Whether the user is currently online.</param>
public sealed record ChannelMemberSummary(
    Guid UserId,
    string DisplayName,
    string Role,
    bool IsOnline);
