using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages chunked file uploads with content-addressable deduplication.
/// </summary>
internal sealed class ChunkedUploadService : IChunkedUploadService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly IQuotaService _quotaService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ChunkedUploadService> _logger;
    private readonly long _maxFileSizeBytes;
    private readonly FileSystemOptions _fileSystemOptions;

    public ChunkedUploadService(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        IQuotaService quotaService,
        IEventBus eventBus,
        ILogger<ChunkedUploadService> logger,
        IOptions<FileUploadOptions> uploadOptions,
        IOptions<FileSystemOptions> fileSystemOptions)
    {
        _db = db;
        _storageEngine = storageEngine;
        _quotaService = quotaService;
        _eventBus = eventBus;
        _logger = logger;
        _maxFileSizeBytes = uploadOptions.Value.MaxFileSizeBytes;
        _fileSystemOptions = fileSystemOptions.Value;
    }

    /// <inheritdoc />
    public async Task<UploadSessionDto> InitiateUploadAsync(InitiateUploadDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        FileService.ValidateFilenameCompatibility(dto.FileName, _fileSystemOptions);

        if (dto.TotalSize > _maxFileSizeBytes)
            throw new Core.Errors.ValidationException("TotalSize",
                $"File size {dto.TotalSize:N0} bytes exceeds the maximum allowed size of {_maxFileSizeBytes:N0} bytes.");

        // Ensure every uploader has a quota row before checking available space.
        await _quotaService.GetOrCreateQuotaAsync(caller.UserId, caller, cancellationToken);

        if (!await _quotaService.HasSufficientQuotaAsync(caller.UserId, dto.TotalSize, cancellationToken))
            throw new Core.Errors.ValidationException("Quota", "Insufficient storage quota for this upload.");

        // Identify which chunks already exist (dedup)
        var existingHashes = await _db.FileChunks
            .AsNoTracking()
            .Where(c => dto.ChunkHashes.Contains(c.ChunkHash))
            .Select(c => c.ChunkHash)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingHashes);
        var missingHashes = dto.ChunkHashes.Where(h => !existingSet.Contains(h)).ToList();

        var session = new ChunkedUploadSession
        {
            TargetParentId = dto.ParentId,
            FileName = dto.FileName,
            TotalSize = dto.TotalSize,
            MimeType = dto.MimeType,
            TotalChunks = dto.ChunkHashes.Count,
            ReceivedChunks = existingHashes.Count,
            ChunkManifest = JsonSerializer.Serialize(dto.ChunkHashes),
            ChunkSizesManifest = dto.ChunkSizes is { Count: > 0 }
                ? JsonSerializer.Serialize(dto.ChunkSizes)
                : null,
            PosixMode = dto.PosixMode,
            PosixOwnerHint = dto.PosixOwnerHint,
            UserId = caller.UserId,
            Status = UploadSessionStatus.InProgress
        };

        _db.UploadSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Upload session {SessionId} initiated for '{FileName}' by {UserId}. {Existing}/{Total} chunks already exist.",
            session.Id, dto.FileName, caller.UserId, existingHashes.Count, dto.ChunkHashes.Count);

        return new UploadSessionDto
        {
            SessionId = session.Id,
            ExistingChunks = existingHashes,
            MissingChunks = missingHashes,
            ExpiresAt = session.ExpiresAt
        };
    }

    /// <inheritdoc />
    public async Task UploadChunkAsync(Guid sessionId, string chunkHash, ReadOnlyMemory<byte> data, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkHash);

        var session = await GetActiveSessionAsync(sessionId, caller, cancellationToken);

        var manifest = JsonSerializer.Deserialize<List<string>>(session.ChunkManifest)!;
        if (!manifest.Contains(chunkHash))
            throw new Core.Errors.ValidationException("ChunkHash", "Chunk hash not found in the session manifest.");

        // Verify data hash
        var computedHash = ContentHasher.ComputeHash(data.Span);
        if (!string.Equals(computedHash, chunkHash, StringComparison.OrdinalIgnoreCase))
            throw new Core.Errors.ValidationException("ChunkHash", "Uploaded data does not match the declared chunk hash.");

        // Check if chunk already exists (concurrent upload or dedup)
        var existingChunk = await _db.FileChunks
            .FirstOrDefaultAsync(c => c.ChunkHash == chunkHash, cancellationToken);

        if (existingChunk is null)
        {
            var storagePath = ContentHasher.GetChunkStoragePath(chunkHash);
            await _storageEngine.WriteChunkAsync(storagePath, data, cancellationToken);

            _db.FileChunks.Add(new FileChunk
            {
                ChunkHash = chunkHash,
                Size = data.Length,
                StoragePath = storagePath,
                ReferenceCount = 0 // Will be incremented on CompleteUpload
            });
        }

        session.ReceivedChunks++;
        session.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Chunk {ChunkHash} uploaded for session {SessionId}. {Received}/{Total}",
            chunkHash, sessionId, session.ReceivedChunks, session.TotalChunks);
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> CompleteUploadAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var session = await GetActiveSessionAsync(sessionId, caller, cancellationToken);
        var manifest = JsonSerializer.Deserialize<List<string>>(session.ChunkManifest)!;

        // Verify all chunks are available
        var availableHashes = await _db.FileChunks
            .AsNoTracking()
            .Where(c => manifest.Contains(c.ChunkHash))
            .Select(c => c.ChunkHash)
            .ToListAsync(cancellationToken);

        if (availableHashes.Count != manifest.Count)
        {
            var missing = manifest.Except(availableHashes).ToList();
            throw new Core.Errors.ValidationException("Chunks", $"Missing {missing.Count} chunk(s). Upload them before completing.");
        }

        // Create or update the file node
        var contentHash = ContentHasher.ComputeManifestHash(manifest);
        var storagePath = ContentHasher.GetFileStoragePath(contentHash);

        FileNode fileNode;
        long quotaDelta;
        if (session.TargetFileNodeId.HasValue)
        {
            fileNode = await _db.FileNodes.FindAsync([session.TargetFileNodeId.Value], cancellationToken)
                ?? throw new NotFoundException("FileNode", session.TargetFileNodeId.Value);
            quotaDelta = session.TotalSize - fileNode.Size; // size delta for file update
            fileNode.Size = session.TotalSize;
            fileNode.ContentHash = contentHash;
            fileNode.StoragePath = storagePath;
            fileNode.CurrentVersion++;
            fileNode.UpdatedAt = DateTime.UtcNow;
            // Preserve existing POSIX metadata when Windows client re-uploads (sends null)
            fileNode.PosixMode = session.PosixMode ?? fileNode.PosixMode;
            fileNode.PosixOwnerHint = session.PosixOwnerHint ?? fileNode.PosixOwnerHint;
        }
        else
        {
            // Compute parent path for new file
            string parentPath = "";
            int parentDepth = -1;

            if (session.TargetParentId.HasValue)
            {
                var parent = await _db.FileNodes.FindAsync([session.TargetParentId.Value], cancellationToken);
                if (parent is not null)
                {
                    parentPath = parent.MaterializedPath;
                    parentDepth = parent.Depth;
                }
            }

            // Guard against case-insensitive name collisions before creating the node.
            if (_fileSystemOptions.EnforceCaseInsensitiveUniqueness)
            {
                IQueryable<FileNode> siblingQuery = session.TargetParentId.HasValue
                    ? _db.FileNodes.Where(n => n.ParentId == session.TargetParentId.Value)
                    : _db.FileNodes.Where(n => n.OwnerId == caller.UserId && n.ParentId == null);

                var conflictingName = await siblingQuery
                    .Where(n => n.Name.ToLower() == session.FileName.ToLower() && n.Name != session.FileName)
                    .Select(n => n.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (conflictingName is not null)
                    throw new NameConflictException(conflictingName);
            }

            fileNode = new FileNode
            {
                Name = session.FileName,
                NodeType = FileNodeType.File,
                MimeType = session.MimeType,
                Size = session.TotalSize,
                ParentId = session.TargetParentId,
                OwnerId = caller.UserId,
                ContentHash = contentHash,
                StoragePath = storagePath,
                Depth = parentDepth + 1,
                PosixMode = session.PosixMode,
                PosixOwnerHint = session.PosixOwnerHint
            };
            fileNode.MaterializedPath = string.IsNullOrEmpty(parentPath)
                ? $"/{fileNode.Id}"
                : $"{parentPath}/{fileNode.Id}";

            _db.FileNodes.Add(fileNode);
            quotaDelta = session.TotalSize; // full size for new file
        }

        // Create file version
        var version = new FileVersion
        {
            FileNodeId = fileNode.Id,
            VersionNumber = fileNode.CurrentVersion,
            Size = session.TotalSize,
            ContentHash = contentHash,
            StoragePath = storagePath,
            MimeType = session.MimeType,
            CreatedByUserId = caller.UserId,
            PosixMode = fileNode.PosixMode
        };
        _db.FileVersions.Add(version);

        // Create version-chunk mappings and increment refcounts
        var chunkSizes = session.ChunkSizesManifest is not null
            ? JsonSerializer.Deserialize<List<int>>(session.ChunkSizesManifest)!
            : null;

        long byteOffset = 0;
        for (var i = 0; i < manifest.Count; i++)
        {
            var chunk = await _db.FileChunks
                .FirstAsync(c => c.ChunkHash == manifest[i], cancellationToken);

            var chunkSize = chunkSizes?[i] ?? chunk.Size;

            _db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = version.Id,
                FileChunkId = chunk.Id,
                SequenceIndex = i,
                Offset = byteOffset,
                ChunkSize = chunkSize
            });

            byteOffset += chunkSize;
            chunk.ReferenceCount++;
            chunk.LastReferencedAt = DateTime.UtcNow;
        }

        // Mark session as completed
        session.Status = UploadSessionStatus.Completed;
        session.TargetFileNodeId = fileNode.Id;
        session.UpdatedAt = DateTime.UtcNow;

        await SyncCursorHelper.AssignNextSequenceAsync(_db, fileNode, caller.UserId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Update quota usage in real time
        if (quotaDelta != 0)
            await _quotaService.AdjustUsedBytesAsync(caller.UserId, quotaDelta, cancellationToken);

        await _eventBus.PublishAsync(new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNode.Id,
            FileName = fileNode.Name,
            Size = fileNode.Size,
            MimeType = fileNode.MimeType,
            ParentId = fileNode.ParentId,
            UploadedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Upload session {SessionId} completed. File {FileNodeId} '{FileName}' created/updated.",
            sessionId, fileNode.Id, fileNode.Name);

        return new FileNodeDto
        {
            Id = fileNode.Id,
            Name = fileNode.Name,
            NodeType = fileNode.NodeType.ToString(),
            MimeType = fileNode.MimeType,
            Size = fileNode.Size,
            ParentId = fileNode.ParentId,
            OwnerId = fileNode.OwnerId,
            CurrentVersion = fileNode.CurrentVersion,
            IsFavorite = fileNode.IsFavorite,
            ContentHash = fileNode.ContentHash,
            CreatedAt = fileNode.CreatedAt,
            UpdatedAt = fileNode.UpdatedAt,
            PosixMode = fileNode.PosixMode,
            PosixOwnerHint = fileNode.PosixOwnerHint
        };
    }

    /// <inheritdoc />
    public async Task CancelUploadAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var session = await GetActiveSessionAsync(sessionId, caller, cancellationToken);

        session.Status = UploadSessionStatus.Cancelled;
        session.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Upload session {SessionId} cancelled by {UserId}", sessionId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<UploadSessionDto?> GetSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var session = await _db.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || (session.UserId != caller.UserId && caller.Type != CallerType.System))
            return null;

        var manifest = JsonSerializer.Deserialize<List<string>>(session.ChunkManifest)!;
        var existingHashes = await _db.FileChunks
            .AsNoTracking()
            .Where(c => manifest.Contains(c.ChunkHash))
            .Select(c => c.ChunkHash)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingHashes);

        return new UploadSessionDto
        {
            SessionId = session.Id,
            ExistingChunks = existingHashes,
            MissingChunks = manifest.Where(h => !existingSet.Contains(h)).ToList(),
            ExpiresAt = session.ExpiresAt
        };
    }

    private async Task<ChunkedUploadSession> GetActiveSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken)
    {
        var session = await _db.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException("UploadSession", sessionId);

        if (session.UserId != caller.UserId && caller.Type != CallerType.System)
            throw new ForbiddenException("You do not own this upload session.");

        if (session.Status != UploadSessionStatus.InProgress)
            throw new Core.Errors.InvalidOperationException("Upload session is not active.");

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.Status = UploadSessionStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            throw new Core.Errors.InvalidOperationException("Upload session has expired.");
        }

        return session;
    }
}
