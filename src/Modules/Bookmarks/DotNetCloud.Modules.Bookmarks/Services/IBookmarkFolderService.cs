using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks.Models;

namespace DotNetCloud.Modules.Bookmarks.Services;

/// <summary>
/// Service for managing bookmark folders.
/// </summary>
public interface IBookmarkFolderService
{
    /// <summary>Lists folders for the caller, optionally filtered by parent.</summary>
    Task<IReadOnlyList<BookmarkFolder>> ListAsync(CallerContext caller, Guid? parentId, CancellationToken ct = default);

    /// <summary>Gets a folder by ID.</summary>
    Task<BookmarkFolder?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default);

    /// <summary>Creates a new folder.</summary>
    Task<BookmarkFolder> CreateAsync(CreateBookmarkFolderRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Updates an existing folder.</summary>
    Task<BookmarkFolder> UpdateAsync(Guid id, UpdateBookmarkFolderRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Soft-deletes a folder and all its contents.</summary>
    Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default);
}

/// <summary>Request DTO for creating a bookmark folder.</summary>
public sealed record CreateBookmarkFolderRequest
{
    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Optional color for UI display.</summary>
    public string? Color { get; init; }
}

/// <summary>Request DTO for updating a bookmark folder.</summary>
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
