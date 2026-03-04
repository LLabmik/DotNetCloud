namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents an @mention in a chat message, with position tracking.
/// </summary>
public sealed class MessageMention
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Message containing this mention.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Navigation property to the message.</summary>
    public Message? Message { get; set; }

    /// <summary>User who was mentioned. Null for @channel or @all.</summary>
    public Guid? MentionedUserId { get; set; }

    /// <summary>Type of mention (User, Channel, All).</summary>
    public MentionType Type { get; set; } = MentionType.User;

    /// <summary>Start position of the mention in the message text.</summary>
    public int StartIndex { get; set; }

    /// <summary>Length of the mention text.</summary>
    public int Length { get; set; }
}
