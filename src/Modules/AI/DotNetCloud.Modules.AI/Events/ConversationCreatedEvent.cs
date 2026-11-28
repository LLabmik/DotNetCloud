using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.AI;

/// <summary>
/// Raised when a new AI conversation is created.
/// </summary>
public sealed record ConversationCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the new conversation.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>The ID of the user who created the conversation.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The model selected for the conversation.</summary>
    public required string Model { get; init; }
}
