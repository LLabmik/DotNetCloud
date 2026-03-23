namespace DotNetCloud.Modules.Notes.Models;

/// <summary>
/// Represents a folder for organizing notes. Supports hierarchical nesting.
/// </summary>
public sealed class NoteFolder
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner user ID.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Parent folder ID. Null for root-level folders.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Folder display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex color code for UI display.</summary>
    public string? Color { get; set; }

    /// <summary>Sort order within the parent folder.</summary>
    public int SortOrder { get; set; }

    /// <summary>Whether the folder has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when the folder was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Timestamp when the folder was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the folder was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation: parent folder.</summary>
    public NoteFolder? Parent { get; set; }

    /// <summary>Navigation: child folders.</summary>
    public ICollection<NoteFolder> Children { get; set; } = new List<NoteFolder>();

    /// <summary>Navigation: notes in this folder.</summary>
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
