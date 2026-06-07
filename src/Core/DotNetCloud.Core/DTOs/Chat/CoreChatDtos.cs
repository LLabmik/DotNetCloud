using System;
using System.Collections.Generic;

namespace DotNetCloud.Core.DTOs.Chat;

/// <summary>
/// Response DTO representing a chat channel.
/// </summary>
public sealed record ChatChannelDto
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
/// Response DTO representing a chat message.
/// </summary>
public sealed record ChatMessageDto
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
    public IReadOnlyList<ChatMessageAttachmentDto> Attachments { get; init; } = [];

    /// <summary>Reactions on this message.</summary>
    public IReadOnlyList<ChatMessageReactionDto> Reactions { get; init; } = [];

    /// <summary>@mentions parsed from this message.</summary>
    public IReadOnlyList<ChatMessageMentionDto> Mentions { get; init; } = [];
}

/// <summary>
/// Response DTO representing a message attachment.
/// </summary>
public sealed record ChatMessageAttachmentDto
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
public sealed record ChatMessageReactionDto
{
    /// <summary>Emoji character or code.</summary>
    public required string Emoji { get; init; }

    /// <summary>Number of users who reacted with this emoji.</summary>
    public int Count { get; init; }

    /// <summary>User IDs who reacted.</summary>
    public IReadOnlyList<Guid> UserIds { get; init; } = [];
}

/// <summary>
/// Response DTO representing an @mention in a message.
/// </summary>
public sealed record ChatMessageMentionDto
{
    /// <summary>Type of mention: "User", "Channel", or "All".</summary>
    public required string Type { get; init; }

    /// <summary>Mentioned user ID. Null for @channel and @all.</summary>
    public Guid? MentionedUserId { get; init; }

    /// <summary>Start position of the mention in the message text.</summary>
    public int StartIndex { get; init; }

    /// <summary>Length of the mention text.</summary>
    public int Length { get; init; }
}

/// <summary>
/// DTO for unread message counts per channel.
/// </summary>
public sealed record ChatUnreadCountDto
{
    /// <summary>Channel ID.</summary>
    public Guid ChannelId { get; init; }

    /// <summary>Total unread message count.</summary>
    public int UnreadCount { get; init; }

    /// <summary>Number of unread @mentions.</summary>
    public int MentionCount { get; init; }

    /// <summary>Whether the caller has muted this channel.</summary>
    public bool IsMuted { get; init; }

    /// <summary>Whether the caller has pinned this channel.</summary>
    public bool IsPinned { get; init; }
}

/// <summary>
/// DTO for channel member information.
/// </summary>
public sealed record ChatChannelMemberDto
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>User's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>User's username.</summary>
    public string? Username { get; init; }

    /// <summary>User's role in the channel.</summary>
    public required string Role { get; init; }

    /// <summary>When the user joined (UTC).</summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>Whether the channel is muted by this user.</summary>
    public bool IsMuted { get; init; }

    /// <summary>Notification preference.</summary>
    public required string NotificationPref { get; init; }
}
