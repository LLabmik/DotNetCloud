namespace DotNetCloud.Modules.Bookmarks.Models;

/// <summary>
/// A folder for organizing bookmarks. Supports hierarchical nesting.
/// </summary>
public sealed class BookmarkFolder
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this folder.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Parent folder ID for hierarchical nesting.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Folder display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional color for UI display.</summary>
    public string? Color { get; set; }

    /// <summary>Sort order within the parent folder.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the folder was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the folder was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the folder was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent folder navigation property.</summary>
    public BookmarkFolder? Parent { get; set; }

    /// <summary>Child folders.</summary>
    public ICollection<BookmarkFolder> Children { get; set; } = new List<BookmarkFolder>();

    /// <summary>Bookmarks in this folder.</summary>
    public ICollection<BookmarkItem> Bookmarks { get; set; } = new List<BookmarkItem>();
}
