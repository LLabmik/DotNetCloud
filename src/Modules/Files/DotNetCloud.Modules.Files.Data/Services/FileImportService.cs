using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>Outcome of importing one template file into a user's tree.</summary>
/// <param name="Node">The created or updated node.</param>
/// <param name="StorageReused">True when the content-addressed blob already existed.</param>
/// <param name="BytesWritten">Bytes written to storage (0 when the blob was reused).</param>
/// <param name="Changed">True when content was created or replaced.</param>
public sealed record FileImportResult(FileNode Node, bool StorageReused, long BytesWritten, bool Changed);

/// <summary>
/// Internal, content-addressed write path used by default-content provisioning.
/// It mirrors upload completion: identical content maps to one physical blob, MIME typing and
/// EXIF capture behave the same, and the same search/sync/destination events are published.
/// </summary>
/// <remarks>
/// Default content is deliberately stored as a whole-file blob (<see cref="FileVersion.IsChunked"/>
/// = <see langword="false"/>) rather than as chunks: the administrator copies a real file once, and
/// every user's logical copy shares that one content-addressed blob.
/// <para>
/// <b>No version history:</b> a default file has exactly one version row that is replaced in place.
/// Version tracking is disabled for default content by design.
/// </para>
/// </remarks>
public sealed class FileImportService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly IEventBus _eventBus;
    private readonly ISyncChangeNotifier _syncNotifier;
    private readonly IMediaCaptureTimeService? _captureTimeService;
    private readonly ILogger<FileImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileImportService"/> class.
    /// </summary>
    public FileImportService(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        IEventBus eventBus,
        ISyncChangeNotifier syncNotifier,
        ILogger<FileImportService> logger,
        IMediaCaptureTimeService? captureTimeService = null)
    {
        _db = db;
        _storageEngine = storageEngine;
        _eventBus = eventBus;
        _syncNotifier = syncNotifier;
        _logger = logger;
        _captureTimeService = captureTimeService;
    }

    /// <summary>
    /// Creates a new file node under <paramref name="parentId"/> whose content is the file at
    /// <paramref name="sourcePath"/>. The blob is written once per unique content hash.
    /// </summary>
    /// <param name="ownerId">Owner of the new node.</param>
    /// <param name="parentId">Parent folder (null for the root level).</param>
    /// <param name="fileName">Display name for the node.</param>
    /// <param name="sourcePath">Absolute path of the template file.</param>
    /// <param name="isReadOnly">Whether the new node is read-only default content.</param>
    /// <param name="caller">Caller recorded on events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FileImportResult> ImportFileAsync(
        Guid ownerId,
        Guid? parentId,
        string fileName,
        string sourcePath,
        bool isReadOnly,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(caller);

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
            throw new NotFoundException("File", sourcePath);

        var (hash, bytesWritten, reused) = await EnsureBlobAsync(sourcePath, sourceInfo.Length, cancellationToken)
            .ConfigureAwait(false);
        var storagePath = ContentHasher.GetFileStoragePath(hash);

        string parentPath = string.Empty;
        var parentDepth = -1;
        if (parentId.HasValue)
        {
            var parent = await _db.FileNodes.FindAsync([parentId.Value], cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("FileNode", parentId.Value);

            if (parent.NodeType != FileNodeType.Folder)
                throw new Core.Errors.ValidationException("ParentId", "Parent must be a folder.");

            parentPath = parent.MaterializedPath;
            parentDepth = parent.Depth;
        }

        var node = new FileNode
        {
            Name = fileName,
            NodeType = FileNodeType.File,
            MimeType = MimeTypeGuesser.FromFileName(fileName),
            Size = sourceInfo.Length,
            ParentId = parentId,
            OwnerId = ownerId,
            ContentHash = hash,
            StoragePath = storagePath,
            Depth = parentDepth + 1,
            IsReadOnly = isReadOnly,
            CurrentVersion = 1
        };

        node.MaterializedPath = string.IsNullOrEmpty(parentPath)
            ? $"/{node.Id}"
            : $"{parentPath}/{node.Id}";

        _db.FileNodes.Add(node);

        // Exactly one version row — default content has no version history.
        _db.FileVersions.Add(new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = node.Size,
            ContentHash = hash,
            StoragePath = storagePath,
            MimeType = node.MimeType,
            CreatedByUserId = caller.UserId,
            IsChunked = false
        });

        await SyncCursorHelper.AssignNextSequenceAsync(_db, node, ownerId, _syncNotifier, cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await AfterWriteAsync(node, storagePath, caller, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Imported '{FileName}' for {UserId} ({Bytes} bytes, storage {State}).",
            fileName, ownerId, node.Size, reused ? "reused" : "written");

        return new FileImportResult(node, reused, bytesWritten, Changed: true);
    }

    /// <summary>
    /// Replaces the content of an existing default-content file in place.
    /// The single version row is updated rather than a new version being appended.
    /// </summary>
    /// <param name="node">Tracked node to update.</param>
    /// <param name="sourcePath">Absolute path of the replacement template file.</param>
    /// <param name="caller">Caller recorded on events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the content changed; otherwise <see langword="false"/>.</returns>
    public async Task<bool> ReplaceCurrentContentAsync(
        FileNode node,
        string sourcePath,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(caller);

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
            throw new NotFoundException("File", sourcePath);

        var (hash, _, _) = await EnsureBlobAsync(sourcePath, sourceInfo.Length, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(hash, node.ContentHash, StringComparison.OrdinalIgnoreCase)
            && sourceInfo.Length == node.Size)
        {
            return false;
        }

        var storagePath = ContentHasher.GetFileStoragePath(hash);

        node.Size = sourceInfo.Length;
        node.ContentHash = hash;
        node.StoragePath = storagePath;
        node.UpdatedAt = DateTime.UtcNow;

        // Default content has no version history: keep exactly one version row and update it.
        var versions = await _db.FileVersions
            .Where(v => v.FileNodeId == node.Id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = versions.FirstOrDefault();
        if (current is null)
        {
            current = new FileVersion
            {
                FileNodeId = node.Id,
                VersionNumber = 1,
                ContentHash = hash,
                StoragePath = storagePath,
                Size = node.Size,
                MimeType = node.MimeType,
                CreatedByUserId = caller.UserId,
                IsChunked = false
            };
            _db.FileVersions.Add(current);
        }

        current.VersionNumber = 1;
        current.Size = node.Size;
        current.ContentHash = hash;
        current.StoragePath = storagePath;
        current.MimeType = node.MimeType;
        current.IsChunked = false;

        // Release the chunk mappings the old content may have used (adoption can pick up a node
        // that was uploaded through the chunked pipeline). The whole-file blob is now the only
        // readable copy; leaving the mappings behind would leak chunk reference counts.
        await ReleaseChunkMappingsAsync([current.Id], cancellationToken).ConfigureAwait(false);

        // Remove any historical versions (a default file must never accumulate history).
        var extra = versions.Skip(1).ToList();
        if (extra.Count > 0)
        {
            await ReleaseChunkMappingsAsync(extra.Select(v => v.Id).ToList(), cancellationToken)
                .ConfigureAwait(false);
            _db.FileVersions.RemoveRange(extra);
        }

        node.CurrentVersion = 1;

        await SyncCursorHelper.AssignNextSequenceAsync(_db, node, node.OwnerId, _syncNotifier, cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await AfterWriteAsync(node, storagePath, caller, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Replaced default content for node {NodeId} '{FileName}' ({Bytes} bytes).",
            node.Id, node.Name, node.Size);

        return true;
    }

    /// <summary>
    /// Computes the content hash and guarantees the content-addressed blob exists exactly once.
    /// </summary>
    private async Task<(string Hash, long BytesWritten, bool Reused)> EnsureBlobAsync(
        string sourcePath,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        var hash = await DefaultUserContentStore.ComputeFileHashAsync(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        var storagePath = ContentHasher.GetFileStoragePath(hash);

        if (await _storageEngine.ExistsAsync(storagePath, cancellationToken).ConfigureAwait(false))
        {
            var length = await _storageEngine.GetLengthAsync(storagePath, cancellationToken).ConfigureAwait(false);
            if (length == expectedSize)
                return (hash, 0, true);
        }

        await using var source = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, System.IO.FileShare.Read,
            bufferSize: 81920, useAsync: true);

        await _storageEngine
            .WriteFromStreamAsync(storagePath, source, expectedSize, cancellationToken)
            .ConfigureAwait(false);

        return (hash, expectedSize, false);
    }

    private async Task ReleaseChunkMappingsAsync(
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken)
    {
        if (versionIds.Count == 0)
            return;

        var mappings = await _db.FileVersionChunks
            .Where(vc => versionIds.Contains(vc.FileVersionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var mapping in mappings)
            await ChunkReferenceHelper.DecrementAsync(_db, mapping.FileChunkId, cancellationToken)
                .ConfigureAwait(false);

        _db.FileVersionChunks.RemoveRange(mappings);
    }

    private async Task AfterWriteAsync(
        FileNode node,
        string storagePath,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        // Inline EXIF capture — the module host's event bus has no subscribers, so the upload path
        // does this inline; the import path must do the same to reach parity.
        if (_captureTimeService is not null)
        {
            await _captureTimeService.TryCaptureFromStorageAsync(
                node.Id, node.StoragePath, node.MimeType, cancellationToken).ConfigureAwait(false);
        }

        await _eventBus.PublishAsync(new FileUploadedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = node.Id,
            FileName = node.Name,
            Size = node.Size,
            MimeType = node.MimeType,
            ParentId = node.ParentId,
            UploadedByUserId = caller.UserId,
            StoragePath = storagePath
        }, caller, cancellationToken).ConfigureAwait(false);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = node.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, cancellationToken).ConfigureAwait(false);
    }
}
