namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a user's membership in a channel, including role and notification preferences.
/// </summary>
public sealed class ChannelMember
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Channel ID this membership belongs to.</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Navigation property to the channel.</summary>
    public Channel? Channel { get; set; }

    /// <summary>User ID of the member.</summary>
    public Guid UserId { get; set; }

    /// <summary>Role of the member in the channel.</summary>
    public ChannelMemberRole Role { get; set; } = ChannelMemberRole.Member;

    /// <summary>When the user joined the channel (UTC).</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the user last read the channel (UTC).</summary>
    public DateTime? LastReadAt { get; set; }

    /// <summary>ID of the last message the user has read.</summary>
    public Guid? LastReadMessageId { get; set; }

    /// <summary>Whether the user has muted this channel.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Whether the user has pinned this channel in their sidebar.</summary>
    public bool IsPinned { get; set; }

    /// <summary>User's notification preference for this channel.</summary>
    public NotificationPreference NotificationPref { get; set; } = NotificationPreference.All;
}
