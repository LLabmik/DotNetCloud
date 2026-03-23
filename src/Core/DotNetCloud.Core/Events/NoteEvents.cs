namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a new note is created.
/// </summary>
public sealed record NoteCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the newly created note.
    /// </summary>
    public required Guid NoteId { get; init; }

    /// <summary>
    /// The title of the note.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The ID of the user who created the note.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// The folder ID the note was created in, if any.
    /// </summary>
    public Guid? FolderId { get; init; }
}

/// <summary>
/// Raised when an existing note is updated.
/// </summary>
public sealed record NoteUpdatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the updated note.
    /// </summary>
    public required Guid NoteId { get; init; }

    /// <summary>
    /// The ID of the user who updated the note.
    /// </summary>
    public required Guid UpdatedByUserId { get; init; }

    /// <summary>
    /// The new version number after the update.
    /// </summary>
    public required int NewVersion { get; init; }
}

/// <summary>
/// Raised when a note is deleted.
/// </summary>
public sealed record NoteDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the deleted note.
    /// </summary>
    public required Guid NoteId { get; init; }

    /// <summary>
    /// The ID of the user who deleted the note.
    /// </summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>
    /// Whether this was a permanent (hard) delete vs. soft delete.
    /// </summary>
    public bool IsPermanent { get; init; }
}
