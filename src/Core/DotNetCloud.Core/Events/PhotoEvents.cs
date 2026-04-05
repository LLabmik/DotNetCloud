namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a new photo is uploaded and indexed.
/// </summary>
public sealed record PhotoUploadedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the photo record.</summary>
    public required Guid PhotoId { get; init; }

    /// <summary>The FileNode ID the photo references.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The ID of the user who owns the photo.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The original filename.</summary>
    public required string FileName { get; init; }
}

/// <summary>
/// Raised when a photo is deleted.
/// </summary>
public sealed record PhotoDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the deleted photo.</summary>
    public required Guid PhotoId { get; init; }

    /// <summary>The ID of the user who deleted the photo.</summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>Whether this was a permanent (hard) delete vs. soft delete.</summary>
    public bool IsPermanent { get; init; }
}

/// <summary>
/// Raised when a new album is created.
/// </summary>
public sealed record AlbumCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the newly created album.</summary>
    public required Guid AlbumId { get; init; }

    /// <summary>The title of the album.</summary>
    public required string Title { get; init; }

    /// <summary>The ID of the user who created the album.</summary>
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Raised when an album is shared with another user.
/// </summary>
public sealed record AlbumSharedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the shared album.</summary>
    public required Guid AlbumId { get; init; }

    /// <summary>The ID of the user who shared the album.</summary>
    public required Guid SharedByUserId { get; init; }

    /// <summary>The ID of the user the album was shared with.</summary>
    public required Guid SharedWithUserId { get; init; }

    /// <summary>The permission level granted.</summary>
    public required string Permission { get; init; }
}

/// <summary>
/// Raised when a photo is edited (non-destructive edit applied).
/// </summary>
public sealed record PhotoEditedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the edited photo.</summary>
    public required Guid PhotoId { get; init; }

    /// <summary>The ID of the user who edited the photo.</summary>
    public required Guid EditedByUserId { get; init; }

    /// <summary>The type of edit operation applied.</summary>
    public required string EditType { get; init; }
}
