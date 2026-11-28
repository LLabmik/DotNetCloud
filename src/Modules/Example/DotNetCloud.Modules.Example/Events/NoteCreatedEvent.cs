using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Example.Events;

/// <summary>
/// Published when a new note is created in the example module.
/// Demonstrates how modules define and publish domain events.
/// </summary>
public sealed record NoteCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the newly created note.</summary>
    public required Guid NoteId { get; init; }

    /// <summary>The title of the created note.</summary>
    public required string Title { get; init; }

    /// <summary>The user who created the note.</summary>
    public required Guid CreatedByUserId { get; init; }
}
