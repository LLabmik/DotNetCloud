namespace DotNetCloud.Modules.AI.Models;

/// <summary>
/// Represents a single message within an AI conversation.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>Unique identifier for the message.</summary>
    public Guid Id { get; set; }

    /// <summary>The conversation this message belongs to.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>The role of the message author: "system", "user", or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The text content of the message.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Number of tokens in this message, if known.</summary>
    public int? TokenCount { get; set; }

    /// <summary>When this message was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation property: the parent conversation.</summary>
    public Conversation? Conversation { get; set; }
}
