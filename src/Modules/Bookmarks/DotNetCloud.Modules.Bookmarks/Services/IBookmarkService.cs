using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks.Models;

namespace DotNetCloud.Modules.Bookmarks.Services;

/// <summary>
/// Result of a delta sync query for the browser extension.
/// </summary>
public sealed record BookmarkSyncItem
{
    /// <summary>Bookmark ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Parent folder ID.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Bookmark URL.</summary>
    public required string Url { get; init; }

    /// <summary>Bookmark title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>When the bookmark was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Result of a delta sync query for the browser extension.
/// </summary>
public sealed record BookmarkSyncFolderItem
{
    /// <summary>Folder ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>When the folder was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Result of a delta sync query for the browser extension.
/// </summary>
public sealed record BookmarkSyncChangesResult
{
    /// <summary>Bookmarks that were created or updated.</summary>
    public required IReadOnlyList<BookmarkSyncItem> Items { get; init; }

    /// <summary>IDs of bookmarks that were deleted.</summary>
    public required IReadOnlyList<Guid> DeletedIds { get; init; }

    /// <summary>Folders that were created or updated.</summary>
    public required IReadOnlyList<BookmarkSyncFolderItem> Folders { get; init; }

    /// <summary>IDs of folders that were deleted.</summary>
    public required IReadOnlyList<Guid> DeletedFolderIds { get; init; }

    /// <summary>Cursor value for the next poll request.</summary>
    public DateTime NextCursor { get; init; }

    /// <summary>Whether there are more results beyond this page.</summary>
    public bool HasMore { get; init; }
}

/// <summary>
/// A single operation within a batch request.
/// </summary>
public sealed record BatchOperationResult
{
    /// <summary>The operation type (create, update, delete).</summary>
    public required string Operation { get; init; }

    /// <summary>Opaque client reference (used on creates to map IDs).</summary>
    public string? ClientRef { get; init; }

    /// <summary>The server-assigned ID for created items.</summary>
    public Guid? ServerId { get; init; }

    /// <summary>The ID of the affected bookmark/folder.</summary>
    public Guid? Id { get; init; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if the operation failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Request DTO for the batch operations endpoint.
/// </summary>
public sealed record BatchRequest
{
    /// <summary>Bookmarks to create.</summary>
    public IReadOnlyList<BatchCreateBookmark>? Creates { get; init; }

    /// <summary>Bookmarks to update.</summary>
    public IReadOnlyList<BatchUpdateBookmark>? Updates { get; init; }

    /// <summary>Bookmark IDs to delete.</summary>
    public IReadOnlyList<Guid>? Deletes { get; init; }

    /// <summary>Folders to create.</summary>
    public IReadOnlyList<BatchCreateFolder>? FolderCreates { get; init; }

    /// <summary>Folder IDs to delete.</summary>
    public IReadOnlyList<Guid>? FolderDeletes { get; init; }
}

/// <summary>Bookmark creation within a batch request.</summary>
public sealed record BatchCreateBookmark
{
    /// <summary>Opaque client reference for mapping.</summary>
    public string? ClientRef { get; init; }

    /// <summary>The bookmark URL.</summary>
    public required string Url { get; init; }

    /// <summary>Optional title.</summary>
    public string? Title { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Optional tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Optional folder ID.</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Optional favorite flag.</summary>
    public bool? IsFavorite { get; init; }
}

/// <summary>Bookmark update within a batch request.</summary>
public sealed record BatchUpdateBookmark
{
    /// <summary>Bookmark ID to update.</summary>
    public required Guid Id { get; init; }

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

/// <summary>Folder creation within a batch request.</summary>
public sealed record BatchCreateFolder
{
    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional parent folder ID.</summary>
    public Guid? ParentId { get; init; }
}

/// <summary>
/// Service for managing bookmark items.
/// </summary>
public interface IBookmarkService
{
    /// <summary>Lists bookmarks for the caller, optionally filtered by folder.</summary>
    Task<IReadOnlyList<BookmarkItem>> ListAsync(CallerContext caller, Guid? folderId, int skip, int take, CancellationToken ct = default);

    /// <summary>Gets a bookmark by ID.</summary>
    Task<BookmarkItem?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default);

    /// <summary>Creates a new bookmark.</summary>
    Task<BookmarkItem> CreateAsync(CreateBookmarkRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Updates an existing bookmark.</summary>
    Task<BookmarkItem> UpdateAsync(Guid id, UpdateBookmarkRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Soft-deletes a bookmark.</summary>
    Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default);

    /// <summary>Searches bookmarks by query text.</summary>
    Task<IReadOnlyList<BookmarkItem>> SearchAsync(CallerContext caller, string query, int skip, int take, CancellationToken ct = default);

    /// <summary>Gets bookmark changes since a specific timestamp for delta sync.</summary>
    Task<BookmarkSyncChangesResult> GetSyncChangesAsync(DateTimeOffset since, int limit, CallerContext caller, CancellationToken ct = default);

    /// <summary>Processes a batch of bookmark operations (create, update, delete) atomically.</summary>
    Task<IReadOnlyList<BatchOperationResult>> BatchAsync(BatchRequest request, CallerContext caller, CancellationToken ct = default);
}

/// <summary>Request DTO for creating a bookmark.</summary>
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

/// <summary>Request DTO for updating a bookmark.</summary>
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
