namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Bookmarks module.
/// </summary>
public interface IBookmarksApiClient
{
    // Bookmarks
    Task<IReadOnlyList<BookmarkItemDto>> ListAsync(Guid? folderId = null, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<BookmarkItemDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BookmarkItemDto?> CreateAsync(CreateBookmarkRequest request, CancellationToken ct = default);
    Task<BookmarkItemDto?> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BookmarkItemDto>> SearchAsync(string query, int skip = 0, int take = 50, CancellationToken ct = default);

    // Folders
    Task<IReadOnlyList<BookmarkFolderDto>> ListFoldersAsync(Guid? parentId = null, CancellationToken ct = default);
    Task<BookmarkFolderDto?> GetFolderAsync(Guid id, CancellationToken ct = default);
    Task<BookmarkFolderDto?> CreateFolderAsync(CreateBookmarkFolderRequest request, CancellationToken ct = default);
    Task<BookmarkFolderDto?> UpdateFolderAsync(Guid id, UpdateBookmarkFolderRequest request, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid id, CancellationToken ct = default);

    // Import/Export
    Task<BookmarkImportResult?> ImportAsync(Stream fileStream, string fileName, Guid? folderId = null, CancellationToken ct = default);
    Task<byte[]> ExportAsync(Guid? folderId = null, CancellationToken ct = default);

    // Previews
    Task<BookmarkPreviewDto?> FetchPreviewAsync(Guid bookmarkId, CancellationToken ct = default);
    Task<BookmarkPreviewDto?> GetPreviewAsync(Guid bookmarkId, CancellationToken ct = default);
}

/// <summary>
/// Flat DTO for a bookmark item, without EF navigation properties.
/// </summary>
public sealed record BookmarkItemDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The user who owns this bookmark.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Optional folder assignment.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>The bookmark URL.</summary>
    public string Url { get; init; } = "";

    /// <summary>Normalized URL for deduplication.</summary>
    public string NormalizedUrl { get; init; } = "";

    /// <summary>User-provided title (overrides scraped title when set).</summary>
    public string Title { get; init; } = "";

    /// <summary>User-provided description.</summary>
    public string? Description { get; init; }

    /// <summary>User notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Whether the bookmark is favorited.</summary>
    public bool IsFavorite { get; init; }

    /// <summary>When the bookmark was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the bookmark was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Flat DTO for a bookmark folder, without EF navigation properties.
/// </summary>
public sealed record BookmarkFolderDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The user who owns this folder.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Parent folder ID for hierarchical nesting.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Folder display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Optional color for UI display.</summary>
    public string? Color { get; init; }

    /// <summary>Sort order within the parent folder.</summary>
    public int SortOrder { get; init; }

    /// <summary>When the folder was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the folder was last updated.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Number of bookmarks directly in this folder.</summary>
    public int BookmarkCount { get; init; }

    /// <summary>Number of child folders.</summary>
    public int ChildFolderCount { get; init; }
}

/// <summary>
/// Request DTO for creating a bookmark.
/// </summary>
public sealed record CreateBookmarkRequest
{
    /// <summary>The bookmark URL.</summary>
    public required string Url { get; init; }

    /// <summary>Optional user-provided title.</summary>
    public string? Title { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Optional tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Optional folder ID.</summary>
    public Guid? FolderId { get; init; }
}

/// <summary>
/// Request DTO for updating a bookmark.
/// </summary>
public sealed record UpdateBookmarkRequest
{
    /// <summary>Updated URL.</summary>
    public string? Url { get; init; }

    /// <summary>Updated title.</summary>
    public string? Title { get; init; }

    /// <summary>Updated description.</summary>
    public string? Description { get; init; }

    /// <summary>Updated notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Updated tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Updated folder ID.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Updated favorite flag.</summary>
    public bool? IsFavorite { get; init; }
}

/// <summary>
/// Request DTO for creating a bookmark folder.
/// </summary>
public sealed record CreateBookmarkFolderRequest
{
    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Optional color for UI display.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Request DTO for updating a bookmark folder.
/// </summary>
public sealed record UpdateBookmarkFolderRequest
{
    /// <summary>Updated folder name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Updated color.</summary>
    public string? Color { get; init; }

    /// <summary>Updated sort order.</summary>
    public int? SortOrder { get; init; }
}

/// <summary>
/// Result of a bookmark import operation.
/// </summary>
public sealed record BookmarkImportResult
{
    /// <summary>Number of bookmarks successfully imported.</summary>
    public int ImportedCount { get; init; }

    /// <summary>Number of folders created.</summary>
    public int FolderCount { get; init; }

    /// <summary>Number of items skipped (duplicates or errors).</summary>
    public int SkippedCount { get; init; }

    /// <summary>Any errors encountered during import.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Flat DTO for bookmark preview metadata.
/// </summary>
public sealed record BookmarkPreviewDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Foreign key to the parent bookmark.</summary>
    public Guid BookmarkId { get; init; }

    /// <summary>When the preview was last fetched.</summary>
    public DateTime? FetchedAt { get; init; }

    /// <summary>Current fetch status.</summary>
    public string Status { get; init; } = "NotFetched";

    /// <summary>Canonical URL from the page.</summary>
    public string? CanonicalUrl { get; init; }

    /// <summary>Site name extracted from OG or Twitter card metadata.</summary>
    public string? SiteName { get; init; }

    /// <summary>Resolved title from the page.</summary>
    public string? ResolvedTitle { get; init; }

    /// <summary>Resolved description from the page.</summary>
    public string? ResolvedDescription { get; init; }

    /// <summary>Favicon URL.</summary>
    public string? FaviconUrl { get; init; }

    /// <summary>Preview image URL.</summary>
    public string? PreviewImageUrl { get; init; }

    /// <summary>Content-Type of the fetched page.</summary>
    public string? ContentType { get; init; }

    /// <summary>Error message if fetch failed.</summary>
    public string? ErrorMessage { get; init; }
}
