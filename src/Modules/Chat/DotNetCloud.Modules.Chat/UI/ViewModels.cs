namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// View model for displaying a channel in the sidebar.
/// </summary>
public sealed class ChannelViewModel
{
    /// <summary>Channel ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Channel type (Public, Private, DirectMessage, Group).</summary>
    public string Type { get; init; } = "Public";

    /// <summary>Channel topic.</summary>
    public string? Topic { get; set; }

    /// <summary>Unread message count.</summary>
    public int UnreadCount { get; set; }

    /// <summary>Unread mention count.</summary>
    public int MentionCount { get; set; }

    /// <summary>Whether the channel is currently active/selected.</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Presence state for direct-message/group peers (Online, Away, Offline).
    /// </summary>
    public string PresenceStatus { get; set; } = "Offline";

    /// <summary>
    /// Whether the channel is pinned in the sidebar.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>Last activity timestamp.</summary>
    public DateTime? LastActivityAt { get; init; }

    /// <summary>Number of members.</summary>
    public int MemberCount { get; init; }
}

/// <summary>
/// View model for displaying a message in the chat view.
/// </summary>
public sealed class MessageViewModel
{
    /// <summary>Message ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Sender user ID.</summary>
    public Guid SenderUserId { get; init; }

    /// <summary>Sender display name.</summary>
    public string SenderName { get; init; } = string.Empty;

    /// <summary>Sender avatar URL.</summary>
    public string? SenderAvatarUrl { get; init; }

    /// <summary>Message content (Markdown).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Message type.</summary>
    public string Type { get; init; } = "Text";

    /// <summary>Sent timestamp.</summary>
    public DateTime SentAt { get; init; }

    /// <summary>Whether the message has been edited.</summary>
    public bool IsEdited { get; init; }

    /// <summary>ID of the message this replies to.</summary>
    public Guid? ReplyToMessageId { get; init; }

    /// <summary>Reactions grouped by emoji.</summary>
    public List<ReactionViewModel> Reactions { get; set; } = [];

    /// <summary>Attachments on this message.</summary>
    public List<AttachmentViewModel> Attachments { get; init; } = [];

    /// <summary>Mentions parsed from this message.</summary>
    public List<MentionViewModel> Mentions { get; init; } = [];

    /// <summary>Whether this message mentions the current user (directly or via @all/@channel).</summary>
    public bool IsMentioningCurrentUser { get; set; }
}

/// <summary>
/// View model for a grouped reaction display.
/// </summary>
public sealed class ReactionViewModel
{
    /// <summary>Emoji character or code.</summary>
    public string Emoji { get; init; } = string.Empty;

    /// <summary>Number of users who reacted.</summary>
    public int Count { get; init; }

    /// <summary>Whether the current user has this reaction.</summary>
    public bool HasReacted { get; set; }
}

/// <summary>
/// View model for a message attachment.
/// </summary>
public sealed class AttachmentViewModel
{
    /// <summary>Attachment ID.</summary>
    public Guid Id { get; init; }

    /// <summary>File name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>MIME type.</summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Thumbnail URL for preview.</summary>
    public string? ThumbnailUrl { get; init; }
}

/// <summary>
/// View model for an @mention in a message.
/// </summary>
public sealed class MentionViewModel
{
    /// <summary>Type of mention: "User", "Channel", or "All".</summary>
    public string Type { get; init; } = "User";

    /// <summary>Mentioned user ID. Null for @channel and @all.</summary>
    public Guid? MentionedUserId { get; init; }

    /// <summary>Start position in the message text.</summary>
    public int StartIndex { get; init; }

    /// <summary>Length of the mention text.</summary>
    public int Length { get; init; }
}

/// <summary>
/// View model for a channel member.
/// </summary>
public sealed class MemberViewModel
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Avatar URL.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Role in the channel.</summary>
    public string Role { get; init; } = "Member";

    /// <summary>Presence status.</summary>
    public string Status { get; init; } = "Offline";

    /// <summary>Optional user handle.</summary>
    public string? Username { get; init; }

    /// <summary>Optional user bio text for profile popup.</summary>
    public string? Bio { get; init; }
}

/// <summary>
/// View model for a typing indicator.
/// </summary>
public sealed record TypingUserViewModel(Guid UserId, string DisplayName);

/// <summary>
/// View model for displaying an announcement.
/// </summary>
public sealed class AnnouncementViewModel
{
    /// <summary>Announcement ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Content (Markdown).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Priority: Normal, Important, Urgent.</summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>Published timestamp.</summary>
    public DateTime PublishedAt { get; init; }

    /// <summary>Optional expiry timestamp.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Whether the announcement is pinned.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Whether acknowledgement is required.</summary>
    public bool RequiresAcknowledgement { get; init; }

    /// <summary>Number of acknowledgements.</summary>
    public int AcknowledgementCount { get; init; }

    /// <summary>Author display name.</summary>
    public string AuthorName { get; init; } = string.Empty;
}
