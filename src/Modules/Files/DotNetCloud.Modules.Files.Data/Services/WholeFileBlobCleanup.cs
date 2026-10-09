using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Deletes whole-file media blobs from storage once nothing references them any more.
/// A blob is only removed when neither a <see cref="Models.FileVersion"/> nor a
/// <see cref="Models.FileNode"/> (including soft-deleted nodes) points at its storage path, so
/// content-addressed deduplication is preserved.
/// </summary>
internal static class WholeFileBlobCleanup
{
    private const string WholeFilePrefix = "files/";

    /// <summary>
    /// Deletes the blob at <paramref name="storagePath"/> when it is a whole-file blob (<c>files/…</c>)
    /// that no version or node references any more. No-op for chunk paths and referenced blobs.
    /// </summary>
    /// <param name="db">Database context used for the reference check.</param>
    /// <param name="storageEngine">Physical storage engine.</param>
    /// <param name="storagePath">Candidate blob path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DeleteIfUnreferencedAsync(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)
            || !storagePath.StartsWith(WholeFilePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var referencedByVersion = await db.FileVersions
            .AnyAsync(v => v.StoragePath == storagePath, cancellationToken);

        if (referencedByVersion)
            return;

        // Trashed nodes keep their StoragePath so a restore can still find the content.
        var referencedByNode = await db.FileNodes
            .IgnoreQueryFilters()
            .AnyAsync(n => n.StoragePath == storagePath, cancellationToken);

        if (referencedByNode)
            return;

        await storageEngine.DeleteAsync(storagePath, cancellationToken);
    }
}
