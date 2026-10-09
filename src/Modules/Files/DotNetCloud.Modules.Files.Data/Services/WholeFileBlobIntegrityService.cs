using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// A whole-file media version whose blob is not on disk (or no longer has the expected size).
/// </summary>
/// <param name="VersionId">The version whose content is missing.</param>
/// <param name="FileNodeId">Owning file node.</param>
/// <param name="FileName">File name, for operator context.</param>
/// <param name="OwnerId">Owner of the file.</param>
/// <param name="Size">Expected content size in bytes.</param>
/// <param name="StoragePath">Blob path that should hold the content.</param>
/// <param name="ChunkMappingsRemaining">Chunk mappings still present for the version.</param>
public sealed record WholeFileBlobDefect(
    Guid VersionId,
    Guid FileNodeId,
    string? FileName,
    Guid OwnerId,
    long Size,
    string? StoragePath,
    int ChunkMappingsRemaining)
{
    /// <summary>
    /// True when the content can still be rebuilt, because the version's chunk mappings survive.
    /// A download of a recoverable defect self-heals by reassembling from chunks.
    /// </summary>
    public bool IsRecoverable => ChunkMappingsRemaining > 0;
}

/// <summary>
/// Outcome of a whole-file blob integrity audit.
/// </summary>
/// <param name="ScannedVersions">Number of whole-file versions examined.</param>
/// <param name="Defects">Versions whose blob is missing; possibly capped by the caller's limit.</param>
/// <param name="Truncated">True when more defects exist than were returned.</param>
public sealed record WholeFileBlobIntegrityReport(
    int ScannedVersions,
    IReadOnlyList<WholeFileBlobDefect> Defects,
    bool Truncated)
{
    /// <summary>An empty report.</summary>
    public static readonly WholeFileBlobIntegrityReport None = new(0, Array.Empty<WholeFileBlobDefect>(), false);

    /// <summary>True when every scanned whole-file version still has its blob.</summary>
    public bool IsHealthy => Defects.Count == 0;

    /// <summary>Defects whose content can still be rebuilt from the surviving chunk mappings.</summary>
    public int RecoverableCount => Defects.Count(d => d.IsRecoverable);

    /// <summary>Defects whose content cannot be rebuilt from server-side state alone.</summary>
    public int UnrecoverableCount => Defects.Count(d => !d.IsRecoverable);
}

/// <summary>
/// Detects whole-file media versions whose blob is missing from storage.
/// </summary>
/// <remarks>
/// A whole-file version is normally the only copy of its content, so a missing blob is silent data
/// loss: the file still lists normally in the UI and only fails when it is read (e.g. as a 404 at
/// playback). This audit makes that state visible instead of leaving it to be discovered by a user.
/// </remarks>
public interface IWholeFileBlobIntegrityService
{
    /// <summary>
    /// Scans every whole-file version belonging to a live (non-trashed) file and reports the ones
    /// whose blob is absent from storage or shorter than the recorded size.
    /// </summary>
    /// <param name="maxDefects">Maximum defects to materialise; the scan itself is never truncated,
    /// but the returned list is capped so a catastrophic outcome cannot exhaust memory or logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WholeFileBlobIntegrityReport> AuditAsync(int maxDefects = 500, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IWholeFileBlobIntegrityService"/>, backed by <see cref="FilesDbContext"/> and
/// <see cref="IFileStorageEngine"/>.
/// </summary>
internal sealed class WholeFileBlobIntegrityService : IWholeFileBlobIntegrityService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="WholeFileBlobIntegrityService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="storageEngine">Physical storage engine.</param>
    public WholeFileBlobIntegrityService(FilesDbContext db, IFileStorageEngine storageEngine)
    {
        _db = db;
        _storageEngine = storageEngine;
    }

    /// <inheritdoc />
    public async Task<WholeFileBlobIntegrityReport> AuditAsync(int maxDefects = 500, CancellationToken cancellationToken = default)
    {
        var versions = await _db.FileVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => !v.IsChunked)
            .Select(v => new { v.Id, v.FileNodeId, v.Size, v.StoragePath })
            .ToListAsync(cancellationToken);

        if (versions.Count == 0)
            return WholeFileBlobIntegrityReport.None;

        var nodeIds = versions.Select(v => v.FileNodeId).Distinct().ToList();
        var nodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => nodeIds.Contains(n.Id) && !n.IsDeleted)
            .Select(n => new { n.Id, n.Name, n.OwnerId })
            .ToListAsync(cancellationToken);
        var nodeById = nodes.ToDictionary(n => n.Id);

        var defects = new List<WholeFileBlobDefect>();
        var truncated = false;

        foreach (var version in versions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Content in the trash is intentionally still on disk; the live set is what matters here.
            if (!nodeById.TryGetValue(version.FileNodeId, out var node))
                continue;

            if (!await IsBlobMissingAsync(version.StoragePath, version.Size, cancellationToken))
                continue;

            if (defects.Count >= maxDefects)
            {
                truncated = true;
                continue;
            }

            var mappings = await _db.FileVersionChunks
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(vc => vc.FileVersionId == version.Id, cancellationToken);

            defects.Add(new WholeFileBlobDefect(
                version.Id, version.FileNodeId, node.Name, node.OwnerId, version.Size, version.StoragePath, mappings));
        }

        return new WholeFileBlobIntegrityReport(versions.Count, defects, truncated);
    }

    private async Task<bool> IsBlobMissingAsync(string? storagePath, long expectedSize, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return true;

        var length = await _storageEngine.GetLengthAsync(storagePath, cancellationToken);
        if (length is null)
            return true;

        // A version with no recorded size can only be checked for existence.
        return expectedSize > 0 && length.Value != expectedSize;
    }
}
