namespace DotNetCloud.Modules.Bookmarks.Web.Services;

/// <summary>
/// A bookmark as the module's own REST API returns it.
/// </summary>
/// <remarks>
/// Tags stay as the raw JSON array string the host stores (<see cref="TagsJson"/>); the UI parses them
/// so a malformed value can never break a render.
/// </remarks>
public sealed record BookmarkDto
{
    /// <summary>Gets the bookmark id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the owning user id.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Gets the folder id, when the bookmark is filed.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Gets the URL as the user entered it.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Gets the user-provided title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the tags as a JSON array string.</summary>
    public string? TagsJson { get; init; }

    /// <summary>Gets a value indicating whether the bookmark is a favorite.</summary>
    public bool IsFavorite { get; init; }

    /// <summary>Gets when the bookmark was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Gets when the bookmark was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// A bookmark folder as the module's own REST API returns it.
/// </summary>
public sealed record BookmarkFolderDto
{
    /// <summary>Gets the folder id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the parent folder id, when nested.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the optional colour used for the folder dot.</summary>
    public string? Color { get; init; }

    /// <summary>Gets the sort order within the parent.</summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// Rich preview metadata for a bookmark.
/// </summary>
public sealed record BookmarkPreviewDto
{
    /// <summary>Gets the preview id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the bookmark the preview belongs to.</summary>
    public Guid BookmarkId { get; init; }

    /// <summary>Gets when the preview was last fetched.</summary>
    public DateTime? FetchedAt { get; init; }

    /// <summary>Gets the fetch status.</summary>
    public BookmarkPreviewStatus Status { get; init; }

    /// <summary>Gets the canonical URL the page declared.</summary>
    public string? CanonicalUrl { get; init; }

    /// <summary>Gets the site name from the page metadata.</summary>
    public string? SiteName { get; init; }

    /// <summary>Gets the resolved page title.</summary>
    public string? ResolvedTitle { get; init; }

    /// <summary>Gets the resolved page description.</summary>
    public string? ResolvedDescription { get; init; }

    /// <summary>Gets the favicon URL.</summary>
    public string? FaviconUrl { get; init; }

    /// <summary>Gets the preview image URL.</summary>
    public string? PreviewImageUrl { get; init; }

    /// <summary>Gets the failure explanation, when the fetch failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Status of a bookmark preview fetch.</summary>
public enum BookmarkPreviewStatus
{
    /// <summary>No preview has been fetched.</summary>
    NotFetched = 0,

    /// <summary>The preview was fetched successfully.</summary>
    Ok = 1,

    /// <summary>The fetch failed.</summary>
    Failed = 2,

    /// <summary>The fetch is running.</summary>
    Fetching = 3,
}

/// <summary>
/// The outcome of a browser-export import.
/// </summary>
public sealed record BookmarkImportResultDto
{
    /// <summary>Gets how many bookmarks were imported.</summary>
    public int ImportedCount { get; init; }

    /// <summary>Gets how many folders were created.</summary>
    public int FolderCount { get; init; }

    /// <summary>Gets how many entries were skipped.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Gets any per-entry errors the importer reported.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// The body of a create or update bookmark request.
/// </summary>
public sealed record BookmarkSaveRequest
{
    /// <summary>Gets the bookmark URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the optional title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets the folder id, when filed.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Gets the favorite flag, when the caller is changing it.</summary>
    public bool? IsFavorite { get; init; }
}

/// <summary>
/// The body of a create or update folder request.
/// </summary>
public sealed record BookmarkFolderSaveRequest
{
    /// <summary>Gets the folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the optional colour.</summary>
    public string? Color { get; init; }

    /// <summary>Gets the optional parent folder id.</summary>
    public Guid? ParentId { get; init; }
}
