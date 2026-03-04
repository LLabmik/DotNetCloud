namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a chat message in a channel.
/// </summary>
public sealed class Message
{
    /// <summary>Unique identifier for this message.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Channel this message was sent to.</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Navigation property to the channel.</summary>
    public Channel? Channel { get; set; }

    /// <summary>User who sent this message.</summary>
    public Guid SenderUserId { get; set; }

    /// <summary>Message content (supports Markdown).</summary>
    public required string Content { get; set; }

    /// <summary>Type of message.</summary>
    public MessageType Type { get; set; } = MessageType.Text;

    /// <summary>When the message was sent (UTC).</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the message was last edited (UTC).</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Whether the message has been edited.</summary>
    public bool IsEdited { get; set; }

    /// <summary>ID of the message this is a reply to.</summary>
    public Guid? ReplyToMessageId { get; set; }

    /// <summary>Navigation property to the replied-to message.</summary>
    public Message? ReplyToMessage { get; set; }

    /// <summary>Whether the message is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the message was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Attachments on this message.</summary>
    public ICollection<MessageAttachment> Attachments { get; set; } = [];

    /// <summary>Reactions on this message.</summary>
    public ICollection<MessageReaction> Reactions { get; set; } = [];

    /// <summary>Mentions in this message.</summary>
    public ICollection<MessageMention> Mentions { get; set; } = [];
}
