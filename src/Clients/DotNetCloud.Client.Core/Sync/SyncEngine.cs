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
using System.Runtime.Versioning;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Bidirectional sync engine using <see cref="FileSystemWatcher"/> for instant detection
/// and a periodic full scan as a safety net.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    private const int DiskFullHResult = unchecked((int)0x80070070);

    private readonly IDotNetCloudApiClient _api;
    private readonly ITokenStore _tokenStore;
    private readonly IChunkedTransferClient _transfer;
    private readonly IConflictResolver _conflictResolver;
    private readonly ILocalStateDb _stateDb;
    private readonly ISelectiveSyncConfig _selectiveSync;
    private readonly ISyncIgnoreParser _syncIgnore;
    private readonly ILockedFileReader _lockedFileReader;
    private readonly SyncStreamListener? _streamListener;
    private readonly ILogger<SyncEngine> _logger;

    /// <summary>
    /// Delay between Tier 2 retry attempts for locked files.
    /// Exposed as internal for test overrides.
    /// </summary>
    internal TimeSpan Tier2RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The device ID for this client installation. When set, the engine uses it for
    /// device-aware echo suppression (skips downloading changes originating from this device).
    /// </summary>
    public Guid? DeviceId { get; set; }

    private FileSystemWatcher? _watcher;
    private bool _pollingFallback;
    private CancellationTokenSource? _cts;
    private Task? _periodicScanTask;
    private SyncContext? _activeContext;
    private SyncState _state = SyncState.Idle;
    private string? _lastError;
    private bool _paused;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private int _syncRerunRequested;

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
        ILogger<SyncEngine> logger,
        SyncStreamListener? streamListener = null)
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
        _streamListener = streamListener;
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

        // Attempt server-side cursor recovery if local cursor is missing (e.g. after reinstall).
        await RecoverCursorFromServerAsync(context, cancellationToken);

        _syncIgnore.Initialize(context.LocalFolderPath);

        // Issue #44: check inotify watch limit on Linux before creating the watcher.
        if (OperatingSystem.IsLinux())
            CheckInotifyLimit();

        // Ensure the local sync folder exists before creating the watcher.
        // The tray app pre-creates this, but verify just in case.
        try { Directory.CreateDirectory(context.LocalFolderPath); }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Could not create sync folder {Path} — insufficient permissions. " +
                "Ensure the folder is pre-created by the tray app.",
                context.LocalFolderPath);
            if (!Directory.Exists(context.LocalFolderPath))
                throw;
        }

        try
        {
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
            _watcher.Error += OnFileSystemWatcherError;
            _watcher.EnableRaisingEvents = true;
        }
        catch (IOException ex) when (OperatingSystem.IsLinux())
        {
            _logger.LogWarning(ex,
                "Could not create FileSystemWatcher — inotify limit likely exhausted. Falling back to polling every 30 seconds.");
            _pollingFallback = true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(ex,
                "Could not create FileSystemWatcher for {Path} — insufficient permissions. Falling back to polling every 30 seconds.",
                context.LocalFolderPath);
            _pollingFallback = true;
        }

        _periodicScanTask = RunPeriodicScanAsync(context, _cts.Token);

        // Start SSE listener if available — enables push-based sync
        if (_streamListener is not null)
        {
            _streamListener.AccessToken = _api.AccessToken;
            _streamListener.SyncChanged += OnSseNotification;
            _streamListener.Start(_cts.Token);
            _logger.LogInformation("SSE sync stream listener started for context {ContextId}.", context.Id);
        }

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

        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            Interlocked.Exchange(ref _syncRerunRequested, 1);
            _logger.LogDebug(
                "Sync already in progress for context {ContextId}; coalescing request into one trailing pass.",
                context.Id);
            return;
        }

        var runTrailingPass = false;
        var syncTimer = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Sync pass starting for context {ContextId}.", context.Id);

            SetState(SyncState.Syncing, context);
            await RefreshAccessTokenAsync(context, cancellationToken);
            var (remoteChangesApplied, serverTree) = await ApplyRemoteChangesAsync(context, cancellationToken);
            var localFilesQueued = await ScanLocalDirectoryAsync(context, serverTree, cancellationToken);
            var localOperationsApplied = await ApplyLocalChangesAsync(context, cancellationToken);
            await _stateDb.UpdateCheckpointAsync(context.StateDatabasePath, DateTime.UtcNow, cancellationToken);
            await _stateDb.CheckpointWalAsync(context.StateDatabasePath, cancellationToken);

            // Acknowledge cursor to server for per-device tracking and recovery
            await AcknowledgeCursorToServerAsync(context, cancellationToken);

            SetState(SyncState.Idle, context);
            syncTimer.Stop();

            _logger.LogInformation(
                "Sync pass complete for context {ContextId}: DurationMs={DurationMs}, RemoteChanges={RemoteChanges}, LocalQueued={LocalQueued}, LocalApplied={LocalApplied}.",
                context.Id,
                syncTimer.ElapsedMilliseconds,
                remoteChangesApplied,
                localFilesQueued,
                localOperationsApplied);
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
            if (IsDiskFullException(ex))
            {
                _logger.LogError(ex,
                    "Disk full while syncing context {ContextId}. Pausing sync until user intervention.",
                    context.Id);

                // Prevent repeated failing sync passes until the user resolves disk pressure.
                _paused = true;
                if (_watcher is not null)
                    _watcher.EnableRaisingEvents = false;

                _lastError = "Disk full: local storage is out of space. Free disk space, then resume sync.";
            }
            else
            {
                _logger.LogError(ex, "Sync error for context {ContextId}.", context.Id);
                _lastError = ex.Message;
            }

            SetState(SyncState.Error, context);
        }
        finally
        {
            _lockedFileReader.ReleaseSnapshot();
            runTrailingPass = Interlocked.Exchange(ref _syncRerunRequested, 0) == 1;
            _syncLock.Release();

            if (runTrailingPass && !_paused && _cts?.IsCancellationRequested != true)
            {
                _logger.LogDebug(
                    "Running coalesced trailing sync pass for context {ContextId}.",
                    context.Id);
                _ = Task.Run(() => SyncAsync(context, _cts?.Token ?? default), _cts?.Token ?? default);
            }
        }
    }

    private static bool IsDiskFullException(Exception ex)
    {
        if (ex is IOException ioEx)
        {
            if (ioEx.HResult == DiskFullHResult)
                return true;

            if (ioEx.Message.Contains("No space left on device", StringComparison.OrdinalIgnoreCase)
                || ioEx.Message.Contains("There is not enough space on the disk", StringComparison.OrdinalIgnoreCase)
                || ioEx.Message.Contains("disk full", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return ex.InnerException is not null && IsDiskFullException(ex.InnerException);
    }

    private static bool IsFileLockedIOException(IOException ex)
    {
        const int SharingViolationHResult = unchecked((int)0x80070020);

        if (ex.HResult == SharingViolationHResult)
            return true;

        var message = ex.Message;
        return message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            || message.Contains("used by another process", StringComparison.OrdinalIgnoreCase)
            || message.Contains("resource temporarily unavailable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("sharing violation", StringComparison.OrdinalIgnoreCase);
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

        // Stop SSE listener
        if (_streamListener is not null)
        {
            _streamListener.SyncChanged -= OnSseNotification;
            await _streamListener.StopAsync();
        }

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_periodicScanTask is not null)
        {
            try
            { await _periodicScanTask; }
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

    /// <summary>
    /// Scans the local sync folder and queues <see cref="PendingUpload"/> for any new or modified files
    /// not yet reflected on the server. Called once per sync pass, between remote-changes application
    /// and local-operations execution.
    /// </summary>
    private async Task<int> ScanLocalDirectoryAsync(SyncContext context, SyncTreeNodeResponse serverTree, CancellationToken cancellationToken)
    {
        // Build a lookup of all tracked file records for O(1) path checks.
        var allRecords = await _stateDb.GetAllFileRecordsAsync(context.StateDatabasePath, cancellationToken);
        var trackedByPath = allRecords.ToDictionary(r => r.LocalPath, StringComparer.OrdinalIgnoreCase);

        // Build a set of paths already queued for upload to avoid duplicates.
        var queuedPaths = await _stateDb.GetPendingUploadPathsAsync(context.StateDatabasePath, cancellationToken);

        // Build a lookup of server files by relative path for dedup against the remote tree.
        // This prevents re-uploading files the server already has (e.g. after state.db reset).
        var serverFilesByRelPath = new Dictionary<string, SyncTreeNodeResponse>(StringComparer.OrdinalIgnoreCase);
        BuildServerFileMap(serverTree, "", serverFilesByRelPath);

        IEnumerable<string> localFiles;
        try
        {
            localFiles = Directory.EnumerateFiles(context.LocalFolderPath, "*", SearchOption.AllDirectories);
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogWarning("Local sync folder {Path} not found during scan.", context.LocalFolderPath);
            return 0;
        }

        var queued = 0;
        foreach (var localPath in localFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(context.LocalFolderPath, localPath);

            if (_syncIgnore.IsIgnored(relativePath))
                continue;

            if (!_selectiveSync.IsIncluded(context.Id, localPath))
                continue;

            // Skip if an upload is already queued for this path.
            if (queuedPaths.Contains(localPath))
                continue;

            if (trackedByPath.TryGetValue(localPath, out var record))
            {
                // Known file — only re-queue if modified since last sync.
                if (!IsLocallyModified(record, localPath))
                    continue;

                _logger.LogDebug(
                    "Local file modified since last sync, queuing upload: {RelPath}", relativePath);
                await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                    new PendingUpload { LocalPath = localPath, NodeId = record.NodeId }, cancellationToken);
                queued++;
            }
            else if (serverFilesByRelPath.TryGetValue(relativePath, out var serverNode))
            {
                // File exists on server but not tracked locally (e.g. after state.db reset).
                // Compare content hashes — if identical, just record it; if different, queue update.
                var localHash = await ComputeFileHashAsync(localPath, cancellationToken);
                if (string.Equals(localHash, serverNode.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Identical — record in state DB without uploading.
                    _logger.LogDebug(
                        "Local file matches server (hash match), recording without upload: {RelPath}", relativePath);
                    await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
                    {
                        LocalPath = localPath,
                        NodeId = serverNode.NodeId,
                        ContentHash = localHash,
                        LastSyncedAt = DateTime.UtcNow,
                        LocalModifiedAt = File.GetLastWriteTimeUtc(localPath),
                    }, cancellationToken);
                }
                else
                {
                    // Different content — queue an update (not a new file).
                    _logger.LogDebug(
                        "Local file differs from server, queuing update upload: {RelPath}", relativePath);
                    await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                        new PendingUpload { LocalPath = localPath, NodeId = serverNode.NodeId }, cancellationToken);
                    queued++;
                }
            }
            else
            {
                // New untracked file — queue a fresh upload.
                _logger.LogDebug("New local file detected, queuing upload: {RelPath}", relativePath);
                await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                    new PendingUpload { LocalPath = localPath }, cancellationToken);
                queued++;
            }
        }

        if (queued > 0)
            _logger.LogInformation(
                "Local scan queued {Count} new/modified file(s) for upload in context {ContextId}.",
                queued, context.Id);

        // ── Local deletion detection ────────────────────────────────────────
        // Check tracked files that no longer exist on disk — these are local deletions
        // that need to be propagated to the server.
        var queuedDeleteNodeIds = await _stateDb.GetPendingDeleteNodeIdsAsync(context.StateDatabasePath, cancellationToken);

        // Build server folder map for folder-level deletion support.
        var serverFoldersByRelPath = new Dictionary<string, SyncTreeNodeResponse>(StringComparer.OrdinalIgnoreCase);
        BuildServerFolderMap(serverTree, "", serverFoldersByRelPath);

        // Phase 1: Collect all missing tracked files.
        var missingFiles = new List<LocalFileRecord>();
        foreach (var record in allRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (record.NodeId == Guid.Empty)
                continue;
            if (File.Exists(record.LocalPath))
                continue;
            if (queuedDeleteNodeIds.Contains(record.NodeId))
                continue;

            var relPath = Path.GetRelativePath(context.LocalFolderPath, record.LocalPath);
            if (_syncIgnore.IsIgnored(relPath))
                continue;

            missingFiles.Add(record);
        }

        // Phase 2: Detect deleted directories — queue a single folder-level delete
        // instead of individual file deletes (server cascade handles children).
        var coveredByFolderDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folderDeletesQueued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in missingFiles)
        {
            var parentDir = Path.GetDirectoryName(record.LocalPath);
            if (parentDir is null || Directory.Exists(parentDir))
                continue; // Parent exists — will need individual file delete

            // Walk up to the highest-level missing ancestor within the sync folder.
            var highestMissing = parentDir;
            var current = Path.GetDirectoryName(parentDir);
            while (current is not null
                && current.Length > context.LocalFolderPath.Length
                && current.StartsWith(context.LocalFolderPath, StringComparison.OrdinalIgnoreCase)
                && !Directory.Exists(current))
            {
                highestMissing = current;
                current = Path.GetDirectoryName(current);
            }

            var folderRelPath = Path.GetRelativePath(context.LocalFolderPath, highestMissing);

            if (folderDeletesQueued.Contains(folderRelPath))
            {
                // Already queued a folder delete for this path — mark file as covered.
                coveredByFolderDelete.Add(record.LocalPath);
                continue;
            }

            // Look up folder NodeId from the server tree.
            if (serverFoldersByRelPath.TryGetValue(folderRelPath, out var folderNode)
                && !queuedDeleteNodeIds.Contains(folderNode.NodeId))
            {
                _logger.LogInformation(
                    "Deleted directory detected, queuing folder deletion: {RelPath} (NodeId={NodeId}).",
                    folderRelPath, folderNode.NodeId);

                await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                    new PendingDelete { LocalPath = highestMissing, NodeId = folderNode.NodeId }, cancellationToken);
                folderDeletesQueued.Add(folderRelPath);
                coveredByFolderDelete.Add(record.LocalPath);
                queued++;
            }
        }

        // Phase 3: Queue individual file deletes for files not covered by a folder delete.
        foreach (var record in missingFiles)
        {
            if (coveredByFolderDelete.Contains(record.LocalPath))
                continue;

            var relPath = Path.GetRelativePath(context.LocalFolderPath, record.LocalPath);
            _logger.LogInformation(
                "Local file deleted, queuing server deletion: {RelPath} (NodeId={NodeId}).",
                relPath, record.NodeId);

            await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                new PendingDelete { LocalPath = record.LocalPath, NodeId = record.NodeId }, cancellationToken);
            queued++;
        }

        return queued;
    }

    private async Task<(int AppliedChanges, SyncTreeNodeResponse Tree)> ApplyRemoteChangesAsync(SyncContext context, CancellationToken cancellationToken)
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
            foreach (var change in page.Changes.Where(c => IsFolderNodeType(c.NodeType) && !c.IsDeleted))
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

                var localPath = await ResolveLocalPathAsync(
                    context,
                    change.NodeId,
                    change.ParentId,
                    change.Name,
                    pathMap,
                    cancellationToken);

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
                else if (IsFileNodeType(change.NodeType))
                {
                    await HandleRemoteUpdateAsync(context, localPath, change, cancellationToken);
                    appliedChanges++;
                }
                else if (IsSymlinkNodeType(change.NodeType) && change.LinkTarget is not null)
                {
                    await HandleRemoteSymlinkAsync(context, localPath, change, cancellationToken);
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

        // Tree-based reconciliation: walk the full server tree and queue downloads
        // for any files that exist on the server but are missing locally.
        // This handles files missed by the change feed (e.g. after state.db wipe
        // when the cursor has already advanced past their change entries).
        var reconciled = await ReconcileServerTreeAsync(context, tree, pathMap, cancellationToken);
        appliedChanges += reconciled;

        return (appliedChanges, tree);
    }

    /// <summary>
    /// Walks the server tree and queues downloads for files that exist on the server
    /// but are missing locally. This ensures subdirectory files are synced even when
    /// the change feed cursor has advanced past their entries.
    /// </summary>
    private async Task<int> ReconcileServerTreeAsync(
        SyncContext context,
        SyncTreeNodeResponse tree,
        Dictionary<Guid, string> pathMap,
        CancellationToken cancellationToken)
    {
        var serverFiles = new Dictionary<string, SyncTreeNodeResponse>(StringComparer.OrdinalIgnoreCase);
        BuildServerFileMap(tree, "", serverFiles);

        // Load pending delete node IDs so we don't re-download files the user just deleted locally.
        var pendingDeleteNodeIds = await _stateDb.GetPendingDeleteNodeIdsAsync(context.StateDatabasePath, cancellationToken);

        var reconciled = 0;
        foreach (var (relativePath, serverNode) in serverFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = Path.Combine(context.LocalFolderPath, relativePath);

            if (!_selectiveSync.IsIncluded(context.Id, localPath))
                continue;

            if (_syncIgnore.IsIgnored(relativePath))
                continue;

            // Skip if the file already exists locally.
            if (File.Exists(localPath))
                continue;

            // Skip if a delete operation is pending for this node — the user deleted it locally
            // and we should not re-download it before the delete is sent to the server.
            if (pendingDeleteNodeIds.Contains(serverNode.NodeId))
                continue;

            // Skip when we already have a valid state DB record for this node.
            // If the record exists but the file is missing on disk, this is a local deletion —
            // don't remove the record or re-download. ScanLocalDirectoryAsync will detect the
            // missing file via the intact record and queue a PendingDelete to propagate to the server.
            var existingRecord = await _stateDb.GetFileRecordByNodeIdAsync(
                context.StateDatabasePath, serverNode.NodeId, cancellationToken);
            if (existingRecord is not null)
            {
                if (File.Exists(existingRecord.LocalPath))
                    continue;

                _logger.LogDebug(
                    "Tracked file missing locally (likely user-deleted), skipping re-download: NodeId={NodeId}, Path={Path}.",
                    serverNode.NodeId,
                    existingRecord.LocalPath);
                continue;
            }

            // Avoid immediate requeue churn for files that recently failed with terminal not-found.
            var hasRecentTerminalFailure = await _stateDb.HasRecentTerminalDownloadFailureAsync(
                context.StateDatabasePath,
                serverNode.NodeId,
                localPath,
                cancellationToken);
            if (hasRecentTerminalFailure)
            {
                _logger.LogDebug(
                    "Skipping tree reconciliation requeue for {RelPath} (NodeId={NodeId}) due to recent terminal download failure.",
                    relativePath,
                    serverNode.NodeId);
                continue;
            }

            _logger.LogInformation(
                "Server file missing locally, queuing download: {RelPath} (NodeId={NodeId}).",
                relativePath, serverNode.NodeId);

            await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                new PendingDownload { LocalPath = localPath, NodeId = serverNode.NodeId, PosixMode = serverNode.PosixMode },
                cancellationToken);
            reconciled++;
        }

        if (reconciled > 0)
            _logger.LogInformation(
                "Tree reconciliation queued {Count} missing file(s) for download in context {ContextId}.",
                reconciled, context.Id);

        return reconciled;
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

    private async Task HandleRemoteSymlinkAsync(SyncContext context, string localPath, Api.SyncChangeResponse change, CancellationToken cancellationToken)
    {
        var localRecord = await _stateDb.GetFileRecordAsync(context.StateDatabasePath, localPath, cancellationToken);
        if (localRecord?.LinkTarget == change.LinkTarget)
            return; // Already up to date

        await _stateDb.QueueOperationAsync(context.StateDatabasePath,
            new PendingDownload { LocalPath = localPath, NodeId = change.NodeId, LinkTarget = change.LinkTarget }, cancellationToken);
    }

    private async Task HandleRemoteUpdateAsync(SyncContext context, string localPath, Api.SyncChangeResponse change, CancellationToken cancellationToken)
    {
        // Issue #51: case-conflict detection on case-insensitive filesystems.
        var resolvedPath = ResolveCaseConflict(localPath);
        if (!string.Equals(resolvedPath, localPath, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Case conflict detected: requested path '{RequestedPath}' conflicts with existing file. Renamed to '{ResolvedPath}'.",
                localPath, resolvedPath);
        }
        localPath = resolvedPath;

        var localRecord = await _stateDb.GetFileRecordAsync(context.StateDatabasePath, localPath, cancellationToken);

        if (localRecord is not null && IsLocallyModified(localRecord, localPath))
        {
            // Echo suppression: if the local file's content hash matches the remote change,
            // this is likely our own upload echoing back. Just update the record and move on.
            string? localContentHash = null;
            try
            {
                localContentHash = await ComputeFileHashAsync(localPath, cancellationToken);
            }
            catch { /* non-critical; resolver falls through to conflict copy if null */ }

            if (!string.IsNullOrEmpty(localContentHash) &&
                !string.IsNullOrEmpty(change.ContentHash) &&
                localContentHash.Equals(change.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Skipping remote change for {RelPath} — local file matches remote hash (echo suppression).",
                    Path.GetRelativePath(context.LocalFolderPath, localPath));
                localRecord.ContentHash = change.ContentHash;
                localRecord.SyncStateTag = "Synced";
                localRecord.LastSyncedAt = DateTime.UtcNow;
                localRecord.LocalModifiedAt = File.Exists(localPath) ? File.GetLastWriteTimeUtc(localPath) : default;
                await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, localRecord, cancellationToken);
                return;
            }

            // Both local and remote changed — run auto-resolution pipeline.

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
                        new PendingDownload { LocalPath = localPath, NodeId = change.NodeId, PosixMode = change.PosixMode }, cancellationToken);
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
            // Device-aware echo suppression: for self-originated changes, trust the local
            // node mapping instead of hash equality because server content hash is manifest-based.
            if (DeviceId.HasValue &&
                change.OriginatingDeviceId == DeviceId.Value &&
                localRecord is not null &&
                localRecord.NodeId == change.NodeId &&
                File.Exists(localPath))
            {
                _logger.LogDebug(
                    "Skipping self-originated change for {RelPath} (device echo suppression).",
                    Path.GetRelativePath(context.LocalFolderPath, localPath));
                localRecord.ContentHash = change.ContentHash;
                localRecord.SyncStateTag = "Synced";
                localRecord.LastSyncedAt = DateTime.UtcNow;
                localRecord.LocalModifiedAt = File.GetLastWriteTimeUtc(localPath);
                await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, localRecord, cancellationToken);
                return;
            }

            // Download the remote version
            await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                new PendingDownload { LocalPath = localPath, NodeId = change.NodeId, PosixMode = change.PosixMode }, cancellationToken);
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
            catch (PathTooLongException ptlEx)
            {
                // Issue #45: path exceeds OS limit — mark as PathTooLong and skip permanently.
                var opPath = op switch
                {
                    PendingUpload u => u.LocalPath,
                    PendingDownload d => d.LocalPath,
                    _ => null,
                };
                _logger.LogWarning(ptlEx,
                    "sync.path_too_long {LocalPath} — skipping permanently. Operation {OpId} moved to failed queue.",
                    opPath, op.Id);

                if (opPath is not null)
                {
                    if (OperatingSystem.IsWindows())
                        _logger.LogWarning(
                            "Windows long-path support is not enabled. Set HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\\LongPathsEnabled=1 (requires admin, then reboot).");

                    var ptlRecord = await _stateDb.GetFileRecordAsync(context.StateDatabasePath, opPath, cancellationToken);
                    if (ptlRecord is not null)
                    {
                        ptlRecord.SyncStateTag = "PathTooLong";
                        await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, ptlRecord, cancellationToken);
                    }
                }

                await _stateDb.MoveToFailedAsync(context.StateDatabasePath, op, ptlEx.Message, cancellationToken);
            }
            catch (HttpRequestException httpEx) when (
                op is PendingDownload &&
                IsNotFoundHttp(httpEx))
            {
                // Remote node/content is missing: retries are not useful for this download operation.
                _logger.LogWarning(httpEx,
                    "Download operation {OpId} failed with 404 Not Found. Moving to failed queue without retry.",
                    op.Id);
                await _stateDb.MoveToFailedAsync(context.StateDatabasePath, op, httpEx.Message, cancellationToken);
            }
            catch (HttpRequestException httpEx) when (
                op is PendingDelete &&
                IsNotFoundHttp(httpEx))
            {
                // Server node already deleted (e.g. cascade from parent folder delete).
                // Treat as successful — clean up local state.
                _logger.LogInformation(
                    "Delete operation {OpId} — server node already gone (404). Cleaning up local state.",
                    op.Id);
                var del = (PendingDelete)op;
                await _stateDb.RemoveFileRecordAsync(context.StateDatabasePath, del.LocalPath, cancellationToken);
                await _stateDb.RemoveFileRecordsUnderPathAsync(context.StateDatabasePath, del.LocalPath, cancellationToken);
                await _stateDb.RemoveOperationAsync(context.StateDatabasePath, op.Id, cancellationToken);
                completedOperations++;
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

    private static bool IsNotFoundHttp(HttpRequestException ex)
    {
        if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return true;
        }

        // Some HTTP pipelines throw 404 exceptions without populating StatusCode.
        return ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecutePendingOperationAsync(SyncContext context, PendingOperationRecord op, CancellationToken cancellationToken)
    {
        if (op is PendingUpload upload)
        {
            // Issue #43: detect local symlinks — upload as SymbolicLink metadata, no content transfer.
            var fileInfo = new FileInfo(upload.LocalPath);
            if (fileInfo.LinkTarget is not null || (OperatingSystem.IsWindows() && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            {
                var symlinkTarget = fileInfo.LinkTarget;
                _logger.LogInformation("Uploading symbolic link {LocalPath} → {Target}.", upload.LocalPath, symlinkTarget);
                var symlinkFileName = Path.GetFileName(upload.LocalPath);
                var parentDir = Path.GetDirectoryName(upload.LocalPath);
                Guid? parentId = parentDir is not null
                    ? (await _stateDb.GetFileRecordAsync(context.StateDatabasePath, parentDir, cancellationToken))?.NodeId
                    : null;
                // Zero-size upload with nodeType hint in filename workaround — the server accepts
                // InitiateUpload with totalSize=0 and no chunks, then reads LinkTarget from the DTO.
                // We pass linkTarget via a custom header by initiating a zero-chunk session.
                var session = await _api.InitiateUploadAsync(
                    symlinkFileName, parentId, totalSize: 0, mimeType: null,
                    chunkHashes: [], chunkSizes: [], posixMode: null,
                    posixOwnerHint: null, linkTarget: symlinkTarget,
                    cancellationToken: cancellationToken);
                var completed = await _api.CompleteUploadAsync(session.SessionId, cancellationToken);
                await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
                {
                    LocalPath = upload.LocalPath,
                    NodeId = completed.Node.Id,
                    ContentHash = null,
                    LastSyncedAt = DateTime.UtcNow,
                    LocalModifiedAt = fileInfo.LastWriteTimeUtc,
                    LinkTarget = symlinkTarget,
                }, cancellationToken);
                return;
            }

            // Issue #40: idempotency check — skip upload if server already has this version.
            if (upload.NodeId.HasValue)
            {
                FileNodeResponse? serverNode = null;
                try
                { serverNode = await _api.GetNodeAsync(upload.NodeId.Value, cancellationToken); }
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
                            PosixMode = OperatingSystem.IsLinux()
                                ? TryGetUnixFileMode(upload.LocalPath)
                                : null,
                        }, cancellationToken);
                        return;
                    }
                }
            }
            var fileName = Path.GetFileName(upload.LocalPath);

            // Issue #45: UTF-8 byte-length check on Linux (255-byte per-component limit).
            if (OperatingSystem.IsLinux())
            {
                var byteLen = System.Text.Encoding.UTF8.GetByteCount(fileName);
                if (byteLen > 255)
                {
                    _logger.LogWarning(
                        "upload.filename_too_long_utf8 {FileName} bytes={Bytes} — skipping.",
                        fileName, byteLen);
                    await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
                    {
                        LocalPath = upload.LocalPath,
                        NodeId = upload.NodeId ?? Guid.Empty,
                        SyncStateTag = "PathTooLong",
                        LastSyncedAt = DateTime.UtcNow,
                        LocalModifiedAt = File.Exists(upload.LocalPath) ? File.GetLastWriteTimeUtc(upload.LocalPath) : DateTime.UtcNow,
                    }, cancellationToken);
                    return;
                }
            }

            var uploadProgress = new InlineProgress<TransferProgress>(p =>
                FileTransferProgress?.Invoke(this, new FileTransferProgressEventArgs
                {
                    FileName = fileName,
                    Direction = "upload",
                    Progress = p,
                }));

            // Read POSIX mode on Linux before opening the file stream.
            int? posixMode = null;
            if (OperatingSystem.IsLinux())
            {
                try
                { posixMode = (int)File.GetUnixFileMode(upload.LocalPath); }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not read UnixFileMode for {Path}; uploading without PosixMode.", upload.LocalPath);
                }
            }

            await using var fileStream = await OpenFileForSyncAsync(upload.LocalPath, cancellationToken);
            var nodeId = await _transfer.UploadAsync(
                upload.NodeId, upload.LocalPath, fileStream, uploadProgress, cancellationToken,
                context.StateDatabasePath, posixMode);

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
                PosixMode = posixMode,
            }, cancellationToken);
        }
        else if (op is PendingDownload download)
        {
            var dlRelPath = Path.GetRelativePath(context.LocalFolderPath, download.LocalPath);
            if (_syncIgnore.IsIgnored(dlRelPath))
            {
                _logger.LogDebug("Skipping download of ignored file {RelPath}.", dlRelPath);
                return;
            }

            // Issue #43: symlink materialisation — no content to download.
            if (download.LinkTarget is not null)
            {
                var symlinkParentPath = Path.GetDirectoryName(download.LocalPath)!;
                var resolvedTarget = Path.GetFullPath(download.LinkTarget, symlinkParentPath);
                if (!IsPathWithinSyncRoot(context.LocalFolderPath, resolvedTarget))
                {
                    _logger.LogWarning(
                        "Blocked symlink {Path} -> {Target}: escapes sync folder.",
                        download.LocalPath,
                        download.LinkTarget);
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(download.LocalPath)!);

                // Windows symlink creation requires Developer Mode or admin; skip gracefully if unavailable.
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        if (File.Exists(download.LocalPath) || Directory.Exists(download.LocalPath))
                            File.Delete(download.LocalPath);
                        File.CreateSymbolicLink(download.LocalPath, download.LinkTarget);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex,
                            "Cannot create symlink {LocalPath} → {Target} on Windows without Developer Mode or admin rights. Skipping.",
                            download.LocalPath, download.LinkTarget);
                        return;
                    }
                }
                else
                {
                    if (File.Exists(download.LocalPath))
                        File.Delete(download.LocalPath);
                    File.CreateSymbolicLink(download.LocalPath, download.LinkTarget);
                }

                await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
                {
                    LocalPath = download.LocalPath,
                    NodeId = download.NodeId,
                    ContentHash = null,
                    LastSyncedAt = DateTime.UtcNow,
                    LocalModifiedAt = new FileInfo(download.LocalPath).LastWriteTimeUtc,
                    LinkTarget = download.LinkTarget,
                }, cancellationToken);
                _logger.LogDebug("Created symlink {LocalPath} → {Target}.", download.LocalPath, download.LinkTarget);
                return;
            }

            var fileName = Path.GetFileName(download.LocalPath);
            var downloadProgress = new InlineProgress<TransferProgress>(p =>
                FileTransferProgress?.Invoke(this, new FileTransferProgressEventArgs
                {
                    FileName = fileName,
                    Direction = "download",
                    Progress = p,
                }));

            using var stream = await _transfer.DownloadAsync(download.NodeId, downloadProgress, cancellationToken);
            var writePath = download.LocalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(writePath)!);
            try
            {
                await using var output = File.Create(writePath);
                await stream.CopyToAsync(output, cancellationToken);
            }
            catch (PathTooLongException) when (OperatingSystem.IsWindows())
            {
                // Issue #45: retry with \\?\ extended-path prefix on Windows.
                stream.Position = 0;
                writePath = ToWindowsLongPath(writePath);
                await using var output = File.Create(writePath);
                await stream.CopyToAsync(output, cancellationToken);
            }

            // Use the written file size — stream.Length throws on non-seekable HTTP response streams.
            var downloadedBytes = new FileInfo(writePath).Length;
            FileTransferComplete?.Invoke(this, new FileTransferCompleteEventArgs
            {
                FileName = fileName,
                Direction = "download",
                TotalBytes = downloadedBytes,
                TotalChunks = 0,
            });

            // Apply POSIX mode on Linux after writing the file.
            if (OperatingSystem.IsLinux())
            {
                if (download.PosixMode.HasValue)
                {
                    // Strip setuid (bit 11) and setgid (bit 10) for security.
                    const int SetuidSetgidMask = 0b_110_000_000_000; // 0o6000
                    var safeMode = (UnixFileMode)(download.PosixMode.Value & ~SetuidSetgidMask);
                    try
                    { File.SetUnixFileMode(writePath, safeMode); }
                    catch (Exception ex) { _logger.LogDebug(ex, "Could not set UnixFileMode {Mode} on {Path}.", safeMode, writePath); }
                }
                else
                {
                    // Windows-originated file: apply sensible Linux default 0o644.
                    var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                        | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
                    try
                    { File.SetUnixFileMode(writePath, defaultMode); }
                    catch (Exception ex) { _logger.LogDebug(ex, "Could not apply default UnixFileMode on {Path}.", writePath); }
                }
            }

            var hash = await ComputeFileHashAsync(writePath, cancellationToken);
            await _stateDb.UpsertFileRecordAsync(context.StateDatabasePath, new LocalFileRecord
            {
                LocalPath = download.LocalPath,
                NodeId = download.NodeId,
                ContentHash = hash,
                LastSyncedAt = DateTime.UtcNow,
                LocalModifiedAt = File.GetLastWriteTimeUtc(download.LocalPath),
                PosixMode = download.PosixMode,
            }, cancellationToken);
        }
        else if (op is PendingDelete delete)
        {
            var delRelPath = Path.GetRelativePath(context.LocalFolderPath, delete.LocalPath);
            _logger.LogInformation(
                "Deleting server node {NodeId} for locally deleted file/folder: {RelPath}.",
                delete.NodeId, delRelPath);

            await _api.DeleteAsync(delete.NodeId, cancellationToken);

            // Clean up file record (individual file) and any child records (folder cascade).
            await _stateDb.RemoveFileRecordAsync(context.StateDatabasePath, delete.LocalPath, cancellationToken);
            await _stateDb.RemoveFileRecordsUnderPathAsync(context.StateDatabasePath, delete.LocalPath, cancellationToken);

            _logger.LogDebug("Server deletion complete for {RelPath} (NodeId={NodeId}).", delRelPath, delete.NodeId);
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

    private async Task<string> ResolveLocalPathAsync(
        SyncContext context,
        Guid nodeId,
        Guid? parentId,
        string name,
        Dictionary<Guid, string> pathMap,
        CancellationToken cancellationToken)
    {
        var existing = await _stateDb.GetFileRecordByNodeIdAsync(context.StateDatabasePath, nodeId, cancellationToken);
        if (existing is not null)
            return ValidatePathWithinSyncRoot(context.LocalFolderPath, existing.LocalPath);

        // Use the tree-derived path map when available
        if (pathMap.TryGetValue(nodeId, out var relativePath))
            return ValidatePathWithinSyncRoot(context.LocalFolderPath, Path.Combine(context.LocalFolderPath, relativePath));

        // When this node is missing from the map (stale tree/page race), build from parent path
        // to avoid incorrectly materializing the file in sync-root.
        if (parentId.HasValue && pathMap.TryGetValue(parentId.Value, out var parentRelativePath))
            return ValidatePathWithinSyncRoot(context.LocalFolderPath, Path.Combine(context.LocalFolderPath, parentRelativePath, name));

        _logger.LogWarning(
            "Could not resolve tree path for node {NodeId} ('{Name}'); falling back to sync-root path.",
            nodeId,
            name);

        return ValidatePathWithinSyncRoot(context.LocalFolderPath, Path.Combine(context.LocalFolderPath, name));
    }

    private static string ValidatePathWithinSyncRoot(string syncRootPath, string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsPathWithinSyncRoot(syncRootPath, fullPath))
            throw new InvalidOperationException($"Resolved path escapes sync folder: {fullPath}");

        return fullPath;
    }

    private static bool IsPathWithinSyncRoot(string syncRootPath, string candidatePath)
    {
        var root = Path.GetFullPath(syncRootPath);
        var candidate = Path.GetFullPath(candidatePath);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return string.Equals(candidate, root, comparison)
            || candidate.StartsWith(normalizedRoot, comparison);
    }

    private static bool IsFolderNodeType(string? nodeType) =>
        string.Equals(nodeType, "Folder", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "Directory", StringComparison.OrdinalIgnoreCase);

    private static bool IsFileNodeType(string? nodeType) =>
        string.Equals(nodeType, "File", StringComparison.OrdinalIgnoreCase);

    private static bool IsSymlinkNodeType(string? nodeType) =>
        string.Equals(nodeType, "SymbolicLink", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// Recursively flattens a server tree into a relative-path → node lookup for files only.
    /// Used by <see cref="ScanLocalDirectoryAsync"/> to detect files already on the server.
    /// </summary>
    private static void BuildServerFileMap(SyncTreeNodeResponse node, string parentPath, Dictionary<string, SyncTreeNodeResponse> map)
    {
        var currentPath = node.NodeId == Guid.Empty
            ? parentPath
            : string.IsNullOrEmpty(parentPath) ? node.Name : Path.Combine(parentPath, node.Name);

        if (node.NodeId != Guid.Empty && IsFileNodeType(node.NodeType))
            map.TryAdd(currentPath, node);

        foreach (var child in node.Children)
            BuildServerFileMap(child, currentPath, map);
    }

    /// <summary>
    /// Recursively flattens a server tree into a relative-path → node lookup for folders only.
    /// Used by <see cref="ScanLocalDirectoryAsync"/> to detect deleted directories.
    /// </summary>
    private static void BuildServerFolderMap(SyncTreeNodeResponse node, string parentPath, Dictionary<string, SyncTreeNodeResponse> map)
    {
        var currentPath = node.NodeId == Guid.Empty
            ? parentPath
            : string.IsNullOrEmpty(parentPath) ? node.Name : Path.Combine(parentPath, node.Name);

        if (node.NodeId != Guid.Empty && IsFolderNodeType(node.NodeType))
            map.TryAdd(currentPath, node);

        foreach (var child in node.Children)
            BuildServerFolderMap(child, currentPath, map);
    }

    private static bool IsLocallyModified(LocalFileRecord record, string localPath)
    {
        if (!File.Exists(localPath))
            return false;
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

    [SupportedOSPlatform("linux")]
    private static int? TryGetUnixFileMode(string path)
    {
        try
        { return (int)File.GetUnixFileMode(path); }
        catch { return null; }
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
        // Tier 1: shared-read open (FileShare.ReadWrite | FileShare.Delete).
        // Fixes the common case where apps such as modern Office hold ReadWrite+Delete share
        // but still allow concurrent readers.
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException ex) when (IsFileLockedIOException(ex))
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
            catch (IOException ex) when (IsFileLockedIOException(ex))
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

    // ── Issue #51: Case-sensitivity helpers ─────────────────────────────────

    /// <summary>
    /// On case-insensitive filesystems (Windows, macOS), checks whether a file
    /// with different casing already exists at <paramref name="localPath"/>.
    /// If so, renames the incoming path to <c>filename (case conflict).ext</c>.
    /// </summary>
    internal static string ResolveCaseConflict(string localPath)
    {
        // Only applies on case-insensitive filesystems.
        if (!IsCaseInsensitiveFileSystem())
            return localPath;

        var directory = Path.GetDirectoryName(localPath);
        if (directory is null || !Directory.Exists(directory))
            return localPath;

        var fileName = Path.GetFileName(localPath);
        string[] siblings;
        try
        { siblings = Directory.GetFileSystemEntries(directory); }
        catch { return localPath; }

        foreach (var existing in siblings)
        {
            var existingName = Path.GetFileName(existing);
            // Same name ignoring case, but different actual casing → conflict.
            if (string.Equals(existingName, fileName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existingName, fileName, StringComparison.Ordinal))
            {
                return BuildCaseConflictPath(localPath);
            }
        }

        return localPath;
    }

    /// <summary>
    /// Builds a case-conflict path: <c>{baseName} (case conflict).ext</c>.
    /// </summary>
    internal static string BuildCaseConflictPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var ext = Path.GetExtension(originalPath);
        var baseName = Path.GetFileNameWithoutExtension(originalPath);

        var candidate = Path.Combine(directory, $"{baseName} (case conflict){ext}");
        var n = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} (case conflict {n}){ext}");
            n++;
        }
        return candidate;
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _onReport;

        public InlineProgress(Action<T> onReport)
        {
            _onReport = onReport;
        }

        public void Report(T value) => _onReport(value);
    }

    /// <summary>
    /// Returns <c>true</c> on Windows and macOS (case-insensitive filesystems).
    /// </summary>
    private static bool IsCaseInsensitiveFileSystem() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    // ── FileSystemWatcher handlers ──────────────────────────────────────────

    // Issue #44: inotify limit check (Linux only).
    private void CheckInotifyLimit()
    {
        const int MinWatches = 65536;
        try
        {
            var raw = File.ReadAllText("/proc/sys/fs/inotify/max_user_watches").Trim();
            if (int.TryParse(raw, out int limit) && limit < MinWatches)
            {
                _logger.LogWarning(
                    "inotify.watch_limit_low Limit={Limit} Recommended={Recommended}",
                    limit, MinWatches);
                _logger.LogWarning(
                    "Fix: echo 'fs.inotify.max_user_watches={Recommended}' | sudo tee /etc/sysctl.d/50-dotnetcloud.conf && sudo sysctl --system",
                    MinWatches);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read inotify watch limit.");
        }
    }

    // Issue #45: long-path support on Windows.
    private static string ToWindowsLongPath(string path)
    {
        if (!OperatingSystem.IsWindows() || path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path;
        return @"\\?\" + Path.GetFullPath(path);
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (_activeContext is null || _paused)
            return;

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
        if (_activeContext is null || _paused)
            return;

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

    // Issue #57: handle FSW.Error to detect internal buffer overflows or watch failures.
    private void OnFileSystemWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogError(ex,
            "FileSystemWatcher error for context {ContextId}. Falling back to polling.",
            _activeContext?.Id);

        _pollingFallback = true;

        // Disable the broken watcher to avoid repeated errors.
        if (_watcher is not null)
            _watcher.EnableRaisingEvents = false;

        // Raise a status change so the tray UI can notify the user.
        if (_activeContext is not null)
        {
            StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
            {
                Context = _activeContext,
                Status = new SyncStatus
                {
                    State = _state,
                    LastError = $"File watcher error: {ex.Message}. Switched to polling.",
                },
            });
        }
    }

    // ── Periodic full scan ──────────────────────────────────────────────────

    private async Task RunPeriodicScanAsync(SyncContext context, CancellationToken cancellationToken)
    {
        // When inotify is unavailable, fall back to a 30-second polling interval.
        // When SSE is connected, extend poll interval to 5 minutes (safety net only).
        var interval = _pollingFallback ? TimeSpan.FromSeconds(30) : context.FullScanInterval;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
                // Adjust interval: longer when SSE is active (push-based), shorter when polling.
                if (_streamListener?.IsConnected == true)
                    interval = TimeSpan.FromMinutes(5);
                else
                    interval = _pollingFallback ? TimeSpan.FromSeconds(30) : context.FullScanInterval;
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

    // ── SSE event handler ───────────────────────────────────────────────────

    private void OnSseNotification(object? sender, SyncChangedEventArgs e)
    {
        if (_paused || _activeContext is null || _cts?.IsCancellationRequested == true)
            return;

        _logger.LogDebug("SSE push notification received (sequence={Sequence}). Triggering sync pass.",
            e.LatestSequence);

        _ = Task.Run(() => SyncAsync(_activeContext, _cts?.Token ?? default), _cts?.Token ?? default);
    }

    // ── Cursor acknowledgement ──────────────────────────────────────────────

    /// <summary>
    /// Sends the current local cursor to the server as an ack, enabling server-side
    /// per-device cursor tracking and recovery after reinstallation.
    /// Best-effort: failures are logged but do not break the sync pass.
    /// </summary>
    private async Task AcknowledgeCursorToServerAsync(SyncContext context, CancellationToken cancellationToken)
    {
        if (DeviceId is null)
            return;

        try
        {
            var cursor = await _stateDb.GetSyncCursorAsync(context.StateDatabasePath, cancellationToken);
            if (cursor is null)
                return;

            // Decode cursor to get the sequence number
            var decoded = DecodeCursorSequence(cursor);
            if (decoded is null)
                return;

            await _api.AcknowledgeCursorAsync(DeviceId.Value, decoded.Value, cancellationToken);
            _logger.LogDebug("Acknowledged cursor (sequence={Sequence}) to server for device {DeviceId}.",
                decoded.Value, DeviceId.Value);
        }
        catch (Exception ex)
        {
            // Best-effort — don't break the sync pass
            _logger.LogDebug(ex, "Failed to acknowledge cursor to server.");
        }
    }

    /// <summary>
    /// Decodes a base64 cursor string to extract the sequence number.
    /// Cursor format: base64("{userId}:{sequence}")
    /// </summary>
    private static long? DecodeCursorSequence(string cursor)
    {
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var colon = raw.LastIndexOf(':');
            if (colon < 1) return null;
            return long.TryParse(raw[(colon + 1)..], out var seq) ? seq : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// If this device has no local cursor but the server has a stored cursor,
    /// restore it to avoid a full re-sync. Best-effort: failures fall back to full sync.
    /// </summary>
    private async Task RecoverCursorFromServerAsync(SyncContext context, CancellationToken cancellationToken)
    {
        if (DeviceId is null)
            return;

        try
        {
            var localCursor = await _stateDb.GetSyncCursorAsync(context.StateDatabasePath, cancellationToken);
            if (localCursor is not null)
                return; // Already have a local cursor — nothing to recover

            var serverCursor = await _api.GetDeviceCursorAsync(DeviceId.Value, cancellationToken);
            if (serverCursor?.Cursor is null)
                return; // No server-side cursor — full sync is needed

            await _stateDb.UpdateSyncCursorAsync(context.StateDatabasePath, serverCursor.Cursor, cancellationToken);
            _logger.LogInformation(
                "Recovered server-side cursor for device {DeviceId}: sequence={Sequence}. Skipping full re-sync.",
                DeviceId.Value, serverCursor.LastAcknowledgedSequence);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server-side cursor recovery failed — will do full sync.");
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
