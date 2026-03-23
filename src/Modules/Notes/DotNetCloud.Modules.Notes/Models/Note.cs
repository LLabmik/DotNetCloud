using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Notes.Models;

/// <summary>
/// Represents a note document.
/// </summary>
public sealed class Note
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner user ID.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Optional folder this note belongs to.</summary>
    public Guid? FolderId { get; set; }

    /// <summary>Note title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Markdown or plain-text content body.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Content format (Markdown or PlainText).</summary>
    public NoteContentFormat Format { get; set; } = NoteContentFormat.Markdown;

    /// <summary>Whether the note is pinned to the top of lists.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Whether the note is marked as a favorite.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Whether the note has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when the note was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Timestamp when the note was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the note was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Version number, incremented on each edit.</summary>
    public int Version { get; set; } = 1;

    /// <summary>ETag for conflict detection.</summary>
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Navigation: folder this note belongs to.</summary>
    public NoteFolder? Folder { get; set; }

    /// <summary>Tags applied to this note.</summary>
    public ICollection<NoteTag> Tags { get; set; } = new List<NoteTag>();

    /// <summary>Links to other entities.</summary>
    public ICollection<NoteLink> Links { get; set; } = new List<NoteLink>();

    /// <summary>Version history.</summary>
    public ICollection<NoteVersion> Versions { get; set; } = new List<NoteVersion>();

    /// <summary>Shares granting access to other users.</summary>
    public ICollection<NoteShare> Shares { get; set; } = new List<NoteShare>();
}
