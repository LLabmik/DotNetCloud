using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Bidirectional sync engine using <see cref="FileSystemWatcher"/> for instant detection
/// and a periodic full scan as a safety net.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    private readonly IDotNetCloudApiClient _api;
    private readonly ITokenStore _tokenStore;
    private readonly IChunkedTransferClient _transfer;
    private readonly IConflictResolver _conflictResolver;
    private readonly ILocalStateDb _stateDb;
    private readonly ISelectiveSyncConfig _selectiveSync;
    private readonly ISyncIgnoreParser _syncIgnore;
    private readonly ILockedFileReader _lockedFileReader;
    private readonly ILogger<SyncEngine> _logger;

    /// <summary>
    /// Delay between Tier 2 retry attempts for locked files.
    /// Exposed as internal for test overrides.
    /// </summary>
    internal TimeSpan Tier2RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private Task? _periodicScanTask;
    private SyncContext? _activeContext;
    private SyncState _state = SyncState.Idle;
    private string? _lastError;
    private bool _paused;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    /// <inheritdoc/>
    public event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;

    /// <inheritdoc/>
    public event EventHandler<FileTransferProgressEventArgs>? FileTransferProgress;

    /// <inheritdoc/>
    public event EventHandler<FileTransferCompleteEventArgs>? FileTransferComplete;

    /// <summary>Initializes a new <see cref="SyncEngine"/>.</summary>
    public SyncEngine(
        IDotNetCloudApiClient api,
        ITokenStore tokenStore,
        IChunkedTransferClient transfer,
        IConflictResolver conflictResolver,
        ILocalStateDb stateDb,
        ISelectiveSyncConfig selectiveSync,
        ISyncIgnoreParser syncIgnore,
        ILockedFileReader lockedFileReader,
        ILogger<SyncEngine> logger)
    {
        _api = api;
        _tokenStore = tokenStore;
        _transfer = transfer;
        _conflictResolver = conflictResolver;
        _stateDb = stateDb;
        _selectiveSync = selectiveSync;
        _syncIgnore = syncIgnore;
        _lockedFileReader = lockedFileReader;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        _activeContext = context;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _stateDb.InitializeAsync(context.StateDatabasePath, cancellationToken);

        // Clean up stale upload sessions (created > 48 h ago; server TTL is 24 h).
        await _stateDb.DeleteStaleActiveUploadSessionsAsync(
            context.StateDatabasePath, DateTime.UtcNow.AddHours(-48), cancellationToken);

        _syncIgnore.Initialize(context.LocalFolderPath);

        _watcher = new FileSystemWatcher(context.LocalFolderPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                           | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        _watcher.Created += OnFileSystemChanged;
        _watcher.Changed += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemRenamed;
        _watcher.EnableRaisingEvents = true;

        _periodicScanTask = RunPeriodicScanAsync(context, _cts.Token);

        if (_stateDb.WasRecentlyReset(context.StateDatabasePath))
            _logger.LogWarning("Local state DB was reset due to corruption for context {ContextId}. A full resync will be performed.", context.Id);

        _logger.LogInformation("Sync engine started for context {ContextId} ({LocalFolder}).",
            context.Id, context.LocalFolderPath);

        SetState(SyncState.Idle, context);
    }

    /// <inheritdoc/>
    public async Task SyncAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        if (_paused)
        {
            _logger.LogDebug("Sync requested but engine is paused for context {ContextId}.", context.Id);
            return;
        }

        await _syncLock.WaitAsync(cancellationToken);
        var syncTimer = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Sync pass starting for context {ContextId}.", context.Id);

            SetState(SyncState.Syncing, context);
            await RefreshAccessTokenAsync(context, cancellationToken);
            var remoteChangesApplied = await ApplyRemoteChangesAsync(context, cancellationToken);
            var localOperationsApplied = await ApplyLocalChangesAsync(context, cancellationToken);
            await _stateDb.UpdateCheckpointAsync(context.StateDatabasePath, DateTime.UtcNow, cancellationToken);
            await _stateDb.CheckpointWalAsync(context.StateDatabasePath, cancellationToken);
            SetState(SyncState.Idle, context);
            syncTimer.Stop();

            _logger.LogInformation(
                "Sync pass complete for context {ContextId}: DurationMs={DurationMs}, FileCount={FileCount}.",
                context.Id,
                syncTimer.ElapsedMilliseconds,
                remoteChangesApplied + localOperationsApplied);
        }
        catch (OperationCanceledException)
        {
            syncTimer.Stop();
            _logger.LogInformation("Sync cancelled for context {ContextId}.", context.Id);
            SetState(SyncState.Idle, context);
        }
        catch (Exception ex)
        {
            syncTimer.Stop();
            _logger.LogError(ex, "Sync error for context {ContextId}.", context.Id);
            _lastError = ex.Message;
            SetState(SyncState.Error, context);
        }
        finally
        {
            _lockedFileReader.ReleaseSnapshot();
            _syncLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<SyncStatus> GetStatusAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        var pendingOps = await _stateDb.GetPendingOperationCountAsync(context.StateDatabasePath, cancellationToken);
        var checkpoint = await _stateDb.GetCheckpointAsync(context.StateDatabasePath, cancellationToken);
        return new SyncStatus
        {
            State = _state,
            PendingUploads = pendingOps.Uploads,
            PendingDownloads = pendingOps.Downloads,
            Conflicts = pendingOps.Conflicts,
            LastSyncedAt = checkpoint,
            LastError = _lastError,
        };
    }

    /// <inheritdoc/>
    public Task PauseAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        _paused = true;
        if (_watcher is not null)
            _watcher.EnableRaisingEvents = false;
        SetState(SyncState.Paused, context);
        _logger.LogInformation("Sync paused for context {ContextId}.", context.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResumeAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        _paused = false;
        if (_watcher is not null)
            _watcher.EnableRaisingEvents = true;
        SetState(SyncState.Idle, context);
        _logger.LogInformation("Sync resumed for context {ContextId}.", context.Id);

        // Trigger a sync pass to catch up on any changes that occurred while paused
        _ = Task.Run(() => SyncAsync(context, _cts?.Token ?? default), _cts?.Token ?? default);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_periodicScanTask is not null)
        {
            try { await _periodicScanTask; }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("Sync engine stopped.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _syncLock.Dispose();
        (_lockedFileReader as IDisposable)?.Dispose();
    }

    // ── Private sync logic ──────────────────────────────────────────────────

    private async Task<int> ApplyRemoteChangesAsync(SyncContext context, CancellationToken cancellationToken)
    {
        // Load stored cursor (null = never synced → server will return full history)
        var cursor = await _stateDb.GetSyncCursorAsync(context.StateDatabasePath, cancellationToken);
        var appliedChanges = 0;

        // Build nodeId → relative-path map once (shared across all pages)
        var tree = await _api.GetFolderTreeAsync(null, cancellationToken);
        var pathMap = new Dictionary<Guid, string>();
        BuildPathMap(tree, "", pathMap);

        // Paginated cursor loop — keeps fetching until HasMore == false
        var hasMore = true;
        while (hasMore)
        {
            var page = await _api.GetChangesSinceAsync(cursor, limit: 500, cancellationToken);
            _logger.LogDebug("Fetched {Count} remote changes (cursor={Cursor}, hasMore={HasMore}).",
                page.Changes.Count, cursor ?? "(none)", page.HasMore);

            // Ensure directories for folder changes before processing file changes
            foreach (var change in page.Changes.Where(c => c.NodeType == "Folder" && !c.IsDeleted))
            {
                if (pathMap.TryGetValue(change.NodeId, out var relPath))
                {
                    var dirPath = Path.Combine(context.LocalFolderPath, relPath);
                    Directory.CreateDirectory(dirPath);
                    _logger.LogDebug("Ensured directory {Path} for folder node {NodeId}.", dirPath, change.NodeId);
                }
            }

            foreach (var change in page.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var localPath = await ResolveLocalPathAsync(context, change.NodeId, change.Name, pathMap, cancellationToken);

                if (!_selectiveSync.IsIncluded(context.Id, localPath))
                    continue;

                var relativePathForIgnore = Path.GetRelativePath(context.LocalFolderPath, localPath);
                if (_syncIgnore.IsIgnored(relativePathForIgnore))
                {
                    _logger.LogDebug("Skipping ignored remote change {RelPath} for context {ContextId}.",
                        relativePathForIgnore, context.Id);
                    continue;
                }

                if (change.IsDeleted)
                {
                    await HandleRemoteDeletionAsync(context, localPath, change.NodeId, cancellationToken);
                    appliedChanges++;
                }
                else if (change.NodeType == "File")
                {
                    await HandleRemoteUpdateAsync(context, localPath, change, cancellationToken);
                    appliedChanges++;
                }
            }

            // Persist cursor after each page for crash resilience — if interrupted mid-sync
            // the next run resumes from the last successfully processed page.
            if (page.NextCursor is not null)
            {
                cursor = page.NextCursor;
                await _stateDb.UpdateSyncCursorAsync(context.StateDatabasePath, cursor, cancellationToken);
            }

            hasMore = page.HasMore;
        }

        return appliedChanges;
    }

    private async Task HandleRemoteDeletionAsync(SyncContext context, string localPath, Guid nodeId, CancellationToken cancellationToken)
    {
        if (File.Exists(localPath))
        {
            var localRecord = await _stateDb.GetFileRecordAsync(context.StateDatabasePath, localPath, cancellationToken);
            if (localRecord is not null && IsLocallyModified(localRecord, localPath))
            {
                // Local file was modified after deletion on server — keep it
                _logger.LogWarning("Remote deleted {NodeId} but local file was modified. Keeping local.", nodeId);
                await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                    new PendingUpload { LocalPath = localPath, NodeId = nodeId }, cancellationToken);
            }
            else
            {
                File.Delete(localPath);
                await _stateDb.RemoveFileRecordAsync(context.StateDatabasePath, localPath, cancellationToken);
                _logger.LogDebug("Deleted local file {Path} (remote deletion).", localPath);
            }
        }
    }

    private async Task HandleRemoteUpdateAsync(SyncContext context, string localPath, Api.SyncChangeResponse change, CancellationToken cancellationToken)
    {
        var localRecord = await _stateDb.GetFileRecordAsync(context.StateDatabasePath, localPath, cancellationToken);

        if (localRecord is not null && IsLocallyModified(localRecord, localPath))
        {
            // Both local and remote changed — run auto-resolution pipeline.
            string? localContentHash = null;
            try
            {
                localContentHash = await ComputeFileHashAsync(localPath, cancellationToken);
            }
            catch { /* non-critical; resolver falls through to conflict copy if null */ }

            var outcome = await _conflictResolver.ResolveAsync(new ConflictInfo
            {
                LocalPath = localPath,
                NodeId = change.NodeId,
                RemoteUpdatedAt = change.UpdatedAt,
                RemoteContentHash = change.ContentHash,
                StateDatabasePath = context.StateDatabasePath,
                LocalContentHash = localContentHash,
                BaseContentHash = localRecord.ContentHash,
                LocalModifiedAt = File.Exists(localPath) ? File.GetLastWriteTimeUtc(localPath) : default,
                LocalUserId = context.UserId,
            }, cancellationToken);

            switch (outcome)
            {
                case Conflict.ConflictResolutionOutcome.AutoResolvedServerWins:
                    await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                        new PendingDownload { LocalPath = localPath, NodeId = change.NodeId }, cancellationToken);
                    break;

                case Conflict.ConflictResolutionOutcome.AutoResolvedIdentical:
                    // Both sides identical — update file record to match synced state.
                    localRecord.ContentHash = change.ContentHash;
                    localRecord.SyncStateTag = "Synced";
                    localRecord.LastSyncedAt = DateTime.UtcNow;
                    await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, localRecord, cancellationToken);
                    break;

                // AutoResolvedLocalWins: local file kept, will be re-queued for upload on next scan.
                // ConflictCopyCreated: server version will be downloaded on next sync cycle.
            }
        }
        else if (localRecord is null || localRecord.ContentHash != change.ContentHash)
        {
            // Download the remote version
            await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                new PendingDownload { LocalPath = localPath, NodeId = change.NodeId }, cancellationToken);
        }
    }

    private const int MaxOperationRetries = 10;

    private async Task<int> ApplyLocalChangesAsync(SyncContext context, CancellationToken cancellationToken)
    {
        var pendingOps = await _stateDb.GetPendingOperationsAsync(context.StateDatabasePath, cancellationToken);
        var completedOperations = 0;

        foreach (var op in pendingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ExecutePendingOperationAsync(context, op, cancellationToken);
                await _stateDb.RemoveOperationAsync(context.StateDatabasePath, op.Id, cancellationToken);
                completedOperations++;
            }
            catch (NameConflictException ncEx)
            {
                // Issue #41: case-sensitivity conflict — move to failed immediately, no retry.
                _logger.LogWarning(
                    "Case-sensitivity conflict for '{FileName}': {Message}. Operation {OpId} moved to failed queue.",
                    op is PendingUpload u ? Path.GetFileName(u.LocalPath) : string.Empty,
                    ncEx.Message, op.Id);
                await _stateDb.MoveToFailedAsync(context.StateDatabasePath, op, ncEx.Message, cancellationToken);
            }
            catch (LockedFileException lockEx)
            {
                // Locked files are not sync failures — defer without consuming the retry budget.
                _logger.LogWarning(
                    "Skipping {Path} — file is locked by another process. Will retry automatically in 2 minutes. (Operation {OpId})",
                    lockEx.FilePath, op.Id);

                // Update SyncStateTag to "Deferred" if there is an existing file record.
                var deferRecord = await _stateDb.GetFileRecordAsync(
                    context.StateDatabasePath, lockEx.FilePath, cancellationToken);
                if (deferRecord is not null)
                {
                    deferRecord.SyncStateTag = "Deferred";
                    await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, deferRecord, cancellationToken);
                }

                // Schedule a short retry without incrementing RetryCount.
                await _stateDb.UpdateOperationRetryAsync(
                    context.StateDatabasePath, op.Id, op.RetryCount,
                    DateTime.UtcNow.AddMinutes(2), lockEx.Message, cancellationToken);
            }
            catch (Exception ex)
            {
                var newRetryCount = op.RetryCount + 1;

                if (newRetryCount >= MaxOperationRetries)
                {
                    _logger.LogError(ex,
                        "Operation {OpId} ({Type}) permanently failed after {RetryCount} attempts. Moving to failed queue.",
                        op.Id, op.OperationType, newRetryCount);
                    await _stateDb.MoveToFailedAsync(context.StateDatabasePath, op, ex.Message, cancellationToken);
                }
                else
                {
                    var nextRetryAt = ComputeNextRetryAt(newRetryCount);
                    _logger.LogWarning(ex,
                        "Operation {OpId} ({Type}) failed (attempt {RetryCount}/{MaxRetries}). Next retry at {NextRetryAt}.",
                        op.Id, op.OperationType, newRetryCount, MaxOperationRetries, nextRetryAt);
                    await _stateDb.UpdateOperationRetryAsync(
                        context.StateDatabasePath, op.Id, newRetryCount, nextRetryAt, ex.Message, cancellationToken);
                }
            }
        }

        return completedOperations;
    }

    private static DateTime ComputeNextRetryAt(int retryCount) => retryCount switch
    {
        1 => DateTime.UtcNow.AddMinutes(1),
        2 => DateTime.UtcNow.AddMinutes(5),
        3 => DateTime.UtcNow.AddMinutes(15),
        4 => DateTime.UtcNow.AddHours(1),
        _ => DateTime.UtcNow.AddHours(6),
    };

    private async Task ExecutePendingOperationAsync(SyncContext context, PendingOperationRecord op, CancellationToken cancellationToken)
    {
        if (op is PendingUpload upload)
        {
            // Issue #40: idempotency check — skip upload if server already has this version.
            if (upload.NodeId.HasValue)
            {
                FileNodeResponse? serverNode = null;
                try { serverNode = await _api.GetNodeAsync(upload.NodeId.Value, cancellationToken); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Could not fetch node {NodeId} for idempotency check; proceeding with upload.", upload.NodeId.Value);
                }

                if (serverNode?.ContentHash is not null)
                {
                    var localHash = await ComputeFileHashAsync(upload.LocalPath, cancellationToken);
                    if (string.Equals(serverNode.ContentHash, localHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Skipping upload of {LocalPath} — server already has this version (hash match).",
                            upload.LocalPath);
                        await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
                        {
                            LocalPath = upload.LocalPath,
                            NodeId = upload.NodeId.Value,
                            ContentHash = localHash,
                            LastSyncedAt = DateTime.UtcNow,
                            LocalModifiedAt = File.GetLastWriteTimeUtc(upload.LocalPath),
                        }, cancellationToken);
                        return;
                    }
                }
            }
            var fileName = Path.GetFileName(upload.LocalPath);
            var uploadProgress = new Progress<TransferProgress>(p =>
                FileTransferProgress?.Invoke(this, new FileTransferProgressEventArgs
                {
                    FileName = fileName,
                    Direction = "upload",
                    Progress = p,
                }));

            await using var fileStream = await OpenFileForSyncAsync(upload.LocalPath, cancellationToken);
            var nodeId = await _transfer.UploadAsync(
                upload.NodeId, upload.LocalPath, fileStream, uploadProgress, cancellationToken,
                context.StateDatabasePath);

            FileTransferComplete?.Invoke(this, new FileTransferCompleteEventArgs
            {
                FileName = fileName,
                Direction = "upload",
                TotalBytes = fileStream.Length,
                TotalChunks = 0,
            });

            var hash = await ComputeFileHashAsync(upload.LocalPath, cancellationToken);
            await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
            {
                LocalPath = upload.LocalPath,
                NodeId = nodeId,
                ContentHash = hash,
                LastSyncedAt = DateTime.UtcNow,
                LocalModifiedAt = File.GetLastWriteTimeUtc(upload.LocalPath),
            }, cancellationToken);
        }
        else if (op is PendingDownload download)
        {            var dlRelPath = Path.GetRelativePath(context.LocalFolderPath, download.LocalPath);
            if (_syncIgnore.IsIgnored(dlRelPath))
            {
                _logger.LogDebug("Skipping download of ignored file {RelPath}.", dlRelPath);
                return;
            }

            var fileName = Path.GetFileName(download.LocalPath);
            var downloadProgress = new Progress<TransferProgress>(p =>
                FileTransferProgress?.Invoke(this, new FileTransferProgressEventArgs
                {
                    FileName = fileName,
                    Direction = "download",
                    Progress = p,
                }));

            using var stream = await _transfer.DownloadAsync(download.NodeId, downloadProgress, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(download.LocalPath)!);
            await using var output = File.Create(download.LocalPath);
            await stream.CopyToAsync(output, cancellationToken);

            FileTransferComplete?.Invoke(this, new FileTransferCompleteEventArgs
            {
                FileName = fileName,
                Direction = "download",
                TotalBytes = stream.Length,
                TotalChunks = 0,
            });

            var hash = await ComputeFileHashAsync(download.LocalPath, cancellationToken);
            await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
            {
                LocalPath = download.LocalPath,
                NodeId = download.NodeId,
                ContentHash = hash,
                LastSyncedAt = DateTime.UtcNow,
                LocalModifiedAt = File.GetLastWriteTimeUtc(download.LocalPath),
            }, cancellationToken);
        }
    }

    private async Task RefreshAccessTokenAsync(SyncContext context, CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.LoadAsync(context.AccountKey, cancellationToken);
        if (tokens is null)
        {
            _logger.LogWarning("No tokens found for context {ContextId}.", context.Id);
            return;
        }

        _logger.LogInformation(
            "Token state for context {ContextId}: IsExpired={IsExpired}, CanRefresh={CanRefresh}, ExpiresAt={ExpiresAt}.",
            context.Id, tokens.IsExpired, tokens.CanRefresh, tokens.ExpiresAt);

        if (tokens.IsExpired && tokens.CanRefresh)
        {
            _logger.LogInformation("Refreshing expired access token for context {ContextId}.", context.Id);
            var refreshed = await _api.RefreshTokenAsync(tokens.RefreshToken!, OAuthConstants.ClientId, cancellationToken);
            tokens = new TokenInfo
            {
                AccessToken = refreshed.AccessToken,
                RefreshToken = refreshed.RefreshToken ?? tokens.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn),
            };
            await _tokenStore.SaveAsync(context.AccountKey, tokens, cancellationToken);
            _logger.LogInformation("Access token refreshed successfully for context {ContextId}. New expiry: {ExpiresAt}.",
                context.Id, tokens.ExpiresAt);
        }

        _api.AccessToken = tokens.AccessToken;
    }

    private async Task<string> ResolveLocalPathAsync(SyncContext context, Guid nodeId, string name, Dictionary<Guid, string> pathMap, CancellationToken cancellationToken)
    {
        var existing = await _stateDb.GetFileRecordByNodeIdAsync(context.StateDatabasePath, nodeId, cancellationToken);
        if (existing is not null)
            return existing.LocalPath;

        // Use the tree-derived path map when available
        if (pathMap.TryGetValue(nodeId, out var relativePath))
            return Path.Combine(context.LocalFolderPath, relativePath);

        return Path.Combine(context.LocalFolderPath, name);
    }

    private static void BuildPathMap(SyncTreeNodeResponse node, string parentPath, Dictionary<Guid, string> map)
    {
        // The virtual root (NodeId == Guid.Empty, Name == "/") has no path segment
        var currentPath = node.NodeId == Guid.Empty
            ? parentPath
            : string.IsNullOrEmpty(parentPath) ? node.Name : Path.Combine(parentPath, node.Name);

        if (node.NodeId != Guid.Empty)
            map[node.NodeId] = currentPath;

        foreach (var child in node.Children)
            BuildPathMap(child, currentPath, map);
    }

    private static bool IsLocallyModified(LocalFileRecord record, string localPath)
    {
        if (!File.Exists(localPath)) return false;
        var localModified = File.GetLastWriteTimeUtc(localPath);
        return localModified > record.LastSyncedAt;
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        // Use shared-read mode so files open in other apps (e.g. Office) don't block hash computation.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Opens a file for reading using a 4-tier strategy for handling files locked by other processes.
    /// </summary>
    /// <param name="path">Full path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A readable stream for the file.</returns>
    /// <exception cref="LockedFileException">Thrown when the file is still inaccessible after all tiers.</exception>
    private async Task<Stream> OpenFileForSyncAsync(string path, CancellationToken cancellationToken)
    {
        // HResult for Win32 ERROR_SHARING_VIOLATION (0x80070020).
        const int SharingViolationHResult = unchecked((int)0x80070020);

        // Tier 1: shared-read open (FileShare.ReadWrite | FileShare.Delete).
        // Fixes the common case where apps such as modern Office hold ReadWrite+Delete share
        // but still allow concurrent readers.
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException ex) when (ex.HResult == SharingViolationHResult)
        {
            _logger.LogDebug("Tier 1 sharing violation on {Path}; attempting Tier 2 retry.", path);
        }

        // Tier 2: retry with backoff for transient locks (antivirus scanners, indexers).
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await Task.Delay(Tier2RetryDelay, cancellationToken);
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
            }
            catch (IOException ex) when (ex.HResult == SharingViolationHResult)
            {
                _logger.LogDebug("Tier 2 attempt {Attempt}/3 still locked on {Path}.", attempt, path);
            }
        }

        // Tier 3: VSS shadow copy (Windows-only; returns null on Linux/macOS or if VSS fails).
        var vssStream = await _lockedFileReader.TryReadLockedFileAsync(path, cancellationToken);
        if (vssStream is not null)
        {
            _logger.LogInformation("Opened {Path} via VSS shadow copy (Tier 3).", path);
            return vssStream;
        }

        // Tier 4: defer — all strategies exhausted.
        throw new LockedFileException(path);
    }

    // ── FileSystemWatcher handlers ──────────────────────────────────────────

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (_activeContext is null || _paused) return;

        // Pre-filter: skip events for known-ignored files to reduce unnecessary sync passes.
        var relativePath = Path.GetRelativePath(_activeContext.LocalFolderPath, e.FullPath);
        if (_syncIgnore.IsIgnored(relativePath))
            return;

        _logger.LogInformation(
            "FileSystemWatcher trigger: ChangeType={ChangeType}, Path={Path}.",
            e.ChangeType,
            e.FullPath);
        _ = Task.Run(() => SyncAsync(_activeContext, _cts?.Token ?? default));
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (_activeContext is null || _paused) return;

        // Pre-filter: skip events for known-ignored files.
        var newRelPath = Path.GetRelativePath(_activeContext.LocalFolderPath, e.FullPath);
        if (_syncIgnore.IsIgnored(newRelPath))
            return;

        _logger.LogInformation(
            "FileSystemWatcher trigger: ChangeType=Renamed, OldPath={OldPath}, NewPath={NewPath}.",
            e.OldFullPath,
            e.FullPath);
        _ = Task.Run(() => SyncAsync(_activeContext, _cts?.Token ?? default));
    }

    // ── Periodic full scan ──────────────────────────────────────────────────

    private async Task RunPeriodicScanAsync(SyncContext context, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(context.FullScanInterval, cancellationToken);
                _logger.LogDebug("Periodic full scan triggered for context {ContextId}.", context.Id);
                await SyncAsync(context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic scan failed for context {ContextId}.", context.Id);
            }
        }
    }

    // ── State helpers ───────────────────────────────────────────────────────

    private void SetState(SyncState state, SyncContext context)
    {
        _state = state;
        if (state != SyncState.Error)
            _lastError = null;

        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            Context = context,
            Status = new SyncStatus { State = state, LastError = _lastError },
        });
    }
}
