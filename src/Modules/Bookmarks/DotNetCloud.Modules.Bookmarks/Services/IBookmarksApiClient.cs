using DotNetCloud.Modules.Bookmarks.Models;

namespace DotNetCloud.Modules.Bookmarks.Services;

/// <summary>
/// HTTP API client for Bookmarks REST endpoints.
/// </summary>
public interface IBookmarksApiClient
{
    // Bookmarks
    Task<IReadOnlyList<BookmarkItem>> ListAsync(Guid? folderId = null, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<BookmarkItem?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BookmarkItem?> CreateAsync(CreateBookmarkRequest request, CancellationToken ct = default);
    Task<BookmarkItem?> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BookmarkItem>> SearchAsync(string query, int skip = 0, int take = 50, CancellationToken ct = default);

    // Folders
    Task<IReadOnlyList<BookmarkFolder>> ListFoldersAsync(Guid? parentId = null, CancellationToken ct = default);
    Task<BookmarkFolder?> GetFolderAsync(Guid id, CancellationToken ct = default);
    Task<BookmarkFolder?> CreateFolderAsync(CreateBookmarkFolderRequest request, CancellationToken ct = default);
    Task<BookmarkFolder?> UpdateFolderAsync(Guid id, UpdateBookmarkFolderRequest request, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid id, CancellationToken ct = default);

    // Import/Export
    Task<BookmarkImportResult?> ImportAsync(Stream fileStream, string fileName, Guid? folderId = null, CancellationToken ct = default);
    Task<byte[]> ExportAsync(Guid? folderId = null, CancellationToken ct = default);

    // Previews
    Task<BookmarkPreview?> FetchPreviewAsync(Guid bookmarkId, CancellationToken ct = default);
    Task<BookmarkPreview?> GetPreviewAsync(Guid bookmarkId, CancellationToken ct = default);
}
