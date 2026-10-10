using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// EF Core-backed <see cref="IMediaCaptureTimeService"/>. On relational providers the write uses a
/// single set-based <c>UPDATE</c> guarded by <c>CapturedAtUtc IS NULL</c>, so concurrent capture
/// attempts (upload completion, the lazy thumbnail endpoint, the background backfill) cannot clobber
/// each other and no entity tracking is left behind in a long-lived scope. The InMemory provider used
/// by unit tests falls back to change tracking because it does not support <c>ExecuteUpdate</c>.
/// </summary>
public sealed class MediaCaptureTimeService : IMediaCaptureTimeService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly ILogger<MediaCaptureTimeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaCaptureTimeService"/> class.
    /// </summary>
    /// <param name="db">The Files database context.</param>
    /// <param name="storageEngine">Storage engine used to read whole-file media blobs.</param>
    /// <param name="logger">Logger instance.</param>
    public MediaCaptureTimeService(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        ILogger<MediaCaptureTimeService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TrySetCaptureTimeAsync(
        Guid fileNodeId,
        DateTime capturedAtUtc,
        CancellationToken cancellationToken = default)
    {
        // The InMemory provider (unit tests) does not support ExecuteUpdate — fall back to change
        // tracking there. Production providers use a single set-based UPDATE below.
        if (ChunkReferenceHelper.IsInMemoryProvider(_db))
        {
            var tracked = await _db.FileNodes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == fileNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (tracked is null || tracked.CapturedAtUtc is not null)
                return false;

            tracked.CapturedAtUtc = capturedAtUtc;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        // IgnoreQueryFilters so a node that is concurrently trashed/restored is still updated;
        // the IS NULL guard makes the write idempotent and race-safe across processes.
        var affected = await _db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.Id == fileNodeId && n.CapturedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(n => n.CapturedAtUtc, capturedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> TryCaptureFromStreamAsync(
        Guid fileNodeId,
        Stream content,
        string? mimeType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mimeType)
            || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var capturedAtUtc = await ImageCaptureTimeReader.TryReadAsync(content, cancellationToken).ConfigureAwait(false);
            if (capturedAtUtc is null)
                return false;

            return await TrySetCaptureTimeAsync(fileNodeId, capturedAtUtc.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort: capture time must never break the caller's primary operation.
            _logger.LogWarning(ex, "Failed to read image capture time for node {FileNodeId}.", fileNodeId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryCaptureFromStorageAsync(
        Guid fileNodeId,
        string? storagePath,
        string? mimeType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)
            || string.IsNullOrWhiteSpace(mimeType)
            || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            // Only whole-file media blobs exist at StoragePath; chunked content (legacy media not yet
            // converted) has no blob and is skipped here — the backfill reads those via DownloadService.
            if (!await _storageEngine.ExistsAsync(storagePath, cancellationToken).ConfigureAwait(false))
                return false;

            await using var stream = await _storageEngine
                .OpenReadStreamAsync(storagePath, cancellationToken)
                .ConfigureAwait(false);

            if (stream is null)
                return false;

            return await TryCaptureFromStreamAsync(fileNodeId, stream, mimeType, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read image capture time for node {FileNodeId}.", fileNodeId);
            return false;
        }
    }
}
