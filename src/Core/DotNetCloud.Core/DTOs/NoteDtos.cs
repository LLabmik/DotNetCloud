namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a note document.
/// </summary>
public sealed record NoteDto
{
    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Identifier of the user who owns this note.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Optional folder this note is filed under.
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>
    /// Note title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Markdown content body of the note.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Content format indicator.
    /// </summary>
    public NoteContentFormat Format { get; init; } = NoteContentFormat.Markdown;

    /// <summary>
    /// Whether the note is pinned to the top.
    /// </summary>
    public bool IsPinned { get; init; }

    /// <summary>
    /// Whether the note is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; init; }

    /// <summary>
    /// Whether the note has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Timestamp when the note was deleted, if applicable.
    /// </summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Timestamp when the note was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the note was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Current version number (incremented on each edit).
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Tags applied to this note.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Metadata about linked entities (file IDs, calendar event IDs, contact IDs).
    /// </summary>
    public IReadOnlyList<NoteLinkDto> Links { get; init; } = [];

    /// <summary>
    /// Content-length in characters for display without fetching full body.
    /// </summary>
    public int ContentLength { get; init; }

    /// <summary>
    /// ETag for conflict detection.
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Content format for a note.
/// </summary>
public enum NoteContentFormat
{
    /// <summary>Markdown text.</summary>
    Markdown,

    /// <summary>Plain text.</summary>
    PlainText
}

/// <summary>
/// A link from a note to another entity in the system.
/// </summary>
public sealed record NoteLinkDto
{
    /// <summary>
    /// The type of entity being linked.
    /// </summary>
    public required NoteLinkType LinkType { get; init; }

    /// <summary>
    /// The ID of the linked entity.
    /// </summary>
    public required Guid TargetId { get; init; }

    /// <summary>
    /// Optional display label for the link.
    /// </summary>
    public string? DisplayLabel { get; init; }
}

/// <summary>
/// Type of entity a note can link to.
/// </summary>
public enum NoteLinkType
{
    /// <summary>Link to a file entity.</summary>
    File,

    /// <summary>Link to a calendar event.</summary>
    CalendarEvent,

    /// <summary>Link to a contact.</summary>
    Contact,

    /// <summary>Link to another note.</summary>
    Note
}

/// <summary>
/// Represents a folder for organizing notes.
/// </summary>
public sealed record NoteFolderDto
{
    /// <summary>
    /// Unique identifier for the folder.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Identifier of the user who owns this folder.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Parent folder ID. Null for root-level folders.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// Folder display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Sort order within the parent folder.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Number of notes directly in this folder.
    /// </summary>
    public int NoteCount { get; init; }

    /// <summary>
    /// Timestamp when the folder was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the folder was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// A historical version of a note.
/// </summary>
public sealed record NoteVersionDto
{
    /// <summary>
    /// Unique identifier for this version record.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The note this version belongs to.
    /// </summary>
    public required Guid NoteId { get; init; }

    /// <summary>
    /// Version number.
    /// </summary>
    public required int VersionNumber { get; init; }

    /// <summary>
    /// Title at the time of this version.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Content body at the time of this version.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// ID of the user who created this version.
    /// </summary>
    public required Guid EditedByUserId { get; init; }

    /// <summary>
    /// Timestamp when this version was saved.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request DTO for creating a new note.
/// </summary>
public sealed record CreateNoteDto
{
    /// <summary>
    /// Optional folder to place the note in.
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>
    /// Note title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Initial Markdown content body.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Content format.
    /// </summary>
    public NoteContentFormat Format { get; init; } = NoteContentFormat.Markdown;

    /// <summary>
    /// Tags to apply.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Entity links to create.
    /// </summary>
    public IReadOnlyList<NoteLinkDto> Links { get; init; } = [];
}

/// <summary>
/// Request DTO for updating an existing note.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateNoteDto
{
    /// <summary>
    /// Updated folder assignment. Null means no change.
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>
    /// Updated title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated content body.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Updated content format.
    /// </summary>
    public NoteContentFormat? Format { get; init; }

    /// <summary>
    /// Updated pinned state.
    /// </summary>
    public bool? IsPinned { get; init; }

    /// <summary>
    /// Updated favorite state.
    /// </summary>
    public bool? IsFavorite { get; init; }

    /// <summary>
    /// Replacement tag list. Null means no change.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Replacement link list. Null means no change.
    /// </summary>
    public IReadOnlyList<NoteLinkDto>? Links { get; init; }

    /// <summary>
    /// Expected version for optimistic concurrency. Null skips the check.
    /// </summary>
    public int? ExpectedVersion { get; init; }
}

/// <summary>
/// Request DTO for creating a note folder.
/// </summary>
public sealed record CreateNoteFolderDto
{
    /// <summary>
    /// Parent folder ID. Null for root-level folders.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// Folder display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }
}

/// <summary>
/// Request DTO for updating a note folder.
/// </summary>
public sealed record UpdateNoteFolderDto
{
    /// <summary>
    /// Updated folder name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Updated parent folder. Null means no change.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// Updated color code.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Updated sort order.
    /// </summary>
    public int? SortOrder { get; init; }
}
