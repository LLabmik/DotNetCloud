using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using SharePermission = DotNetCloud.Modules.Files.Models.SharePermission;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Reconstructs file content from stored chunks for download.
/// </summary>
internal sealed class DownloadService : IDownloadService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly ILogger<DownloadService> _logger;
    private readonly IPermissionService _permissions;
    private readonly string _tmpPath;

    public DownloadService(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        ILogger<DownloadService> logger,
        IPermissionService permissions,
        IOptions<FileUploadOptions> uploadOptions)
    {
        _db = db;
        _storageEngine = storageEngine;
        _logger = logger;
        _permissions = permissions;
        _tmpPath = uploadOptions.Value.TmpPath ?? Path.GetTempPath();
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadCurrentAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileNodeId, cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        await _permissions.RequirePermissionAsync(fileNodeId, caller, SharePermission.Read, cancellationToken);

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

        await _permissions.RequirePermissionAsync(version.FileNodeId, caller, SharePermission.Read, cancellationToken);

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

        await _permissions.RequirePermissionAsync(fileNodeId, caller, SharePermission.Read, cancellationToken);

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

        var relatedNodeIds = await _db.FileVersionChunks
            .AsNoTracking()
            .Where(vc => vc.FileChunk!.ChunkHash == chunkHash)
            .Select(vc => vc.FileVersion!.FileNodeId)
            .Distinct()
            .Take(64)
            .ToListAsync(cancellationToken);

        if (relatedNodeIds.Count == 0)
            return null;

        var canReadChunk = false;
        foreach (var nodeId in relatedNodeIds)
        {
            if (await _permissions.HasPermissionAsync(nodeId, caller, SharePermission.Read, cancellationToken))
            {
                canReadChunk = true;
                break;
            }
        }

        if (!canReadChunk)
            return null;

        var chunk = await _db.FileChunks
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChunkHash == chunkHash, cancellationToken);

        if (chunk is null)
            return null;

        return await _storageEngine.OpenReadStreamAsync(chunk.StoragePath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadZipAsync(IReadOnlyList<Guid> nodeIds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(nodeIds);

        if (nodeIds.Count == 0)
            throw new Core.Errors.InvalidOperationException("No nodes specified for download.");

        if (nodeIds.Count > 500)
            throw new Core.Errors.InvalidOperationException("Cannot download more than 500 items at once.");

        var tempPath = Path.Combine(_tmpPath, $"dotnetcloud-zip-{Guid.NewGuid():N}.zip");

        try
        {
            await using (var zipStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, System.IO.FileShare.None, 81920, FileOptions.Asynchronous))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var nodeId in nodeIds.Distinct())
                {
                    var node = await _db.FileNodes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

                    if (node is null) continue;

                    await _permissions.RequirePermissionAsync(nodeId, caller, SharePermission.Read, cancellationToken);

                    if (node.NodeType == FileNodeType.Folder)
                    {
                        await AddFolderToZipAsync(archive, node, node.Name, caller, cancellationToken);
                    }
                    else
                    {
                        await AddFileToZipAsync(archive, node, node.Name, cancellationToken);
                    }
                }
            }

            _logger.LogInformation("zip.created {NodeCount} {UserId}", nodeIds.Count, caller.UserId);

            return new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                System.IO.FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best-effort */ }
            throw;
        }
    }

    private async Task AddFolderToZipAsync(ZipArchive archive, FileNode folder, string pathPrefix, CallerContext caller, CancellationToken cancellationToken)
    {
        // Add an empty directory entry
        archive.CreateEntry(pathPrefix + "/");

        var children = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.ParentId == folder.Id && !n.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var child in children)
        {
            var childPath = $"{pathPrefix}/{child.Name}";

            if (child.NodeType == FileNodeType.Folder)
            {
                await AddFolderToZipAsync(archive, child, childPath, caller, cancellationToken);
            }
            else
            {
                await AddFileToZipAsync(archive, child, childPath, cancellationToken);
            }
        }
    }

    private async Task AddFileToZipAsync(ZipArchive archive, FileNode fileNode, string entryPath, CancellationToken cancellationToken)
    {
        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == fileNode.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);

        if (latestVersion is null) return;

        await using var entryStream = entry.Open();

        var versionChunks = await _db.FileVersionChunks
            .AsNoTracking()
            .Include(vc => vc.FileChunk)
            .Where(vc => vc.FileVersionId == latestVersion.Id)
            .OrderBy(vc => vc.SequenceIndex)
            .ToListAsync(cancellationToken);

        foreach (var vc in versionChunks)
        {
            if (vc.FileChunk!.Size == 0) continue;

            var chunkStream = await _storageEngine.OpenReadStreamAsync(vc.FileChunk.StoragePath, cancellationToken);
            if (chunkStream is null) continue;

            await using (chunkStream)
            {
                await chunkStream.CopyToAsync(entryStream, cancellationToken);
            }
        }
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

        // All chunks are zero-byte (e.g. empty file): serve empty stream without touching storage.
        if (versionChunks.All(vc => vc.FileChunk!.Size == 0))
            return Stream.Null;

        var tempPath = Path.Combine(_tmpPath, $"dotnetcloud-download-{versionId:N}-{Guid.NewGuid():N}.bin");

        try
        {
            await using (var tempWrite = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                System.IO.FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                foreach (var vc in versionChunks)
                {
                    // Zero-byte chunk contributes nothing to the output; skip storage I/O.
                    if (vc.FileChunk!.Size == 0)
                        continue;

                    var chunkStream = await _storageEngine.OpenReadStreamAsync(vc.FileChunk.StoragePath, cancellationToken);
                    if (chunkStream is null)
                    {
                        _logger.LogWarning("Chunk blob missing from storage for hash '{ChunkHash}' in version {VersionId}.",
                            vc.FileChunk.ChunkHash, versionId);
                        throw new NotFoundException(
                            $"File content is unavailable: chunk '{vc.FileChunk.ChunkHash[..8]}…' blob is missing from storage.");
                    }

                    await using (chunkStream)
                    {
                        await chunkStream.CopyToAsync(tempWrite, cancellationToken);
                    }
                }
            }

            _logger.LogDebug("Reconstructed file from {ChunkCount} chunks for version {VersionId} into temp file.",
                versionChunks.Count, versionId);

            return new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                System.IO.FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }

            throw;
        }
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
