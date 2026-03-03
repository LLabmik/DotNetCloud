using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Example.Events;

/// <summary>
/// Published when an existing note is deleted from the example module.
/// </summary>
public sealed record NoteDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the deleted note.</summary>
    public required Guid NoteId { get; init; }

    /// <summary>The user who deleted the note.</summary>
    public required Guid DeletedByUserId { get; init; }
}
