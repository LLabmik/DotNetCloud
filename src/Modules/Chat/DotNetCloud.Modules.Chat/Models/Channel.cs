namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a chat channel (public, private, DM, or group).
/// </summary>
public sealed class Channel
{
    /// <summary>Unique identifier for this channel.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name of the channel.</summary>
    public required string Name { get; set; }

    /// <summary>Optional description of the channel.</summary>
    public string? Description { get; set; }

    /// <summary>Channel type (Public, Private, DirectMessage, Group).</summary>
    public ChannelType Type { get; set; } = ChannelType.Public;

    /// <summary>Organization this channel belongs to. Null for DMs.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>User who created this channel.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When the channel was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the last message was sent (UTC).</summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>Whether the channel is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Channel avatar URL.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Channel topic displayed in the header.</summary>
    public string? Topic { get; set; }

    /// <summary>Whether the channel is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the channel was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Members of this channel.</summary>
    public ICollection<ChannelMember> Members { get; set; } = [];

    /// <summary>Messages in this channel.</summary>
    public ICollection<Message> Messages { get; set; } = [];

    /// <summary>Pinned messages in this channel.</summary>
    public ICollection<PinnedMessage> PinnedMessages { get; set; } = [];
}
