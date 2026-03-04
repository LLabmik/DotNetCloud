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

    /// <inheritdoc />
    public async Task<Stream?> DownloadChunkByHashAsync(string chunkHash, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkHash);

        var chunk = await _db.FileChunks
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChunkHash == chunkHash, cancellationToken);

        if (chunk is null)
            return null;

        return await _storageEngine.OpenReadStreamAsync(chunk.StoragePath, cancellationToken);
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
/// Supports seeking when all inner streams are seekable.
/// Disposes all inner streams when disposed.
/// </summary>
internal sealed class ConcatenatedStream : Stream
{
    private readonly IReadOnlyList<Stream> _streams;
    private int _currentIndex;
    private long _position;

    public ConcatenatedStream(IReadOnlyList<Stream> streams)
    {
        _streams = streams;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => _streams.Count == 0 || _streams.All(s => s.CanSeek);

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _streams.Sum(s => s.Length);

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!CanSeek)
            throw new NotSupportedException("One or more inner streams do not support seeking.");

        var length = Length;
        long targetPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (targetPosition < 0)
            throw new IOException("Cannot seek before the beginning of the stream.");

        targetPosition = Math.Min(targetPosition, length);

        if (_streams.Count == 0)
        {
            _position = targetPosition;
            return _position;
        }

        // Find which inner stream contains the target position
        long accumulated = 0;
        for (var i = 0; i < _streams.Count; i++)
        {
            var streamLen = _streams[i].Length;
            var isLast = i == _streams.Count - 1;

            if (targetPosition <= accumulated + streamLen || isLast)
            {
                _currentIndex = i;
                _streams[i].Seek(targetPosition - accumulated, SeekOrigin.Begin);

                // Reset subsequent streams to their beginning so they're read correctly next
                for (var j = i + 1; j < _streams.Count; j++)
                    _streams[j].Seek(0, SeekOrigin.Begin);

                break;
            }

            accumulated += streamLen;
        }

        _position = targetPosition;
        return _position;
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

        _position += totalRead;
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

        _position += totalRead;
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

        _position += totalRead;
        return totalRead;
    }

    /// <inheritdoc />
    public override void Flush() { }

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
