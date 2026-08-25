using System.Text.Json;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Manages multiple sync contexts (one per OS-user + server-account pair),
/// creating and supervising a dedicated <see cref="ISyncEngine"/> for each.
/// </summary>
public sealed class SyncContextManager : ISyncContextManager, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string _registryPath;
    private readonly string _dataRoot;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SyncContextManager> _logger;

    // Dictionary is protected by _lock after startup.
    // LoadContextsAsync is called once before IPC server starts (no lock needed there).
    private readonly Dictionary<Guid, RunningContext> _contexts = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private const string DataRootEnvVar = "DOTNETCLOUD_SYNC_DATA_ROOT";

    /// <inheritdoc/>
    public event EventHandler<SyncProgressEventArgs>? SyncProgress;

    /// <inheritdoc/>
    public event EventHandler<SyncCompleteEventArgs>? SyncComplete;

    /// <inheritdoc/>
    public event EventHandler<SyncErrorEventArgs>? SyncError;

    /// <inheritdoc/>
    public event EventHandler<SyncConflictDetectedEventArgs>? ConflictDetected;

    /// <inheritdoc/>
    public event EventHandler<SyncConflictAutoResolvedEventArgs>? ConflictAutoResolved;

    /// <inheritdoc/>
    public event EventHandler<ContextTransferProgressEventArgs>? TransferProgress;

    /// <inheritdoc/>
    public event EventHandler<ContextTransferCompleteEventArgs>? TransferComplete;

    /// <inheritdoc/>
    public event EventHandler<SizeLimitDecisionRequestedEventArgs>? SizeLimitDecisionRequested;

    /// <summary>Initializes a new <see cref="SyncContextManager"/>.</summary>
    public SyncContextManager(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ILogger<SyncContextManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _logger = logger;

        _dataRoot = GetSystemDataRoot();
        _registryPath = Path.Combine(_dataRoot, "contexts.json");
        Directory.CreateDirectory(_dataRoot);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task LoadContextsAsync(CancellationToken cancellationToken = default)
    {
        var registrations = await LoadRegistrationsAsync(cancellationToken);
        _logger.LogInformation("Loading {Count} persisted sync context(s).", registrations.Count);

        // Pre-register every context (offline) so each engine can compute its sibling
        // scoped-folder exclusions while the contexts are still starting up.
        foreach (var reg in registrations)
        {
            RegisterOfflineContext(reg);
        }

        foreach (var reg in registrations)
        {
            try
            {
                // Called at startup (sequential, before IPC accepts connections — no lock needed).
                await StartContextInternalAsync(reg, cancellationToken);

                // Best-effort reconcile of the server-side sync folder registration.
                var startedRunning = await GetRunningContextAsync(reg.Id);
                if (startedRunning is not null)
                    await TryReconcileServerRegistrationAsync(startedRunning, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start sync engine for context {ContextId} ({DisplayName}). Registering as offline.",
                    reg.Id, reg.DisplayName);

                // Register the context without a running engine so it still
                // appears in ListContexts (shown as offline/error in the UI).
                RegisterOfflineContext(reg);
            }
        }

        ApplyScopedFolderExclusions();
    }

    /// <inheritdoc/>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var stopTasks = _contexts.Values.Select(async ctx =>
            {
                try
                {
                    if (ctx.Engine is not null)
                    {
                        await ctx.Engine.StopAsync(cancellationToken);
                        await ctx.Engine.DisposeAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping context {ContextId}.", ctx.Registration.Id);
                }
            });

            await Task.WhenAll(stopTasks);
            _contexts.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Context management ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncContextRegistration>> GetContextsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _contexts.Values.Select(c => c.Registration).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<SyncContextRegistration> AddContextAsync(
        AddAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var contextId = Guid.CreateVersion7();
        var dataDirectory = Path.Combine(_dataRoot, contextId.ToString("N"));
        Directory.CreateDirectory(dataDirectory);

        var accountKey = BuildAccountKey(request.ServerBaseUrl, request.UserId);
        var registration = new SyncContextRegistration
        {
            Id = contextId,
            ServerBaseUrl = request.ServerBaseUrl,
            UserId = request.UserId,
            LocalFolderPath = request.LocalFolderPath,
            DisplayName = request.DisplayName,
            AccountKey = accountKey,
            OsUserName = request.OsUserName,
            DataDirectory = dataDirectory,
            FullScanInterval = request.FullScanInterval,
            ServerFolderId = request.ServerFolderId,
            ServerFolderDisplayPath = request.ServerFolderDisplayPath,
        };

        // Persist tokens before starting the engine so RefreshAccessTokenAsync finds them
        var tokenStore = CreateTokenStore(dataDirectory);
        await tokenStore.SaveAsync(accountKey, new TokenInfo
        {
            AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken,
            ExpiresAt = request.ExpiresAt,
        }, cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await StartContextInternalAsync(registration, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to start sync engine for new context {ContextId} ({DisplayName}). " +
                    "Account will be saved as offline.",
                    contextId, request.DisplayName);

                // Register the context as offline so the account still appears in the UI.
                RegisterOfflineContext(registration);
            }

            ApplyScopedFolderExclusions();
            await SaveRegistrationsAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Added sync context {ContextId} ({DisplayName}).",
            contextId, request.DisplayName);

        // Best-effort server-side registration of the sync folder.
        var addedRunning = await GetRunningContextAsync(contextId);
        if (addedRunning is not null)
            await TryRegisterSyncFolderOnServerAsync(addedRunning, cancellationToken);

        return registration;
    }

    /// <inheritdoc/>
    public async Task<SyncContextRegistration> AddFolderAsync(
        Guid existingContextId,
        string localFolderPath,
        Guid? serverFolderId,
        string? serverFolderDisplayPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localFolderPath))
            throw new ArgumentException("A local folder path is required.", nameof(localFolderPath));

        var source = await GetRunningContextAsync(existingContextId);
        if (source is null)
            throw new InvalidOperationException("Source sync context not found.");

        var sourceRegistration = source.Registration;
        var contextId = Guid.CreateVersion7();
        var dataDirectory = Path.Combine(_dataRoot, contextId.ToString("N"));
        Directory.CreateDirectory(dataDirectory);

        // Copy the existing account's tokens so the new folder shares the same account (no re-auth).
        var sourceTokenStore = CreateTokenStore(sourceRegistration.DataDirectory);
        var token = await sourceTokenStore.LoadAsync(sourceRegistration.AccountKey, cancellationToken);
        if (token is null)
            throw new InvalidOperationException("No stored tokens are available for the account.");

        var newTokenStore = CreateTokenStore(dataDirectory);
        await newTokenStore.SaveAsync(sourceRegistration.AccountKey, token, cancellationToken);

        var newRegistration = new SyncContextRegistration
        {
            Id = contextId,
            ServerBaseUrl = sourceRegistration.ServerBaseUrl,
            UserId = sourceRegistration.UserId,
            LocalFolderPath = localFolderPath,
            DisplayName = sourceRegistration.DisplayName,
            AccountKey = sourceRegistration.AccountKey,
            OsUserName = sourceRegistration.OsUserName,
            DataDirectory = dataDirectory,
            FullScanInterval = sourceRegistration.FullScanInterval,
            ServerFolderId = serverFolderId,
            ServerFolderDisplayPath = serverFolderDisplayPath,
        };

        await _lock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await StartContextInternalAsync(newRegistration, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start sync engine for new folder context {ContextId}.", contextId);
                RegisterOfflineContext(newRegistration);
            }

            // Make sibling engines (e.g. the whole-account context) exclude the new scoped folder.
            ApplyScopedFolderExclusions();
            await SaveRegistrationsAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        // Best-effort server-side registration of the sync folder.
        var addedRunning = await GetRunningContextAsync(contextId);
        if (addedRunning is not null)
            await TryRegisterSyncFolderOnServerAsync(addedRunning, cancellationToken);

        _logger.LogInformation("Added sync folder {LocalFolder} for context {ContextId}.",
            localFolderPath, existingContextId);
        return newRegistration;
    }

    /// <inheritdoc/>
    public async Task RemoveContextAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        IDotNetCloudApiClient? removedApiClient = null;
        Guid? removedServerFolderId = null;
        string? removedDataDirectory = null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_contexts.TryGetValue(contextId, out var running))
            {
                _logger.LogWarning("Context {ContextId} not found for removal.", contextId);
                return;
            }

            if (running.Engine is not null)
            {
                await running.Engine.StopAsync(cancellationToken);
                await running.Engine.DisposeAsync();
            }

            // Capture before removal so the best-effort cleanup (outside the lock) can use it.
            removedApiClient = running.ApiClient;
            removedServerFolderId = running.Registration.ServerFolderId;
            removedDataDirectory = running.Registration.DataDirectory;

            var tokenStore = CreateTokenStore(running.Registration.DataDirectory);
            await tokenStore.DeleteAsync(running.Registration.AccountKey, cancellationToken);

            _contexts.Remove(contextId);

            // Drop the removed context's scoped folder from sibling engines' exclusions.
            ApplyScopedFolderExclusions();
            await SaveRegistrationsAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        // Best-effort server-side unregistration of the sync folder.
        await TryUnregisterSyncFolderOnServerAsync(removedApiClient, removedServerFolderId, cancellationToken);

        // Best-effort cleanup of the removed context's data directory (state DB, tokens, …)
        // so it doesn't linger as an orphan after the context is removed.
        if (removedDataDirectory is not null)
        {
            try
            {
                if (Directory.Exists(removedDataDirectory))
                    Directory.Delete(removedDataDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not remove data directory for removed context {ContextId}.", contextId);
            }
        }

        _logger.LogInformation("Removed sync context {ContextId}.", contextId);
    }

    /// <inheritdoc/>
    public async Task<int> ReauthenticateAccountAsync(
        Guid contextId,
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_contexts.TryGetValue(contextId, out var primary))
                throw new InvalidOperationException("Account context not found for re-authentication.");

            var accountKey = primary.Registration.AccountKey;

            // Snapshot BEFORE restarting: StartContextInternalAsync replaces entries in
            // _contexts while iterating, which would otherwise throw on enumeration.
            var targets = _contexts.Values
                .Where(c => string.Equals(c.Registration.AccountKey, accountKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var updated = 0;
            foreach (var running in targets)
            {
                var registration = running.Registration;

                // Persist the fresh tokens so engines (re)started later find them.
                var tokenStore = CreateTokenStore(registration.DataDirectory);
                await tokenStore.SaveAsync(accountKey, new TokenInfo
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                }, cancellationToken);

                // Refresh the in-memory access token for running API clients.
                if (running.ApiClient is not null)
                    running.ApiClient.AccessToken = accessToken;

                // Restart engines that failed to start (offline) so the account comes back online.
                if (running.Engine is null)
                {
                    _logger.LogInformation(
                        "Restarting offline sync engine for context {ContextId} after re-authentication.",
                        registration.Id);
                    try
                    {
                        await StartContextInternalAsync(registration, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to restart sync engine for context {ContextId} after re-authentication.",
                            registration.Id);
                        RegisterOfflineContext(registration);
                    }
                }

                updated++;
            }

            ApplyScopedFolderExclusions();

            _logger.LogInformation(
                "Re-authenticated account (key {AccountKey}): updated {Count} context(s).",
                accountKey, updated);
            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Per-context operations ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<SyncStatus?> GetStatusAsync(
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_contexts.TryGetValue(contextId, out var running))
                return null;

            if (running.Engine is null)
                return new SyncStatus { State = SyncState.Error, LastError = "Sync engine failed to start." };

            return await running.Engine.GetStatusAsync(running.SyncContext, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task PauseAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.Engine is null)
            return;
        await running.Engine.PauseAsync(running.SyncContext, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResumeAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.Engine is null)
            return;
        await running.Engine.ResumeAsync(running.SyncContext, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SyncNowAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.Engine is null)
            return;
        await running.Engine.SyncAsync(running.SyncContext, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DotNetCloud.Client.Core.LocalState.ConflictRecord>> ListConflictsAsync(
        Guid contextId, bool includeHistory, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.StateDb is null)
            return [];

        var dbPath = running.SyncContext.StateDatabasePath;
        if (includeHistory)
            return await running.StateDb.GetConflictHistoryAsync(dbPath, cancellationToken);

        return await running.StateDb.GetUnresolvedConflictsAsync(dbPath, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResolveConflictAsync(
        Guid contextId, int conflictId, string resolution,
        CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.StateDb is null)
            return;

        await running.StateDb.ResolveConflictAsync(
            running.SyncContext.StateDatabasePath, conflictId, resolution, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> BatchResolveConflictsAsync(
        string resolution, CancellationToken cancellationToken = default)
    {
        var total = 0;
        foreach (var contextId in _contexts.Keys)
        {
            var running = await GetRunningContextAsync(contextId);
            if (running?.StateDb is null)
                continue;

            var count = await running.StateDb.BatchResolveConflictsAsync(
                running.SyncContext.StateDatabasePath, resolution, cancellationToken);
            total += count;

            if (count > 0)
            {
                _logger.LogInformation(
                    "Batch-resolved {Count} conflict(s) for context {ContextId} with resolution '{Resolution}'.",
                    count, contextId, resolution);
            }
        }
        return total;
    }

    /// <inheritdoc/>
    public async Task UpdateBandwidthAsync(
        decimal uploadLimitKbps, decimal downloadLimitKbps,
        CancellationToken cancellationToken = default)
    {
        // Persist to sync-settings.json so new contexts pick up the values.
        await PersistBandwidthSettingsAsync(uploadLimitKbps, downloadLimitKbps, cancellationToken);

        _logger.LogInformation(
            "Bandwidth limits updated: upload={Upload} KB/s, download={Download} KB/s.",
            uploadLimitKbps, downloadLimitKbps);
    }

    /// <inheritdoc/>
    public async Task UpdateSelectiveSyncAsync(
        Guid contextId,
        IReadOnlyList<SelectiveSyncRule> rules,
        CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running is null || running.SelectiveSync is null || running.StateDb is null)
            return;

        running.SelectiveSync.ClearRules(contextId);
        foreach (var rule in rules)
        {
            if (rule.IsInclude)
                running.SelectiveSync.Include(contextId, rule.FolderPath);
            else
                running.SelectiveSync.Exclude(contextId, rule.FolderPath);
        }

        await running.SelectiveSync.SaveAsync(running.StateDb, running.SyncContext.StateDatabasePath, contextId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SelectiveSyncRule>> GetSelectiveSyncRulesAsync(
        Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.SelectiveSync is null)
            return [];

        return running.SelectiveSync.GetRules(contextId);
    }

    /// <inheritdoc/>
    public async Task ApplySizeLimitDecisionAsync(
        Guid contextId, string relativePath, bool syncFolder, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running is null || running.SelectiveSync is null || running.StateDb is null)
            return;

        running.SelectiveSync.SetRule(contextId, "/" + relativePath.TrimStart('/'), syncFolder, "SizeLimit");
        await running.SelectiveSync.SaveAsync(running.StateDb, running.SyncContext.StateDatabasePath, contextId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetSizeLimitSettingsAsync(bool enabled, long maxFolderSizeBytes, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            foreach (var running in _contexts.Values)
            {
                if (running.Engine is not null)
                {
                    running.Engine.SizeLimitEnabled = enabled;
                    running.Engine.MaxFolderSizeBytes = maxFolderSizeBytes;
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<SyncTreeNodeResponse?> GetFolderTreeAsync(
        Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.ApiClient is null)
            return null;

        // The access token may not be set yet if this is called before the first
        // sync pass (e.g. immediately after add-account). Load it from the token store.
        await EnsureAccessTokenAsync(running, cancellationToken);

        return await running.ApiClient.GetFolderTreeAsync(null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<FileNodeResponse> CreateRemoteFolderAsync(
        Guid contextId, string name, Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.ApiClient is null)
            throw new InvalidOperationException("Sync context not found or not running.");

        // The access token may not be set yet if this is called outside the sync loop.
        await EnsureAccessTokenAsync(running, cancellationToken);

        return await running.ApiClient.CreateFolderAsync(name, parentId, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAllAsync(CancellationToken.None);
        _lock.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Creates and starts a sync engine for <paramref name="registration"/>,
    /// then stores it in <see cref="_contexts"/>.
    /// Must be called either at startup (before IPC server) or while <see cref="_lock"/> is held.
    /// </summary>
    private async Task StartContextInternalAsync(
        SyncContextRegistration registration,
        CancellationToken cancellationToken)
    {
        var syncContext = new SyncContext
        {
            Id = registration.Id,
            ServerBaseUrl = registration.ServerBaseUrl,
            UserId = registration.UserId,
            LocalFolderPath = registration.LocalFolderPath,
            DisplayName = registration.DisplayName,
            StateDatabasePath = Path.Combine(registration.DataDirectory, "state.db"),
            AccountKey = registration.AccountKey,
            FullScanInterval = registration.FullScanInterval,
            UploadLimitKbps = registration.UploadLimitKbps,
            DownloadLimitKbps = registration.DownloadLimitKbps,
            ServerFolderId = registration.ServerFolderId,
            ServerFolderDisplayPath = registration.ServerFolderDisplayPath,
        };

        var stateDatabasePath = Path.Combine(registration.DataDirectory, "state.db");
        var (engine, conflictResolver, stateDb, apiClient, selectiveSync) = CreateEngine(registration);

        // Keep this context from syncing remote subtrees owned by sibling scoped contexts.
        engine.ExcludedServerFolderIds = ComputeExcludedFolderIds(registration);

        // Ensure the per-context state DB schema is current BEFORE loading selective-sync
        // rules. On a brand-new DB, EnsureCreatedAsync creates every table; on an existing
        // DB it is a no-op and RunSchemaEvolutionAsync must run explicitly to add newer
        // tables (e.g. SyncFolderRules). SyncEngine.StartAsync runs this again later, which
        // is idempotent.
        await stateDb.InitializeAsync(stateDatabasePath, cancellationToken);
        await selectiveSync.LoadAsync(stateDb, stateDatabasePath, registration.Id, cancellationToken);

        // One-time migration from the legacy per-folder .selective-sync.json file (if present).
        await MigrateLegacySelectiveSyncAsync(registration, stateDb, stateDatabasePath, selectiveSync, cancellationToken);

        // Forward conflict events with the context ID
        conflictResolver.ConflictDetected += (_, args) =>
            ConflictDetected?.Invoke(this, new SyncConflictDetectedEventArgs
            {
                ContextId = registration.Id,
                OriginalPath = args.OriginalPath,
                ConflictCopyPath = args.ConflictCopyPath,
            });

        conflictResolver.AutoResolved += (_, args) =>
            ConflictAutoResolved?.Invoke(this, new SyncConflictAutoResolvedEventArgs
            {
                ContextId = registration.Id,
                LocalPath = args.LocalPath,
                Strategy = args.Strategy,
                Resolution = args.Resolution,
            });

        // Forward per-file transfer progress with throttling (max 2 events/sec per file).
        // Key: "{contextId}:{fileName}:{direction}"
        var progressThrottle = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();
        engine.FileTransferProgress += (_, args) =>
        {
            var throttleKey = $"{registration.Id}:{args.FileName}:{args.Direction}";
            var now = DateTime.UtcNow;
            // Allow event if last event for this file was >500ms ago (or never sent).
            if (progressThrottle.TryGetValue(throttleKey, out var lastSent)
                && (now - lastSent).TotalMilliseconds < 500)
                return;
            progressThrottle[throttleKey] = now;

            TransferProgress?.Invoke(this, new ContextTransferProgressEventArgs
            {
                ContextId = registration.Id,
                FileName = args.FileName,
                Direction = args.Direction,
                BytesTransferred = args.Progress.BytesTransferred,
                TotalBytes = args.Progress.TotalBytes,
                ChunksTransferred = args.Progress.ChunksTransferred,
                TotalChunks = args.Progress.TotalChunks,
                PercentComplete = args.Progress.PercentComplete,
            });
        };

        engine.FileTransferComplete += (_, args) =>
        {
            // Remove throttle entry for the completed file.
            progressThrottle.TryRemove($"{registration.Id}:{args.FileName}:{args.Direction}", out DateTime _);
            TransferComplete?.Invoke(this, new ContextTransferCompleteEventArgs
            {
                ContextId = registration.Id,
                FileName = args.FileName,
                Direction = args.Direction,
                TotalBytes = args.TotalBytes,
            });
        };

        // Forward status changes as service-level events
        engine.StatusChanged += (_, args) => OnEngineStatusChanged(registration.Id, args.Status);

        // Forward folder size-limit prompts with the context ID.
        engine.SizeLimitDecisionRequested += (_, args) =>
            SizeLimitDecisionRequested?.Invoke(this, new SizeLimitDecisionRequestedEventArgs
            {
                ContextId = registration.Id,
                RelativePath = args.RelativePath,
                SizeBytes = args.SizeBytes,
                LimitBytes = args.LimitBytes,
            });

        await engine.StartAsync(syncContext, cancellationToken);

        _contexts[registration.Id] = new RunningContext
        {
            Registration = registration,
            SyncContext = syncContext,
            Engine = engine,
            StateDb = stateDb,
            ApiClient = apiClient,
            SelectiveSync = selectiveSync,
        };

        _logger.LogDebug("Started sync engine for context {ContextId} ({DisplayName}).",
            registration.Id, registration.DisplayName);
    }

    /// <summary>
    /// Registers a context as offline (no running engine) so it still appears in
    /// <see cref="GetContextsAsync"/> and the tray can show it with an error/offline state.
    /// </summary>
    private void RegisterOfflineContext(SyncContextRegistration registration)
    {
        var syncContext = new SyncContext
        {
            Id = registration.Id,
            ServerBaseUrl = registration.ServerBaseUrl,
            UserId = registration.UserId,
            LocalFolderPath = registration.LocalFolderPath,
            DisplayName = registration.DisplayName,
            StateDatabasePath = Path.Combine(registration.DataDirectory, "state.db"),
            AccountKey = registration.AccountKey,
            FullScanInterval = registration.FullScanInterval,
        };

        _contexts[registration.Id] = new RunningContext
        {
            Registration = registration,
            SyncContext = syncContext,
            Engine = null,
            StateDb = null,
            ApiClient = null,
            SelectiveSync = new SelectiveSyncConfig(),
        };

        _logger.LogWarning("Registered context {ContextId} ({DisplayName}) as offline.",
            registration.Id, registration.DisplayName);
    }

    /// <summary>
    /// Computes the server folder NodeIds this context must NOT sync — the remote subtrees
    /// owned by sibling scoped contexts of the same account. Prevents a whole-account context
    /// from syncing folders that a dedicated scoped context manages (e.g. an added Pictures
    /// folder), which would otherwise cause the two contexts to fight over the same remote folder.
    /// </summary>
    private IReadOnlyList<Guid> ComputeExcludedFolderIds(SyncContextRegistration registration)
    {
        return _contexts.Values
            .Where(c => c.Registration.Id != registration.Id
                && c.Registration.ServerFolderId.HasValue
                && string.Equals(c.Registration.AccountKey, registration.AccountKey, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Registration.ServerFolderId!.Value)
            .ToList();
    }

    /// <summary>
    /// Pushes the current scoped-folder exclusions onto every running engine. Call while holding
    /// <see cref="_lock"/> (or during single-threaded startup) so sibling engines pick up (or drop)
    /// exclusions after contexts are added, removed, or re-authenticated.
    /// </summary>
    private void ApplyScopedFolderExclusions()
    {
        foreach (var running in _contexts.Values)
        {
            if (running.Engine is not null)
                running.Engine.ExcludedServerFolderIds = ComputeExcludedFolderIds(running.Registration);
        }
    }

    private (ISyncEngine engine, ConflictResolver conflictResolver, LocalStateDb stateDb, IDotNetCloudApiClient apiClient, SelectiveSyncConfig selectiveSync) CreateEngine(
        SyncContextRegistration registration)
    {
        var tokenStore = CreateTokenStore(registration.DataDirectory);

        // Resolve or generate a stable device ID for this installation.
        var deviceIdProvider = new DeviceIdProvider(_loggerFactory.CreateLogger<DeviceIdProvider>());
        var deviceId = deviceIdProvider.GetOrCreateDeviceId(_dataRoot);

        // Each context gets its own API client configured with the correct base URL.
        // When bandwidth limits are set, build a custom pipeline with ThrottledHttpHandler.
        var uploadBytes = (long)(registration.UploadLimitKbps * 1024);
        var downloadBytes = (long)(registration.DownloadLimitKbps * 1024);

        HttpClient httpClient;
        if (uploadBytes > 0 || downloadBytes > 0)
        {
            var throttledHandler = new ThrottledHttpHandler(uploadBytes, downloadBytes)
            {
                InnerHandler = new DeviceIdentityHandler(
                    deviceId,
                    _loggerFactory.CreateLogger<DeviceIdentityHandler>())
                {
                    InnerHandler = new CorrelationIdHandler(
                        _loggerFactory.CreateLogger<CorrelationIdHandler>())
                    {
                        InnerHandler = OAuthHttpClientHandlerFactory.CreateHandler()
                    }
                }
            };
            httpClient = new HttpClient(new TimeoutHandler(TimeSpan.FromSeconds(60))
            {
                InnerHandler = throttledHandler
            })
            {
                BaseAddress = new Uri(registration.ServerBaseUrl.TrimEnd('/') + '/')
            };
        }
        else
        {
            httpClient = _httpClientFactory.CreateClient("DotNetCloudSync");
            httpClient.BaseAddress = new Uri(registration.ServerBaseUrl.TrimEnd('/') + '/');
        }

        var apiClient = new DotNetCloudApiClient(
            httpClient,
            _loggerFactory.CreateLogger<DotNetCloudApiClient>());

        var stateDb = new LocalStateDb(
            _loggerFactory.CreateLogger<LocalStateDb>());

        var conflictResolver = new ConflictResolver(
            stateDb,
            _loggerFactory.CreateLogger<ConflictResolver>());

        // Issue #55: load conflict resolution settings from sync-settings.json.
        conflictResolver.Settings = LoadConflictResolutionSettings();

        var transfer = new ChunkedTransferClient(
            apiClient,
            stateDb,
            _loggerFactory.CreateLogger<ChunkedTransferClient>())
        {
            // Enable gRPC client-streaming upload. Falls back to HTTP on RpcException.
            EnableGrpcStreaming = true,
        };

        var selectiveSync = new SelectiveSyncConfig();

        var syncIgnore = new SyncIgnoreParser();

        // Use VssLockedFileReader on Windows (SYSTEM privilege required for VSS).
        // Use NoOpLockedFileReader on Linux/macOS (advisory locks rarely block reads there).
        ILockedFileReader lockedFileReader = OperatingSystem.IsWindows()
            ? new VssLockedFileReader(_loggerFactory.CreateLogger<VssLockedFileReader>())
            : new NoOpLockedFileReader();

        // Create a dedicated HttpClient for the SSE stream listener (long-lived connection).
        var sseHttpClient = new HttpClient(OAuthHttpClientHandlerFactory.CreateHandler())
        {
            BaseAddress = httpClient.BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
        var streamListener = new SyncStreamListener(
            sseHttpClient,
            _loggerFactory.CreateLogger<SyncStreamListener>());

        var engine = new SyncEngine(
            apiClient,
            tokenStore,
            transfer,
            conflictResolver,
            stateDb,
            selectiveSync,
            syncIgnore,
            lockedFileReader,
            _loggerFactory.CreateLogger<SyncEngine>(),
            streamListener)
        {
            DeviceId = deviceId,
            InitialSyncOnStartup = true
        };

        return (engine, conflictResolver, stateDb, apiClient, selectiveSync);
    }

    private async Task MigrateLegacySelectiveSyncAsync(
        SyncContextRegistration registration,
        LocalStateDb stateDb,
        string stateDatabasePath,
        ISelectiveSyncConfig selectiveSync,
        CancellationToken cancellationToken)
    {
        var legacyConfigPath = Path.Combine(registration.LocalFolderPath, ".selective-sync.json");
        if (!File.Exists(legacyConfigPath))
            return;

        try
        {
            var legacyRules = await ReadLegacySelectiveSyncJsonAsync(legacyConfigPath, cancellationToken);
            foreach (var rule in legacyRules)
            {
                if (rule.IsInclude)
                    selectiveSync.Include(registration.Id, rule.FolderPath);
                else
                    selectiveSync.Exclude(registration.Id, rule.FolderPath);
            }

            await selectiveSync.SaveAsync(stateDb, stateDatabasePath, registration.Id, cancellationToken);
            File.Delete(legacyConfigPath);
            _logger.LogInformation("Migrated legacy selective-sync config for context {ContextId}.", registration.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate legacy selective-sync config for context {ContextId}.", registration.Id);
        }
    }

    private static async Task<List<SelectiveSyncRule>> ReadLegacySelectiveSyncJsonAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, List<SelectiveSyncRule>>>(stream, cancellationToken: cancellationToken);
        if (loaded is null)
            return [];

        return loaded.Values.SelectMany(v => v).ToList();
    }

    /// <summary>
    /// Ensures the API client for <paramref name="running"/> has a valid access token loaded.
    /// Called before out-of-band API calls (e.g. folder tree) that happen outside the sync loop.
    /// </summary>
    private async Task EnsureAccessTokenAsync(RunningContext running, CancellationToken cancellationToken)
    {
        if (running.ApiClient is null)
            return;
        if (running.ApiClient.AccessToken is not null)
            return;

        var tokenStore = CreateTokenStore(running.Registration.DataDirectory);
        var tokens = await tokenStore.LoadAsync(running.Registration.AccountKey, cancellationToken);
        if (tokens is null)
        {
            _logger.LogWarning("No tokens found for context {ContextId} when loading for API call.",
                running.Registration.Id);
            return;
        }

        // If expired and refreshable, try to refresh.
        if (tokens.IsExpired && tokens.CanRefresh)
        {
            try
            {
                var refreshed = await running.ApiClient.RefreshTokenAsync(
                    tokens.RefreshToken!, OAuthConstants.ClientId, cancellationToken);
                tokens = new TokenInfo
                {
                    AccessToken = refreshed.AccessToken,
                    RefreshToken = refreshed.RefreshToken ?? tokens.RefreshToken,
                    ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn),
                };
                await tokenStore.SaveAsync(running.Registration.AccountKey, tokens, cancellationToken);
                _logger.LogInformation("Refreshed expired token for context {ContextId}.", running.Registration.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh token for context {ContextId}. Using existing token.",
                    running.Registration.Id);
            }
        }

        running.ApiClient.AccessToken = tokens.AccessToken;
    }

    private EncryptedFileTokenStore CreateTokenStore(string dataDirectory) =>
        new(dataDirectory, _loggerFactory.CreateLogger<EncryptedFileTokenStore>());

    private void OnEngineStatusChanged(Guid contextId, SyncStatus status)
    {
        if (status.State == SyncState.Idle)
        {
            SyncComplete?.Invoke(this, new SyncCompleteEventArgs
            {
                ContextId = contextId,
                Status = status,
            });
        }
        else
        {
            SyncProgress?.Invoke(this, new SyncProgressEventArgs
            {
                ContextId = contextId,
                Status = status,
            });
        }

        if (status.State == SyncState.Error && status.LastError is not null)
        {
            SyncError?.Invoke(this, new SyncErrorEventArgs
            {
                ContextId = contextId,
                ErrorMessage = status.LastError,
            });
        }
    }

    private async Task TryRegisterSyncFolderOnServerAsync(RunningContext running, CancellationToken cancellationToken)
    {
        if (running.ApiClient is null || !running.Registration.ServerFolderId.HasValue)
            return;

        try
        {
            await EnsureAccessTokenAsync(running, cancellationToken);
            await running.ApiClient.RegisterSyncFolderAsync(running.Registration.ServerFolderId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register sync folder on server for context {ContextId}.", running.Registration.Id);
        }
    }

    private async Task TryUnregisterSyncFolderOnServerAsync(IDotNetCloudApiClient? apiClient, Guid? serverFolderId, CancellationToken cancellationToken)
    {
        if (apiClient is null || !serverFolderId.HasValue)
            return;

        try
        {
            await apiClient.DeleteSyncFolderAsync(serverFolderId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unregister sync folder on server.");
        }
    }

    private async Task TryReconcileServerRegistrationAsync(RunningContext running, CancellationToken cancellationToken)
    {
        if (running.ApiClient is null || !running.Registration.ServerFolderId.HasValue)
            return;

        try
        {
            await EnsureAccessTokenAsync(running, cancellationToken);
            var serverRegistrations = await running.ApiClient.ListSyncFoldersAsync(cancellationToken);
            if (serverRegistrations.All(r => r.RemoteFolderNodeId != running.Registration.ServerFolderId.Value))
            {
                await running.ApiClient.RegisterSyncFolderAsync(running.Registration.ServerFolderId.Value, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reconcile sync folder registration for context {ContextId}.", running.Registration.Id);
        }
    }

    private async Task<RunningContext?> GetRunningContextAsync(Guid contextId)
    {
        await _lock.WaitAsync();
        try
        {
            _contexts.TryGetValue(contextId, out var running);
            if (running is null)
                _logger.LogWarning("Context {ContextId} not found.", contextId);
            return running;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<SyncContextRegistration>> LoadRegistrationsAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_registryPath))
            return [];

        try
        {
            await using var stream = File.OpenRead(_registryPath);
            var result = await JsonSerializer.DeserializeAsync<List<SyncContextRegistration>>(
                stream, JsonOptions, cancellationToken);
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load context registry from {Path}.", _registryPath);
            return [];
        }
    }

    // Caller must hold _lock (or be at startup before IPC server starts).
    private async Task SaveRegistrationsAsync(CancellationToken cancellationToken)
    {
        var registrations = _contexts.Values.Select(c => c.Registration).ToList();
        await using var stream = File.Create(_registryPath);
        await JsonSerializer.SerializeAsync(stream, registrations, JsonOptions, cancellationToken);
    }

    private static string BuildAccountKey(string serverBaseUrl, Guid userId) =>
        $"{serverBaseUrl.TrimEnd('/')}:{userId}";

    /// <summary>Returns the platform-appropriate root directory for service data.</summary>
    internal static string GetSystemDataRoot()
    {
        var configuredPath = Environment.GetEnvironmentVariable(DataRootEnvVar);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        if (OperatingSystem.IsWindows())
        {
            // When running inside an MSIX package, the service may not have
            // write access to ProgramData. Use LocalApplicationData instead.
            var baseDir = AppContext.BaseDirectory;
            if (baseDir.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DotNetCloud", "Sync");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "DotNetCloud", "Sync");
        }

        // Prefer the system-wide location when writable (service/root context).
        var systemPath = "/var/lib/dotnetcloud/sync";
        if (CanUseDirectory(systemPath))
        {
            return systemPath;
        }

        // Fallback for non-root developer/runtime sessions.
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "DotNetCloud",
            "Sync");
    }

    private static bool CanUseDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            var probePath = Path.Combine(path, $".write-test-{Guid.CreateVersion7():N}");
            using (File.Create(probePath))
            {
            }
            File.Delete(probePath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Persists bandwidth limits to <c>sync-settings.json</c> so they survive service restarts.
    /// </summary>
    private async Task PersistBandwidthSettingsAsync(
        decimal uploadLimitKbps, decimal downloadLimitKbps,
        CancellationToken cancellationToken)
    {
        var settingsPath = FindOrCreateSyncSettingsPath();
        try
        {
            Dictionary<string, object> root;
            if (File.Exists(settingsPath))
            {
                await using var readStream = File.OpenRead(settingsPath);
                root = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(
                    readStream, JsonOptions, cancellationToken) ?? [];
            }
            else
            {
                root = [];
            }

            root["bandwidth"] = new Dictionary<string, decimal>
            {
                ["uploadLimitKbps"] = uploadLimitKbps,
                ["downloadLimitKbps"] = downloadLimitKbps,
            };

            await using var writeStream = File.Create(settingsPath);
            await JsonSerializer.SerializeAsync(writeStream, root, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist bandwidth settings to {Path}.", settingsPath);
        }
    }

    /// <summary>
    /// Loads bandwidth limits from <c>sync-settings.json</c>.
    /// Returns (0, 0) if the file or section is not found.
    /// </summary>
    internal static (decimal uploadKbps, decimal downloadKbps) LoadBandwidthSettings()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "sync-settings.json"),
            Path.Combine(GetSystemDataRoot(), "sync-settings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "sync-settings.json"),
        };

        var settingsPath = candidates.FirstOrDefault(File.Exists);
        if (settingsPath is null)
            return (0, 0);

        try
        {
            using var stream = File.OpenRead(settingsPath);
            using var doc = System.Text.Json.JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("bandwidth", out var bw))
                return (0, 0);

            var upload = bw.TryGetProperty("uploadLimitKbps", out var u) && u.TryGetDecimal(out var uv)
                ? uv : 0;
            var download = bw.TryGetProperty("downloadLimitKbps", out var d) && d.TryGetDecimal(out var dv)
                ? dv : 0;

            return (upload, download);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Loads conflict resolution settings from <c>sync-settings.json</c>.
    /// Returns defaults if the file or section is not found.
    /// </summary>
    internal static ConflictResolutionSettings LoadConflictResolutionSettings()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "sync-settings.json"),
            Path.Combine(GetSystemDataRoot(), "sync-settings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "sync-settings.json"),
        };

        var settingsPath = candidates.FirstOrDefault(File.Exists);
        if (settingsPath is null)
            return new ConflictResolutionSettings();

        try
        {
            using var stream = File.OpenRead(settingsPath);
            using var doc = System.Text.Json.JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("conflictResolution", out var cr))
                return new ConflictResolutionSettings();

            var settings = new ConflictResolutionSettings();

            if (cr.TryGetProperty("autoResolveEnabled", out var are))
                settings.AutoResolveEnabled = are.GetBoolean();

            if (cr.TryGetProperty("newerWinsThresholdMinutes", out var nwt) && nwt.TryGetInt32(out var nwtVal))
                settings.NewerWinsThresholdMinutes = nwtVal;

            if (cr.TryGetProperty("enabledStrategies", out var es) && es.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                settings.EnabledStrategies = [];
                foreach (var item in es.EnumerateArray())
                {
                    var val = item.GetString();
                    if (val is not null)
                        settings.EnabledStrategies.Add(val);
                }
            }

            return settings;
        }
        catch
        {
            return new ConflictResolutionSettings();
        }
    }

    /// <summary>
    /// Persists conflict resolution settings to <c>sync-settings.json</c>.
    /// </summary>
    public async Task PersistConflictResolutionSettingsAsync(
        ConflictResolutionSettings settings,
        CancellationToken cancellationToken)
    {
        var settingsPath = FindOrCreateSyncSettingsPath();
        try
        {
            Dictionary<string, object> root;
            if (File.Exists(settingsPath))
            {
                await using var readStream = File.OpenRead(settingsPath);
                root = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(
                    readStream, JsonOptions, cancellationToken) ?? [];
            }
            else
            {
                root = [];
            }

            root["conflictResolution"] = new Dictionary<string, object>
            {
                ["autoResolveEnabled"] = settings.AutoResolveEnabled,
                ["newerWinsThresholdMinutes"] = settings.NewerWinsThresholdMinutes,
                ["enabledStrategies"] = settings.EnabledStrategies,
            };

            await using var writeStream = File.Create(settingsPath);
            await JsonSerializer.SerializeAsync(writeStream, root, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist conflict resolution settings to {Path}.", settingsPath);
        }
    }

    private string FindOrCreateSyncSettingsPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "sync-settings.json"),
            Path.Combine(_dataRoot, "sync-settings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "sync-settings.json"),
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[1];
    }

    private sealed class RunningContext
    {
        public required SyncContextRegistration Registration { get; init; }
        public required SyncContext SyncContext { get; init; }

        /// <summary>Null when the engine failed to start (context is offline).</summary>
        public ISyncEngine? Engine { get; init; }

        /// <summary>Null when the engine failed to start (context is offline).</summary>
        public LocalStateDb? StateDb { get; init; }

        /// <summary>Null when the engine failed to start (context is offline).</summary>
        public IDotNetCloudApiClient? ApiClient { get; init; }

        /// <summary>Selective sync rules used by the running engine and persisted config file.</summary>
        public required ISelectiveSyncConfig SelectiveSync { get; init; }

        /// <summary>True when the sync engine is running.</summary>
        public bool IsOnline => Engine is not null;
    }
}
