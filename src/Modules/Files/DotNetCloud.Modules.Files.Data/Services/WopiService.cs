using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Core WOPI operations for Collabora Online integration.
/// Reads/writes file content via the storage engine and creates new versions on save.
/// </summary>
internal sealed class WopiService : IWopiService
{
    private readonly FilesDbContext _db;
    private readonly IDownloadService _downloadService;
    private readonly IFileStorageEngine _storageEngine;
    private readonly IPermissionService _permissionService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<WopiService> _logger;
    private readonly IFileVersioningSettingsProvider _versioningSettings;
    private readonly IUserDirectory? _userDirectory;

    public WopiService(
        FilesDbContext db,
        IDownloadService downloadService,
        IFileStorageEngine storageEngine,
        IPermissionService permissionService,
        IEventBus eventBus,
        ILogger<WopiService> logger,
        IFileVersioningSettingsProvider versioningSettings,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _downloadService = downloadService;
        _storageEngine = storageEngine;
        _permissionService = permissionService;
        _eventBus = eventBus;
        _logger = logger;
        _versioningSettings = versioningSettings;
        _userDirectory = userDirectory;
    }

    /// <inheritdoc />
    public async Task<WopiCheckFileInfoResponse?> CheckFileInfoAsync(Guid fileId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileId && !n.IsDeleted, cancellationToken);

        if (node is null)
            return null;

        var permission = await _permissionService.GetEffectivePermissionAsync(fileId, caller, cancellationToken);
        if (permission is null)
            return null;

        bool canWrite = permission >= SharePermission.ReadWrite;

        return new WopiCheckFileInfoResponse
        {
            BaseFileName = node.Name,
            OwnerId = node.OwnerId.ToString(),
            Size = node.Size,
            Version = $"{node.CurrentVersion}_{node.UpdatedAt.Ticks}",
            UserCanWrite = canWrite,
            SupportsUpdate = canWrite,
            SHA256 = node.ContentHash ?? string.Empty,
            LastModifiedTime = node.UpdatedAt.ToString("O"),
            UserId = caller.UserId.ToString(),
            UserFriendlyName = await ResolveUserFriendlyNameAsync(caller.UserId, cancellationToken),
            IsAnonymousUser = false
        };
    }

    /// <summary>
    /// Resolves the display name Collabora shows for the editing user ("UserFriendlyName").
    /// Falls back to the user's GUID when the user directory capability is unavailable or the
    /// user cannot be resolved — a directory lookup must never fail opening a document.
    /// </summary>
    private async Task<string> ResolveUserFriendlyNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_userDirectory is null)
            return userId.ToString();

        try
        {
            var names = await _userDirectory.GetDisplayNamesAsync([userId], cancellationToken);
            if (names.TryGetValue(userId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
                return TruncateForWopi(displayName);

            _logger.LogDebug("No display name resolved for user {UserId}; falling back to the user id", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve display name for user {UserId}; falling back to the user id", userId);
        }

        return userId.ToString();
    }

    /// <summary>
    /// WOPI caps <c>UserFriendlyName</c> at 128 characters; longer values are rejected by
    /// Collabora or truncated when rendered.
    /// </summary>
    private static string TruncateForWopi(string value) =>
        value.Length <= 128 ? value : value[..128];

    /// <inheritdoc />
    public async Task<(Stream Content, string MimeType, string FileName)?> GetFileAsync(Guid fileId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileId && !n.IsDeleted, cancellationToken);

        if (node is null)
            return null;

        await _permissionService.RequirePermissionAsync(fileId, caller, SharePermission.Read, cancellationToken);

        var stream = await _downloadService.DownloadCurrentAsync(fileId, caller, cancellationToken);
        var mimeType = node.MimeType ?? "application/octet-stream";

        _logger.LogInformation("WOPI GetFile: {FileId} ({FileName}), user {UserId}", fileId, node.Name, caller.UserId);

        return (stream, mimeType, node.Name);
    }

    /// <inheritdoc />
    public async Task<string> PutFileAsync(Guid fileId, Stream content, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .FirstOrDefaultAsync(n => n.Id == fileId && !n.IsDeleted, cancellationToken)
            ?? throw new Core.Errors.NotFoundException($"File {fileId} not found.");

        if (node.NodeType != FileNodeType.File)
            throw new Core.Errors.InvalidOperationException("Cannot save content to a folder.");

        await _permissionService.RequirePermissionAsync(fileId, caller, SharePermission.ReadWrite, cancellationToken);

        // Read all content and chunk it
        var chunks = await ContentHasher.ChunkAndHashAsync(content, ContentHasher.DefaultChunkSize, cancellationToken);

        var chunkHashes = chunks.Select(c => c.Hash).ToList();
        var manifestHash = ContentHasher.ComputeManifestHash(chunkHashes);
        long totalSize = chunks.Sum(c => (long)c.Data.Length);

        // Store new chunks (deduplication: skip existing) and track by hash for mapping
        var chunksByHash = new Dictionary<string, FileChunk>(chunkHashes.Count);
        foreach (var (hash, data) in chunks)
        {
            var existing = await _db.FileChunks
                .FirstOrDefaultAsync(c => c.ChunkHash == hash, cancellationToken);

            if (existing is not null)
            {
                await ChunkReferenceHelper.IncrementAsync(_db, existing.Id, cancellationToken);
                if (!ChunkReferenceHelper.IsInMemoryProvider(_db))
                    _db.Entry(existing).State = EntityState.Detached;

                // Re-fetch as no-tracking for the mapping dictionary.
                chunksByHash[hash] = existing;
            }
            else
            {
                var storagePath = ContentHasher.GetChunkStoragePath(hash);
                await _storageEngine.WriteChunkAsync(storagePath, data, cancellationToken);

                var newChunk = new FileChunk
                {
                    ChunkHash = hash,
                    Size = data.Length,
                    StoragePath = storagePath,
                    ReferenceCount = 1
                };
                _db.FileChunks.Add(newChunk);
                chunksByHash[hash] = newChunk;
            }
        }

        // Create new version
        var newVersionNumber = node.CurrentVersion + 1;
        var fileVersion = new FileVersion
        {
            FileNodeId = fileId,
            VersionNumber = newVersionNumber,
            Size = totalSize,
            ContentHash = manifestHash,
            StoragePath = ContentHasher.GetFileStoragePath(manifestHash),
            MimeType = node.MimeType,
            CreatedByUserId = caller.UserId
        };
        _db.FileVersions.Add(fileVersion);

        // Create version-chunk mappings using tracked entities
        for (int i = 0; i < chunkHashes.Count; i++)
        {
            _db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = fileVersion.Id,
                FileChunkId = chunksByHash[chunkHashes[i]].Id,
                SequenceIndex = i
            });
        }

        // Update node metadata
        node.CurrentVersion = newVersionNumber;
        node.Size = totalSize;
        node.ContentHash = manifestHash;
        node.StoragePath = ContentHasher.GetFileStoragePath(manifestHash);
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Apply the version policy immediately so a disabled or capped policy takes effect on save.
        var retentionOptions = await _versioningSettings.GetAsync(cancellationToken);
        var retention = await VersionRetentionEnforcer.ApplyAsync(_db, fileId, retentionOptions, cancellationToken);
        if (retention.PrunedCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Reap the blobs of any pruned whole-file versions that nothing else references.
        foreach (var prunedPath in retention.PrunedWholeFilePaths)
            await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(_db, _storageEngine, prunedPath, cancellationToken);

        _logger.LogInformation("WOPI PutFile: {FileId} ({FileName}) → v{Version}, {Size} bytes, user {UserId}",
            fileId, node.Name, newVersionNumber, totalSize, caller.UserId);

        // Publish event
        await _eventBus.PublishAsync(new FileUploadedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileId,
            FileName = node.Name,
            UploadedByUserId = caller.UserId,
            Size = totalSize,
            MimeType = node.MimeType,
            ParentId = node.ParentId,
            StoragePath = node.StoragePath
        }, caller);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = fileId.ToString(),
            Action = SearchIndexAction.Index
        }, caller);

        return node.UpdatedAt.ToString("O");
    }
}
