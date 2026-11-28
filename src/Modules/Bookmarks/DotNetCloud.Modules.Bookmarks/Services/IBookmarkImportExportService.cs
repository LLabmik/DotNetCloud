using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Bookmarks.Services;

/// <summary>
/// Service for importing and exporting bookmarks in browser HTML format.
/// </summary>
public interface IBookmarkImportExportService
{
    /// <summary>Imports bookmarks from a browser HTML export stream.</summary>
    Task<BookmarkImportResult> ImportHtmlAsync(Stream htmlStream, Guid? targetFolderId, CallerContext caller, CancellationToken ct = default);

    /// <summary>Exports bookmarks to browser HTML format.</summary>
    Task<Stream> ExportHtmlAsync(CallerContext caller, Guid? folderId, CancellationToken ct = default);
}

/// <summary>Result of a bookmark import operation.</summary>
public sealed record BookmarkImportResult
{
    /// <summary>Number of bookmarks successfully imported.</summary>
    public int ImportedCount { get; set; }

    /// <summary>Number of folders created.</summary>
    public int FolderCount { get; set; }

    /// <summary>Number of items skipped (duplicates or errors).</summary>
    public int SkippedCount { get; set; }

    /// <summary>Any errors encountered during import.</summary>
    public List<string> Errors { get; set; } = [];
}
