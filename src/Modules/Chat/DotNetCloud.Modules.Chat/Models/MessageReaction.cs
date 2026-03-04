namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents an emoji reaction on a chat message.
/// </summary>
public sealed class MessageReaction
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Message this reaction is on.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Navigation property to the message.</summary>
    public Message? Message { get; set; }

    /// <summary>User who reacted.</summary>
    public Guid UserId { get; set; }

    /// <summary>Emoji character or custom emoji code.</summary>
    public required string Emoji { get; set; }

    /// <summary>When the reaction was added (UTC).</summary>
    public DateTime ReactedAt { get; set; } = DateTime.UtcNow;
}
