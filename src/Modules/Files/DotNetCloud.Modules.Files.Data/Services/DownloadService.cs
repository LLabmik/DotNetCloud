using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Reconstructs file content from stored chunks for download.
/// </summary>
internal sealed class DownloadService : IDownloadService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly ILogger<DownloadService> _logger;

    public DownloadService(FilesDbContext db, IFileStorageEngine storageEngine, ILogger<DownloadService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadCurrentAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileNodeId, cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        if (node.NodeType != FileNodeType.File)
            throw new Core.Errors.InvalidOperationException("Cannot download a folder.");

        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == fileNodeId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion is null)
            throw new NotFoundException("No version found for file.", fileNodeId);

        return await BuildStreamFromVersionAsync(latestVersion.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadVersionAsync(Guid fileVersionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var version = await _db.FileVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == fileVersionId, cancellationToken)
            ?? throw new NotFoundException("FileVersion", fileVersionId);

        return await BuildStreamFromVersionAsync(version.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetChunkManifestAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileNodeId, cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        if (node.NodeType != FileNodeType.File)
            throw new Core.Errors.InvalidOperationException("Cannot get chunk manifest for a folder.");

        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == fileNodeId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion is null)
            return [];

        return await _db.FileVersionChunks
            .AsNoTracking()
            .Include(vc => vc.FileChunk)
            .Where(vc => vc.FileVersionId == latestVersion.Id)
            .OrderBy(vc => vc.SequenceIndex)
            .Select(vc => vc.FileChunk!.ChunkHash)
            .ToListAsync(cancellationToken);
    }

    private async Task<Stream> BuildStreamFromVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var versionChunks = await _db.FileVersionChunks
            .AsNoTracking()
            .Include(vc => vc.FileChunk)
            .Where(vc => vc.FileVersionId == versionId)
            .OrderBy(vc => vc.SequenceIndex)
            .ToListAsync(cancellationToken);

        if (versionChunks.Count == 0)
            return Stream.Null;

        var streams = new List<Stream>(versionChunks.Count);

        foreach (var vc in versionChunks)
        {
            var stream = await _storageEngine.OpenReadStreamAsync(vc.FileChunk!.StoragePath, cancellationToken);
            if (stream is null)
            {
                // Dispose already-opened streams
                foreach (var s in streams)
                    await s.DisposeAsync();

                throw new Core.Errors.InvalidOperationException(
                    $"Chunk storage missing for hash '{vc.FileChunk.ChunkHash}'. File may be corrupted.");
            }
            streams.Add(stream);
        }

        _logger.LogDebug("Reconstructing file from {ChunkCount} chunks for version {VersionId}",
            versionChunks.Count, versionId);

        return new ConcatenatedStream(streams);
    }
}

/// <summary>
/// A read-only stream that concatenates multiple inner streams in sequence.
/// Disposes all inner streams when disposed.
/// </summary>
internal sealed class ConcatenatedStream : Stream
{
    private readonly IReadOnlyList<Stream> _streams;
    private int _currentIndex;

    public ConcatenatedStream(IReadOnlyList<Stream> streams)
    {
        _streams = streams;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _streams.Sum(s => s.Length);

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;

        while (totalRead < count && _currentIndex < _streams.Count)
        {
            var read = _streams[_currentIndex].Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                _currentIndex++;
                continue;
            }
            totalRead += read;
        }

        return totalRead;
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < count && _currentIndex < _streams.Count)
        {
            var read = await _streams[_currentIndex].ReadAsync(
                buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken);
            if (read == 0)
            {
                _currentIndex++;
                continue;
            }
            totalRead += read;
        }

        return totalRead;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length && _currentIndex < _streams.Count)
        {
            var read = await _streams[_currentIndex].ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0)
            {
                _currentIndex++;
                continue;
            }
            totalRead += read;
        }

        return totalRead;
    }

    /// <inheritdoc />
    public override void Flush() { }

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
            foreach (var stream in _streams)
                stream.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        foreach (var stream in _streams)
            await stream.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
