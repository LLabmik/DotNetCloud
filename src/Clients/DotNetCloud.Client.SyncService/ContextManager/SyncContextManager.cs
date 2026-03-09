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

namespace DotNetCloud.Client.SyncService.ContextManager;

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

        foreach (var reg in registrations)
        {
            try
            {
                // Called at startup (sequential, before IPC accepts connections — no lock needed).
                await StartContextInternalAsync(reg, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start sync context {ContextId} ({DisplayName}). Skipping.",
                    reg.Id, reg.DisplayName);
            }
        }
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
                    await ctx.Engine.StopAsync(cancellationToken);
                    await ctx.Engine.DisposeAsync();
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
        var contextId = Guid.NewGuid();
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
            await StartContextInternalAsync(registration, cancellationToken);
            await SaveRegistrationsAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Added sync context {ContextId} ({DisplayName}).",
            contextId, request.DisplayName);
        return registration;
    }

    /// <inheritdoc/>
    public async Task RemoveContextAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_contexts.TryGetValue(contextId, out var running))
            {
                _logger.LogWarning("Context {ContextId} not found for removal.", contextId);
                return;
            }

            await running.Engine.StopAsync(cancellationToken);
            await running.Engine.DisposeAsync();

            var tokenStore = CreateTokenStore(running.Registration.DataDirectory);
            await tokenStore.DeleteAsync(running.Registration.AccountKey, cancellationToken);

            _contexts.Remove(contextId);
            await SaveRegistrationsAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Removed sync context {ContextId}.", contextId);
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
        if (running is null) return;
        await running.Engine.PauseAsync(running.SyncContext, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResumeAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running is null) return;
        await running.Engine.ResumeAsync(running.SyncContext, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SyncNowAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running is null) return;
        await running.Engine.SyncAsync(running.SyncContext, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DotNetCloud.Client.Core.LocalState.ConflictRecord>> ListConflictsAsync(
        Guid contextId, bool includeHistory, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running is null) return [];

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
        if (running is null) return;

        await running.StateDb.ResolveConflictAsync(
            running.SyncContext.StateDatabasePath, conflictId, resolution, cancellationToken);
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
    public async Task<SyncTreeNodeResponse?> GetFolderTreeAsync(
        Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running is null) return null;

        return await running.ApiClient.GetFolderTreeAsync(null, cancellationToken);
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
        };

        var (engine, conflictResolver, stateDb, apiClient) = CreateEngine(registration);

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

        await engine.StartAsync(syncContext, cancellationToken);

        _contexts[registration.Id] = new RunningContext
        {
            Registration = registration,
            SyncContext = syncContext,
            Engine = engine,
            StateDb = stateDb,
            ApiClient = apiClient,
        };

        _logger.LogDebug("Started sync engine for context {ContextId} ({DisplayName}).",
            registration.Id, registration.DisplayName);
    }

    private (ISyncEngine engine, ConflictResolver conflictResolver, LocalStateDb stateDb, IDotNetCloudApiClient apiClient) CreateEngine(
        SyncContextRegistration registration)
    {
        var tokenStore = CreateTokenStore(registration.DataDirectory);

        // Each context gets its own API client configured with the correct base URL.
        // When bandwidth limits are set, build a custom pipeline with ThrottledHttpHandler.
        var uploadBytes = (long)(registration.UploadLimitKbps * 1024);
        var downloadBytes = (long)(registration.DownloadLimitKbps * 1024);

        HttpClient httpClient;
        if (uploadBytes > 0 || downloadBytes > 0)
        {
            var throttledHandler = new ThrottledHttpHandler(uploadBytes, downloadBytes)
            {
                InnerHandler = new CorrelationIdHandler(
                    _loggerFactory.CreateLogger<CorrelationIdHandler>())
                {
                    InnerHandler = OAuthHttpClientHandlerFactory.CreateHandler()
                }
            };
            httpClient = new HttpClient(throttledHandler)
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
            _loggerFactory.CreateLogger<ChunkedTransferClient>());

        var selectiveSync = new SelectiveSyncConfig();

        var syncIgnore = new SyncIgnoreParser();

        // Use VssLockedFileReader on Windows (SYSTEM privilege required for VSS).
        // Use NoOpLockedFileReader on Linux/macOS (advisory locks rarely block reads there).
        ILockedFileReader lockedFileReader = OperatingSystem.IsWindows()
            ? new VssLockedFileReader(_loggerFactory.CreateLogger<VssLockedFileReader>())
            : new NoOpLockedFileReader();

        var engine = new SyncEngine(
            apiClient,
            tokenStore,
            transfer,
            conflictResolver,
            stateDb,
            selectiveSync,
            syncIgnore,
            lockedFileReader,
            _loggerFactory.CreateLogger<SyncEngine>());

        return (engine, conflictResolver, stateDb, apiClient);
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
    private static string GetSystemDataRoot() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "DotNetCloud", "Sync")
            : "/var/lib/dotnetcloud/sync";

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
        public required ISyncEngine Engine { get; init; }
        public required LocalStateDb StateDb { get; init; }
        public required IDotNetCloudApiClient ApiClient { get; init; }
    }
}
