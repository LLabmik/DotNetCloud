using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks.Models;

namespace DotNetCloud.Modules.Bookmarks.Services;

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
