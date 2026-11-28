using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.AI;

/// <summary>
/// Raised when a message is added to an AI conversation.
/// </summary>
public sealed record ConversationMessageEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The conversation the message was added to.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>The role of the message: "user" or "assistant".</summary>
    public required string Role { get; init; }

    /// <summary>The user who owns the conversation.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Token count of the message, if known.</summary>
    public int? TokenCount { get; init; }
}
