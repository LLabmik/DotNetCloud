namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a message pinned in a channel for easy access.
/// </summary>
public sealed class PinnedMessage
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Channel where the message is pinned.</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Navigation property to the channel.</summary>
    public Channel? Channel { get; set; }

    /// <summary>Message that is pinned.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Navigation property to the pinned message.</summary>
    public Message? Message { get; set; }

    /// <summary>User who pinned the message.</summary>
    public Guid PinnedByUserId { get; set; }

    /// <summary>When the message was pinned (UTC).</summary>
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}
