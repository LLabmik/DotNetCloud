using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<SyncEngine> _logger;

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

    /// <summary>Initializes a new <see cref="SyncEngine"/>.</summary>
    public SyncEngine(
        IDotNetCloudApiClient api,
        ITokenStore tokenStore,
        IChunkedTransferClient transfer,
        IConflictResolver conflictResolver,
        ILocalStateDb stateDb,
        ISelectiveSyncConfig selectiveSync,
        ILogger<SyncEngine> logger)
    {
        _api = api;
        _tokenStore = tokenStore;
        _transfer = transfer;
        _conflictResolver = conflictResolver;
        _stateDb = stateDb;
        _selectiveSync = selectiveSync;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        _activeContext = context;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _stateDb.InitializeAsync(context.StateDatabasePath, cancellationToken);

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
        try
        {
            SetState(SyncState.Syncing, context);
            await RefreshAccessTokenAsync(context, cancellationToken);
            await ApplyRemoteChangesAsync(context, cancellationToken);
            await ApplyLocalChangesAsync(context, cancellationToken);
            await _stateDb.UpdateCheckpointAsync(context.StateDatabasePath, DateTime.UtcNow, cancellationToken);
            SetState(SyncState.Idle, context);
            _logger.LogDebug("Sync pass complete for context {ContextId}.", context.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sync cancelled for context {ContextId}.", context.Id);
            SetState(SyncState.Idle, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync error for context {ContextId}.", context.Id);
            _lastError = ex.Message;
            SetState(SyncState.Error, context);
        }
        finally
        {
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
    }

    // ── Private sync logic ──────────────────────────────────────────────────

    private async Task ApplyRemoteChangesAsync(SyncContext context, CancellationToken cancellationToken)
    {
        var checkpoint = await _stateDb.GetCheckpointAsync(context.StateDatabasePath, cancellationToken);
        var since = checkpoint ?? DateTime.UtcNow.AddDays(-365);

        var remoteChanges = await _api.GetChangesSinceAsync(since, null, cancellationToken);
        _logger.LogDebug("Found {Count} remote changes since {Since}.", remoteChanges.Count, since);

        foreach (var change in remoteChanges)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = await ResolveLocalPathAsync(context, change.NodeId, change.Name, cancellationToken);

            if (!_selectiveSync.IsIncluded(context.Id, localPath))
                continue;

            if (change.IsDeleted)
            {
                await HandleRemoteDeletionAsync(context, localPath, change.NodeId, cancellationToken);
            }
            else
            {
                await HandleRemoteUpdateAsync(context, localPath, change, cancellationToken);
            }
        }
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
            // Both local and remote changed — conflict!
            await _conflictResolver.ResolveAsync(new ConflictInfo
            {
                LocalPath = localPath,
                NodeId = change.NodeId,
                RemoteUpdatedAt = change.UpdatedAt,
                RemoteContentHash = change.ContentHash,
            }, cancellationToken);
        }
        else if (localRecord is null || localRecord.ContentHash != change.ContentHash)
        {
            // Download the remote version
            await _stateDb.QueueOperationAsync(context.StateDatabasePath,
                new PendingDownload { LocalPath = localPath, NodeId = change.NodeId }, cancellationToken);
        }
    }

    private async Task ApplyLocalChangesAsync(SyncContext context, CancellationToken cancellationToken)
    {
        var pendingOps = await _stateDb.GetPendingOperationsAsync(context.StateDatabasePath, cancellationToken);

        foreach (var op in pendingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ExecutePendingOperationAsync(context, op, cancellationToken);
                await _stateDb.RemoveOperationAsync(context.StateDatabasePath, op.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to execute pending operation {OpId}. Will retry.", op.Id);
            }
        }
    }

    private async Task ExecutePendingOperationAsync(SyncContext context, PendingOperationRecord op, CancellationToken cancellationToken)
    {
        if (op is PendingUpload upload)
        {
            await using var fileStream = File.OpenRead(upload.LocalPath);
            var nodeId = await _transfer.UploadAsync(upload.NodeId, upload.LocalPath, fileStream, null, cancellationToken);
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
        {
            using var stream = await _transfer.DownloadAsync(download.NodeId, null, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(download.LocalPath)!);
            await using var output = File.Create(download.LocalPath);
            await stream.CopyToAsync(output, cancellationToken);
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

    private async Task<string> ResolveLocalPathAsync(SyncContext context, Guid nodeId, string name, CancellationToken cancellationToken)
    {
        var existing = await _stateDb.GetFileRecordByNodeIdAsync(context.StateDatabasePath, nodeId, cancellationToken);
        if (existing is not null)
            return existing.LocalPath;
        return Path.Combine(context.LocalFolderPath, name);
    }

    private static bool IsLocallyModified(LocalFileRecord record, string localPath)
    {
        if (!File.Exists(localPath)) return false;
        var localModified = File.GetLastWriteTimeUtc(localPath);
        return localModified > record.LastSyncedAt;
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ── FileSystemWatcher handlers ──────────────────────────────────────────

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (_activeContext is null || _paused) return;
        _logger.LogDebug("File system change detected: {ChangeType} {Path}.", e.ChangeType, e.FullPath);
        _ = Task.Run(() => SyncAsync(_activeContext, _cts?.Token ?? default));
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (_activeContext is null || _paused) return;
        _logger.LogDebug("File renamed: {OldPath} → {NewPath}.", e.OldFullPath, e.FullPath);
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
