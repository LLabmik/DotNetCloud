namespace DotNetCloud.Modules.Chat.DTOs;

/// <summary>
/// Response DTO representing a chat channel.
/// </summary>
public sealed record ChannelDto
{
    /// <summary>Channel ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Channel name.</summary>
    public required string Name { get; init; }

    /// <summary>Channel description.</summary>
    public string? Description { get; init; }

    /// <summary>Channel type.</summary>
    public required string Type { get; init; }

    /// <summary>Channel topic.</summary>
    public string? Topic { get; init; }

    /// <summary>Channel avatar URL.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Whether the channel is archived.</summary>
    public bool IsArchived { get; init; }

    /// <summary>Number of members.</summary>
    public int MemberCount { get; init; }

    /// <summary>Last activity timestamp (UTC).</summary>
    public DateTime? LastActivityAt { get; init; }

    /// <summary>Created timestamp (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>User who created the channel.</summary>
    public Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Request DTO for creating a new channel.
/// </summary>
public sealed record CreateChannelDto
{
    /// <summary>Channel name.</summary>
    public required string Name { get; init; }

    /// <summary>Channel description.</summary>
    public string? Description { get; init; }

    /// <summary>Channel type: "Public", "Private", or "Group".</summary>
    public required string Type { get; init; }

    /// <summary>Channel topic.</summary>
    public string? Topic { get; init; }

    /// <summary>Initial member user IDs (optional).</summary>
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];
}

/// <summary>
/// Request DTO for updating a channel.
/// </summary>
public sealed record UpdateChannelDto
{
    /// <summary>New channel name.</summary>
    public string? Name { get; init; }

    /// <summary>New channel description.</summary>
    public string? Description { get; init; }

    /// <summary>New channel topic.</summary>
    public string? Topic { get; init; }
}

/// <summary>
/// Response DTO representing a channel member.
/// </summary>
public sealed record ChannelMemberDto
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>User's role in the channel.</summary>
    public required string Role { get; init; }

    /// <summary>When the user joined (UTC).</summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>Whether the channel is muted by this user.</summary>
    public bool IsMuted { get; init; }

    /// <summary>Notification preference.</summary>
    public required string NotificationPref { get; init; }
}

/// <summary>
/// Request DTO for adding a member to a channel.
/// </summary>
public sealed record AddChannelMemberDto
{
    /// <summary>User ID to add.</summary>
    public Guid UserId { get; init; }

    /// <summary>Initial role for the member.</summary>
    public string? Role { get; init; }
}

/// <summary>
/// Response DTO representing a chat message.
/// </summary>
public sealed record MessageDto
{
    /// <summary>Message ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Channel ID.</summary>
    public Guid ChannelId { get; init; }

    /// <summary>Sender user ID.</summary>
    public Guid SenderUserId { get; init; }

    /// <summary>Message content (Markdown).</summary>
    public required string Content { get; init; }

    /// <summary>Message type.</summary>
    public required string Type { get; init; }

    /// <summary>Sent timestamp (UTC).</summary>
    public DateTime SentAt { get; init; }

    /// <summary>Edited timestamp (UTC).</summary>
    public DateTime? EditedAt { get; init; }

    /// <summary>Whether the message was edited.</summary>
    public bool IsEdited { get; init; }

    /// <summary>ID of the message this replies to.</summary>
    public Guid? ReplyToMessageId { get; init; }

    /// <summary>Attachments on this message.</summary>
    public IReadOnlyList<MessageAttachmentDto> Attachments { get; init; } = [];

    /// <summary>Reactions on this message.</summary>
    public IReadOnlyList<MessageReactionDto> Reactions { get; init; } = [];
}

/// <summary>
/// Request DTO for sending a new message.
/// </summary>
public sealed record SendMessageDto
{
    /// <summary>Message content (Markdown).</summary>
    public required string Content { get; init; }

    /// <summary>ID of the message to reply to (optional).</summary>
    public Guid? ReplyToMessageId { get; init; }
}

/// <summary>
/// Request DTO for editing a message.
/// </summary>
public sealed record EditMessageDto
{
    /// <summary>New message content (Markdown).</summary>
    public required string Content { get; init; }
}

/// <summary>
/// Response DTO representing a message attachment.
/// </summary>
public sealed record MessageAttachmentDto
{
    /// <summary>Attachment ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>File name.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type.</summary>
    public required string MimeType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Thumbnail URL.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Files module FileNode ID (if linked).</summary>
    public Guid? FileNodeId { get; init; }
}

/// <summary>
/// Response DTO representing reactions grouped by emoji.
/// </summary>
public sealed record MessageReactionDto
{
    /// <summary>Emoji character or code.</summary>
    public required string Emoji { get; init; }

    /// <summary>Number of users who reacted with this emoji.</summary>
    public int Count { get; init; }

    /// <summary>User IDs who reacted.</summary>
    public IReadOnlyList<Guid> UserIds { get; init; } = [];
}

/// <summary>
/// DTO for typing indicator broadcasts.
/// </summary>
public sealed record TypingIndicatorDto
{
    /// <summary>Channel where the user is typing.</summary>
    public Guid ChannelId { get; init; }

    /// <summary>User who is typing.</summary>
    public Guid UserId { get; init; }

    /// <summary>User's display name.</summary>
    public string? DisplayName { get; init; }
}

/// <summary>
/// DTO for user presence information.
/// </summary>
public sealed record PresenceDto
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>Presence status: "Online", "Away", "DoNotDisturb", "Offline".</summary>
    public required string Status { get; init; }

    /// <summary>Custom status message.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Last seen timestamp (UTC).</summary>
    public DateTime? LastSeenAt { get; init; }
}

/// <summary>
/// DTO for unread message counts per channel.
/// </summary>
public sealed record UnreadCountDto
{
    /// <summary>Channel ID.</summary>
    public Guid ChannelId { get; init; }

    /// <summary>Total unread message count.</summary>
    public int UnreadCount { get; init; }

    /// <summary>Number of unread @mentions.</summary>
    public int MentionCount { get; init; }
}

// ── Announcement DTOs ───────────────────────────────────────────────

/// <summary>
/// Response DTO representing an announcement.
/// </summary>
public sealed record AnnouncementDto
{
    /// <summary>Announcement ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Organization ID.</summary>
    public Guid OrganizationId { get; init; }

    /// <summary>Author user ID.</summary>
    public Guid AuthorUserId { get; init; }

    /// <summary>Announcement title.</summary>
    public required string Title { get; init; }

    /// <summary>Announcement content (Markdown).</summary>
    public required string Content { get; init; }

    /// <summary>Priority: Normal, Important, Urgent.</summary>
    public required string Priority { get; init; }

    /// <summary>Published timestamp (UTC).</summary>
    public DateTime PublishedAt { get; init; }

    /// <summary>Expiry timestamp (UTC), if any.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Whether the announcement is pinned.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Whether acknowledgement is required.</summary>
    public bool RequiresAcknowledgement { get; init; }

    /// <summary>Number of acknowledgements received.</summary>
    public int AcknowledgementCount { get; init; }
}

/// <summary>
/// Request DTO for creating an announcement.
/// </summary>
public sealed record CreateAnnouncementDto
{
    /// <summary>Organization ID.</summary>
    public Guid OrganizationId { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Content (Markdown).</summary>
    public required string Content { get; init; }

    /// <summary>Priority: "Normal", "Important", "Urgent".</summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>Optional expiry date.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Whether acknowledgement is required.</summary>
    public bool RequiresAcknowledgement { get; init; }
}

/// <summary>
/// Request DTO for updating an announcement.
/// </summary>
public sealed record UpdateAnnouncementDto
{
    /// <summary>New title.</summary>
    public string? Title { get; init; }

    /// <summary>New content.</summary>
    public string? Content { get; init; }

    /// <summary>New priority.</summary>
    public string? Priority { get; init; }

    /// <summary>New expiry date.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Whether to pin the announcement.</summary>
    public bool? IsPinned { get; init; }
}

/// <summary>
/// Response DTO for an announcement acknowledgement.
/// </summary>
public sealed record AnnouncementAcknowledgementDto
{
    /// <summary>User ID who acknowledged.</summary>
    public Guid UserId { get; init; }

    /// <summary>When they acknowledged (UTC).</summary>
    public DateTime AcknowledgedAt { get; init; }
}
