using System.Text.Json;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
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
        };

        var (engine, conflictResolver) = CreateEngine(registration);

        // Forward conflict events with the context ID
        conflictResolver.ConflictDetected += (_, args) =>
            ConflictDetected?.Invoke(this, new SyncConflictDetectedEventArgs
            {
                ContextId = registration.Id,
                OriginalPath = args.OriginalPath,
                ConflictCopyPath = args.ConflictCopyPath,
            });

        // Forward status changes as service-level events
        engine.StatusChanged += (_, args) => OnEngineStatusChanged(registration.Id, args.Status);

        await engine.StartAsync(syncContext, cancellationToken);

        _contexts[registration.Id] = new RunningContext
        {
            Registration = registration,
            SyncContext = syncContext,
            Engine = engine,
        };

        _logger.LogDebug("Started sync engine for context {ContextId} ({DisplayName}).",
            registration.Id, registration.DisplayName);
    }

    private (ISyncEngine engine, ConflictResolver conflictResolver) CreateEngine(
        SyncContextRegistration registration)
    {
        var tokenStore = CreateTokenStore(registration.DataDirectory);

        // Each context gets its own API client configured with the correct base URL
        var httpClient = _httpClientFactory.CreateClient("DotNetCloudSync");
        httpClient.BaseAddress = new Uri(registration.ServerBaseUrl.TrimEnd('/') + '/');

        var apiClient = new DotNetCloudApiClient(
            httpClient,
            _loggerFactory.CreateLogger<DotNetCloudApiClient>());

        var conflictResolver = new ConflictResolver(
            _loggerFactory.CreateLogger<ConflictResolver>());

        var stateDb = new LocalStateDb(
            _loggerFactory.CreateLogger<LocalStateDb>());

        var transfer = new ChunkedTransferClient(
            apiClient,
            stateDb,
            _loggerFactory.CreateLogger<ChunkedTransferClient>());

        var selectiveSync = new SelectiveSyncConfig();

        var syncIgnore = new SyncIgnoreParser();

        var engine = new SyncEngine(
            apiClient,
            tokenStore,
            transfer,
            conflictResolver,
            stateDb,
            selectiveSync,
            syncIgnore,
            _loggerFactory.CreateLogger<SyncEngine>());

        return (engine, conflictResolver);
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

    private sealed class RunningContext
    {
        public required SyncContextRegistration Registration { get; init; }
        public required SyncContext SyncContext { get; init; }
        public required ISyncEngine Engine { get; init; }
    }
}
