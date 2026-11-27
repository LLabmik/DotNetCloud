namespace DotNetCloud.Modules.AI.Models;

/// <summary>
/// Represents an AI chat conversation belonging to a user.
/// </summary>
public sealed class Conversation
{
    /// <summary>Unique identifier for the conversation.</summary>
    public Guid Id { get; set; }

    /// <summary>The user who owns this conversation.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Display title for the conversation.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The model used for this conversation (e.g., "gpt-oss:20b").</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>System prompt for the conversation, if any.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>When the conversation was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the last message was added.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Whether this conversation has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Navigation property: messages in this conversation.</summary>
    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
