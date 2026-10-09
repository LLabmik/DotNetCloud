using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Default <see cref="IWholeFileStorageService"/> implementation backed by <see cref="FilesDbContext"/>
/// and an <see cref="IFileStorageEngine"/>.
/// </summary>
internal sealed class WholeFileStorageService : IWholeFileStorageService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly FileUploadOptions _options;
    private readonly ILogger<WholeFileStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WholeFileStorageService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="storageEngine">Physical storage engine.</param>
    /// <param name="uploadOptions">Upload options (carries the <c>WholeFileMediaStorage</c> gate).</param>
    /// <param name="logger">Logger instance.</param>
    public WholeFileStorageService(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        IOptions<FileUploadOptions> uploadOptions,
        ILogger<WholeFileStorageService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _options = uploadOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool ShouldStoreWholeFile(string? mimeType, string? fileName)
        => FileStorageClassifier.IsWholeFileEligible(mimeType, fileName, _options.WholeFileMediaStorage);

    /// <inheritdoc />
    public Task<Stream?> OpenWholeFileStreamAsync(FileVersion version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        return _storageEngine.OpenReadStreamAsync(version.StoragePath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ConvertVersionToWholeFileAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await _db.FileVersions
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version is null || !version.IsChunked)
            return false;

        var fileName = await _db.FileNodes
            .Where(n => n.Id == version.FileNodeId)
            .Select(n => n.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ShouldStoreWholeFile(version.MimeType, fileName))
            return false;

        // Capture the values we need, then stop tracking the entity so the raw UPDATE below and the
        // subsequent entity removals don't fight over the same key.
        var storagePath = version.StoragePath;
        var expectedSize = version.Size;
        _db.Entry(version).State = EntityState.Detached;

        var versionChunks = await _db.FileVersionChunks
            .AsNoTracking()
            .Include(vc => vc.FileChunk)
            .Where(vc => vc.FileVersionId == versionId)
            .OrderBy(vc => vc.SequenceIndex)
            .ToListAsync(cancellationToken);

        if (versionChunks.Count == 0)
            return false; // Nothing to reassemble — leave the version as-is.

        // Step 1: write the blob before flipping the flag. While the version is still flagged
        // chunked, the read path keeps serving chunks, so a crash here is harmless.
        if (!await _storageEngine.ExistsAsync(storagePath, cancellationToken))
        {
            try
            {
                var chunkSources = versionChunks
                    .Select(vc => (Path: vc.FileChunk!.StoragePath, Size: (long)vc.FileChunk.Size))
                    .ToList();

                var source = new ChunkSequenceReadStream(_storageEngine, chunkSources);
                await _storageEngine.WriteFromStreamAsync(storagePath, source, expectedSize, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Whole-file conversion failed while writing the blob for version {VersionId}; leaving it chunked.",
                    versionId);
                return false;
            }
        }

        // Step 2: atomically flip the flag. The DB update is the cross-process gate — only the caller
        // that observes the transition removes the chunk mappings and decrements the refcounts.
        try
        {
            return await FlipAndCleanupAsync(versionId, versionChunks, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Whole-file conversion failed while flipping the storage mode for version {VersionId}; leaving it chunked.",
                versionId);
            return false;
        }
    }

    private async Task<bool> FlipAndCleanupAsync(
        Guid versionId,
        IReadOnlyList<FileVersionChunk> versionChunks,
        CancellationToken cancellationToken)
    {
        if (ChunkReferenceHelper.IsInMemoryProvider(_db))
        {
            var tracked = await _db.FileVersions.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
            if (tracked is null || !tracked.IsChunked)
                return false;

            tracked.IsChunked = false;

            var mappings = await _db.FileVersionChunks
                .Where(vc => vc.FileVersionId == versionId)
                .ToListAsync(cancellationToken);
            _db.FileVersionChunks.RemoveRange(mappings);

            foreach (var vc in versionChunks)
                await ChunkReferenceHelper.DecrementAsync(_db, vc.FileChunkId, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var converted = false;
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var affected = await _db.FileVersions
                .Where(v => v.Id == versionId && v.IsChunked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsChunked, false), ct);

            if (affected != 1)
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            await _db.FileVersionChunks
                .Where(vc => vc.FileVersionId == versionId)
                .ExecuteDeleteAsync(ct);

            foreach (var vc in versionChunks)
                await ChunkReferenceHelper.DecrementAsync(_db, vc.FileChunkId, ct);

            await transaction.CommitAsync(ct);
            converted = true;
        }, cancellationToken);

        if (converted)
        {
            _logger.LogInformation(
                "Converted version {VersionId} to whole-file storage ({ChunkCount} chunk(s) released).",
                versionId, versionChunks.Count);
        }

        return converted;
    }
}

/// <summary>
/// A forward-only read stream that concatenates a version's chunk blobs, opening one blob at a time
/// so a multi-GB file never holds hundreds of file handles open at once. Used to feed the whole-file
/// conversion without buffering the payload in memory.
/// </summary>
internal sealed class ChunkSequenceReadStream : Stream
{
    private readonly IFileStorageEngine _storageEngine;
    private readonly IReadOnlyList<(string Path, long Size)> _chunks;
    private int _index = -1;
    private Stream? _current;
    private long _position;

    public ChunkSequenceReadStream(IFileStorageEngine storageEngine, IReadOnlyList<(string Path, long Size)> chunks)
    {
        _storageEngine = storageEngine;
        _chunks = chunks;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _chunks.Sum(c => c.Size);

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_current is null)
            {
                _index++;
                if (_index >= _chunks.Count)
                    return 0;

                _current = await _storageEngine.OpenReadStreamAsync(_chunks[_index].Path, cancellationToken)
                    ?? throw new IOException($"Chunk blob missing from storage: {_chunks[_index].Path}");
            }

            var read = await _current.ReadAsync(buffer, cancellationToken);
            if (read > 0)
            {
                _position += read;
                return read;
            }

            await _current.DisposeAsync();
            _current = null;
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _current?.Dispose();
            _current = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_current is not null)
        {
            await _current.DisposeAsync();
            _current = null;
        }

        await base.DisposeAsync();
    }
}
