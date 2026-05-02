using DotNetCloud.Modules.Bookmarks.Models;

namespace DotNetCloud.Modules.Bookmarks.Services;

/// <summary>
/// Service for fetching and managing rich previews for bookmarks.
/// </summary>
public interface IBookmarkPreviewService
{
    /// <summary>Fetches a preview for the specified bookmark.</summary>
    Task<BookmarkPreview> FetchPreviewAsync(Guid bookmarkId, CancellationToken ct = default);

    /// <summary>Refreshes an existing preview.</summary>
    Task<BookmarkPreview> RefreshPreviewAsync(Guid bookmarkId, CancellationToken ct = default);

    /// <summary>Gets the current preview for a bookmark.</summary>
    Task<BookmarkPreview?> GetPreviewAsync(Guid bookmarkId, CancellationToken ct = default);

    /// <summary>Refreshes all stale previews (older than the specified age).</summary>
    Task<int> RefreshStalePreviewsAsync(TimeSpan maxAge, CancellationToken ct = default);
}
