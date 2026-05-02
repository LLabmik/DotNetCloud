namespace DotNetCloud.Modules.Bookmarks.Models;

/// <summary>
/// A bookmark item with URL, metadata, and optional folder assignment.
/// </summary>
public sealed class BookmarkItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this bookmark.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Optional folder assignment.</summary>
    public Guid? FolderId { get; set; }

    /// <summary>The bookmark URL as entered by the user.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Normalized URL for deduplication.</summary>
    public string NormalizedUrl { get; set; } = string.Empty;

    /// <summary>User-provided title (overrides scraped title when set).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>User-provided description.</summary>
    public string? Description { get; set; }

    /// <summary>User notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Tags stored as a JSON array.</summary>
    public string? TagsJson { get; set; }

    /// <summary>Whether the bookmark is favorited.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the bookmark was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the bookmark was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the bookmark was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Folder navigation property.</summary>
    public BookmarkFolder? Folder { get; set; }

    /// <summary>Rich preview data for this bookmark.</summary>
    public BookmarkPreview? Preview { get; set; }
}
