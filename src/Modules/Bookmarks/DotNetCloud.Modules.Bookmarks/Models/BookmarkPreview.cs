namespace DotNetCloud.Modules.Bookmarks.Models;

/// <summary>
/// Rich preview metadata fetched from the bookmarked URL.
/// </summary>
public sealed class BookmarkPreview
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the parent bookmark.</summary>
    public Guid BookmarkId { get; set; }

    /// <summary>When the preview was last fetched.</summary>
    public DateTime? FetchedAt { get; set; }

    /// <summary>Current fetch status.</summary>
    public BookmarkPreviewStatus Status { get; set; } = BookmarkPreviewStatus.NotFetched;

    /// <summary>Canonical URL from the page (link rel=canonical).</summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>Site name extracted from OG or Twitter card metadata.</summary>
    public string? SiteName { get; set; }

    /// <summary>Resolved title from the page.</summary>
    public string? ResolvedTitle { get; set; }

    /// <summary>Resolved description from the page.</summary>
    public string? ResolvedDescription { get; set; }

    /// <summary>Favicon URL.</summary>
    public string? FaviconUrl { get; set; }

    /// <summary>Preview image URL (OG image or Twitter card image).</summary>
    public string? PreviewImageUrl { get; set; }

    /// <summary>Content-Type of the fetched page.</summary>
    public string? ContentType { get; set; }

    /// <summary>Content-Length in bytes.</summary>
    public long? ContentLength { get; set; }

    /// <summary>Error message if fetch failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>ETag header value for conditional fetching.</summary>
    public string? ETag { get; set; }

    /// <summary>Last-Modified header value for conditional fetching.</summary>
    public string? LastModified { get; set; }

    /// <summary>When the preview record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the preview record was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent bookmark navigation property.</summary>
    public BookmarkItem? Bookmark { get; set; }
}

/// <summary>
/// Status of a bookmark preview fetch.
/// </summary>
public enum BookmarkPreviewStatus
{
    /// <summary>Preview has not been fetched yet.</summary>
    NotFetched = 0,

    /// <summary>Preview was fetched successfully.</summary>
    Ok = 1,

    /// <summary>Preview fetch failed.</summary>
    Failed = 2,

    /// <summary>Preview is currently being fetched.</summary>
    Fetching = 3
}
