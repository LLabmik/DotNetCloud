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

    /// <summary>Organization this channel belongs to (null for DMs).</summary>
    public Guid? OrganizationId { get; init; }

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

    /// <summary>@mentions parsed from this message.</summary>
    public IReadOnlyList<MessageMentionDto> Mentions { get; init; } = [];
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
/// Request DTO for attaching a file to an existing message.
/// </summary>
public sealed record CreateAttachmentDto
{
    /// <summary>File name to display.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type of the file.</summary>
    public required string MimeType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Thumbnail URL for image/video previews.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Files module FileNode ID (if linked to a file in storage).</summary>
    public Guid? FileNodeId { get; init; }
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
/// Response DTO representing an @mention in a message.
/// </summary>
public sealed record MessageMentionDto
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

/// <summary>
/// Request DTO for registering a device for push notifications.
/// </summary>
public sealed record RegisterDeviceDto
{
    /// <summary>Push provider device token.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Provider value: FCM or UnifiedPush.</summary>
    public required string Provider { get; init; }

    /// <summary>UnifiedPush endpoint URL, when provider is UnifiedPush.</summary>
    public string? Endpoint { get; init; }
}

/// <summary>
/// DTO for caller-level notification preferences for push delivery.
/// </summary>
public sealed record NotificationPreferencesDto
{
    /// <summary>Whether push notifications are globally enabled.</summary>
    public bool PushEnabled { get; init; } = true;

    /// <summary>Whether do-not-disturb mode is enabled.</summary>
    public bool DoNotDisturb { get; init; }

    /// <summary>Channel IDs muted for push notifications.</summary>
    public IReadOnlyList<Guid> MutedChannelIds { get; init; } = [];
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

// ── Channel Invite DTOs ─────────────────────────────────────────────

/// <summary>
/// Response DTO representing a channel invitation.
/// </summary>
public sealed record ChannelInviteDto
{
    /// <summary>Invite ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Channel ID the invite is for.</summary>
    public Guid ChannelId { get; init; }

    /// <summary>Channel name for display.</summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>User who is invited.</summary>
    public Guid InvitedUserId { get; init; }

    /// <summary>User who sent the invitation.</summary>
    public Guid InvitedByUserId { get; init; }

    /// <summary>Invite status: Pending, Accepted, Declined, Revoked.</summary>
    public required string Status { get; init; }

    /// <summary>When the invite was created (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the invite was responded to (UTC).</summary>
    public DateTime? RespondedAt { get; init; }

    /// <summary>Optional message from the inviter.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// Request DTO for creating a channel invitation for a single user.
/// </summary>
public sealed record CreateChannelInviteDto
{
    /// <summary>User ID to invite.</summary>
    public Guid UserId { get; init; }

    /// <summary>Optional message to include with the invitation.</summary>
    public string? Message { get; init; }
}

// ───────────────────────────────────────────────────────────────
// Video Calling DTOs
// ───────────────────────────────────────────────────────────────

/// <summary>
/// Response DTO representing a video call.
/// </summary>
public sealed record VideoCallDto
{
    /// <summary>Video call ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Channel the call belongs to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>User who initiated the call.</summary>
    public required Guid InitiatorUserId { get; init; }

    /// <summary>Display name of the initiator.</summary>
    public string? InitiatorDisplayName { get; init; }

    /// <summary>Current host of the call (has control authority).</summary>
    public Guid HostUserId { get; init; }

    /// <summary>Current call state.</summary>
    public required string State { get; init; }

    /// <summary>Type of media (Audio, Video).</summary>
    public required string MediaType { get; init; }

    /// <summary>Whether this is a group call.</summary>
    public bool IsGroupCall { get; init; }

    /// <summary>When the call started (UTC).</summary>
    public DateTime? StartedAtUtc { get; init; }

    /// <summary>When the call ended (UTC).</summary>
    public DateTime? EndedAtUtc { get; init; }

    /// <summary>Reason the call ended.</summary>
    public string? EndReason { get; init; }

    /// <summary>Maximum number of concurrent participants during the call.</summary>
    public int MaxParticipants { get; init; }

    /// <summary>Current participants in the call.</summary>
    public IReadOnlyList<CallParticipantDto> Participants { get; init; } = [];

    /// <summary>When the call record was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Response DTO representing a participant in a video call.
/// </summary>
public sealed record CallParticipantDto
{
    /// <summary>Participant record ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>User ID of the participant.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Display name of the participant.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Role in the call (Host, Participant).</summary>
    public required string Role { get; init; }

    /// <summary>When the participant joined (UTC).</summary>
    public DateTime JoinedAtUtc { get; init; }

    /// <summary>When the participant left (UTC), null if still in the call.</summary>
    public DateTime? LeftAtUtc { get; init; }

    /// <summary>Whether the participant has audio enabled.</summary>
    public bool HasAudio { get; set; }

    /// <summary>Whether the participant has video enabled.</summary>
    public bool HasVideo { get; set; }

    /// <summary>Whether the participant is sharing their screen.</summary>
    public bool HasScreenShare { get; set; }
}

/// <summary>
/// DTO for WebRTC signaling data (SDP offers, answers, and ICE candidates).
/// </summary>
public sealed record CallSignalDto
{
    /// <summary>The video call ID this signal belongs to.</summary>
    public required Guid CallId { get; init; }

    /// <summary>The user sending the signal.</summary>
    public required Guid FromUserId { get; init; }

    /// <summary>The target user for the signal.</summary>
    public required Guid ToUserId { get; init; }

    /// <summary>Signal type: "offer", "answer", or "ice-candidate".</summary>
    public required string Type { get; init; }

    /// <summary>The SDP or ICE candidate payload.</summary>
    public required string Payload { get; init; }
}

/// <summary>
/// Request DTO for initiating a video call in a channel.
/// </summary>
public sealed record StartCallRequest
{
    /// <summary>Type of media for the call (Audio, Video).</summary>
    public required string MediaType { get; init; }
}

/// <summary>
/// Request DTO for joining an active video call.
/// </summary>
public sealed record JoinCallRequest
{
    /// <summary>Whether to join with audio enabled.</summary>
    public bool WithAudio { get; init; } = true;

    /// <summary>Whether to join with video enabled.</summary>
    public bool WithVideo { get; init; }
}

/// <summary>
/// Response DTO for call history entries.
/// </summary>
public sealed record CallHistoryDto
{
    /// <summary>Video call ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Channel the call belonged to.</summary>
    public required Guid ChannelId { get; init; }

    /// <summary>User who initiated the call.</summary>
    public required Guid InitiatorUserId { get; init; }

    /// <summary>Display name of the initiator.</summary>
    public string? InitiatorDisplayName { get; init; }

    /// <summary>Final state of the call.</summary>
    public required string State { get; init; }

    /// <summary>Type of media (Audio, Video).</summary>
    public required string MediaType { get; init; }

    /// <summary>Reason the call ended.</summary>
    public string? EndReason { get; init; }

    /// <summary>Call duration in seconds, null if call was never connected.</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>Number of participants who joined the call.</summary>
    public int ParticipantCount { get; init; }

    /// <summary>When the call was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; init; }
}
