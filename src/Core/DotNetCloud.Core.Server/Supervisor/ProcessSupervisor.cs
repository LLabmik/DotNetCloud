using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Manages the lifecycle of module processes: spawning, health monitoring,
/// restart policies, resource limiting, and graceful shutdown.
/// Runs as a hosted background service.
/// </summary>
internal sealed class ProcessSupervisor : BackgroundService, IProcessSupervisor
{
    private readonly ILogger<ProcessSupervisor> _logger;
    private readonly ProcessSupervisorOptions _options;
    private readonly ModuleDiscoveryService _discoveryService;
    private readonly ModuleManifestLoader _manifestLoader;
    private readonly IModuleManifestRegistry _manifestRegistry;
    private readonly GrpcChannelManager _channelManager;
    private readonly ResourceLimiter _resourceLimiter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleSchemaService _moduleSchemaService;
    private readonly ModuleCredentialStore _credentials;
    private readonly ModuleAssertionKeyRing _assertions;
    private readonly IModuleDataStoreProvisioningService _dataStoreProvisioning;
    private readonly CoreDatabaseOptions _database;
    private readonly Quotas.ModuleQuotaResolver _quotas;
    private readonly ModuleEndpointAllocator _endpoints;
    private readonly ConcurrentDictionary<string, ModuleProcessHandle> _modules = new();
    private readonly SemaphoreSlim _startStopLock = new(1, 1);

    public ProcessSupervisor(
        ILogger<ProcessSupervisor> logger,
        IOptions<ProcessSupervisorOptions> options,
        ModuleDiscoveryService discoveryService,
        ModuleManifestLoader manifestLoader,
        IModuleManifestRegistry manifestRegistry,
        GrpcChannelManager channelManager,
        ResourceLimiter resourceLimiter,
        IServiceScopeFactory scopeFactory,
        ModuleSchemaService moduleSchemaService,
        ModuleCredentialStore credentials,
        ModuleAssertionKeyRing assertions,
        IModuleDataStoreProvisioningService dataStoreProvisioning,
        CoreDatabaseOptions database,
        Quotas.ModuleQuotaResolver quotas,
        ModuleEndpointAllocator endpoints)
    {
        _logger = logger;
        _options = options.Value;
        _discoveryService = discoveryService;
        _manifestLoader = manifestLoader;
        _manifestRegistry = manifestRegistry;
        _channelManager = channelManager;
        _resourceLimiter = resourceLimiter;
        _scopeFactory = scopeFactory;
        _moduleSchemaService = moduleSchemaService;
        _credentials = credentials;
        _assertions = assertions;
        _dataStoreProvisioning = dataStoreProvisioning;
        _database = database;
        _quotas = quotas;
        _endpoints = endpoints;
    }

    // ---- IProcessSupervisor ----

    /// <inheritdoc />
    public async Task StartAllModulesAsync(CancellationToken cancellationToken = default)
    {
        await _startStopLock.WaitAsync(cancellationToken);
        try
        {
            var discovered = _discoveryService.DiscoverModules();

            if (discovered.Count == 0)
            {
                _logger.LogInformation("No modules discovered. Supervisor idle.");
                return;
            }

            _logger.LogInformation("Starting {Count} discovered modules", discovered.Count);

            await SyncDiscoveredModulesToDatabaseAsync(discovered, cancellationToken);

            // Ensure every module's database schema exists BEFORE its host process is spawned.
            // On a fresh database the InstalledModules table is empty when core startup runs, so
            // the normal pre-start schema pass (Program.InitializeDatabaseAsync) is a no-op, and
            // the lazy pass (ModuleUiRegistrationHostedService) skipped schema creation because the
            // supervisor had just synced the module records above. Module hosts would then query
            // tables that don't exist yet and crash with 42P01 until a container restart. Creating
            // schemas here, before any host starts, makes first boot deterministic and self-healing.
            await EnsureModuleSchemasAsync(discovered, cancellationToken);

            foreach (var module in discovered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await StartModuleCoreAsync(module, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to start module {ModuleId}", LogSanitizer.Sanitize(module.ModuleId));
                }
            }
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAllModulesAsync(CancellationToken cancellationToken = default)
    {
        await _startStopLock.WaitAsync(cancellationToken);
        try
        {
            var moduleIds = _modules.Keys.ToList();
            _logger.LogInformation("Stopping {Count} modules gracefully", moduleIds.Count);

            var stopTasks = moduleIds.Select(id => StopModuleCoreAsync(id, cancellationToken));
            await Task.WhenAll(stopTasks);

            _logger.LogInformation("All modules stopped");
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StartModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var discovered = _discoveryService.DiscoverModule(moduleId);
        if (discovered is null)
        {
            _logger.LogWarning("Cannot start module {ModuleId}: not found on filesystem", SanitizeForLog(moduleId));
            return;
        }

        await StartModuleCoreAsync(discovered, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        await StopModuleCoreAsync(moduleId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestartModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        _logger.LogInformation("Restarting module {ModuleId}", LogSanitizer.Sanitize(moduleId));
        await StopModuleCoreAsync(moduleId, cancellationToken);

        var discovered = _discoveryService.DiscoverModule(moduleId);
        if (discovered is not null)
        {
            await StartModuleCoreAsync(discovered, cancellationToken);
        }
    }

    /// <inheritdoc />
    public ModuleProcessInfo? GetModuleInfo(string moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return _modules.TryGetValue(moduleId, out var handle)
            ? handle.ToProcessInfo()
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ModuleProcessInfo> GetAllModuleInfo()
    {
        return _modules.Values.Select(h => h.ToProcessInfo()).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public bool IsModuleRunning(string moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return _modules.TryGetValue(moduleId, out var handle) && handle.IsRunning;
    }

    // ---- BackgroundService ----

    /// <summary>
    /// Starts all discovered modules on application startup, then runs the health monitor loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield so the rest of the host can finish starting
        await Task.Yield();

        try
        {
            await StartAllModulesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during module startup");
        }

        await RunHealthMonitorLoopAsync(stoppingToken);
    }

    /// <summary>
    /// Gracefully stops all modules when the host shuts down.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Process supervisor shutting down - stopping all modules");

        try
        {
            await StopAllModulesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error stopping modules during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    // ---- Start / Stop Core ----

    private async Task StartModuleCoreAsync(DiscoveredModule discovered, CancellationToken cancellationToken)
    {
        var moduleId = discovered.ModuleId;

        if (_modules.TryGetValue(moduleId, out var existing) && existing.IsRunning)
        {
            _logger.LogWarning("Module {ModuleId} is already running (PID {Pid})", SanitizeForLog(moduleId), existing.ProcessId);
            return;
        }

        _logger.LogInformation("Starting module {ModuleId} from {Path}", SanitizeForLog(moduleId), SanitizeForLog(discovered.ExecutablePath));

        var manifest = LoadManifest(discovered);

        if (ModuleCompatibility.TryGetIncompatibilityReason(manifest, out var incompatibilityReason))
        {
            // Refuse the start instead of launching a host that cannot work against this core.
            // The handle is recorded so the admin surface shows why the module is not running.
            _logger.LogError(
                "Module {ModuleId} v{Version} is incompatible with this core: {Reason}",
                SanitizeForLog(moduleId), SanitizeForLog(manifest.Version), incompatibilityReason);

            RecordRejectedStart(discovered, manifest, incompatibilityReason!);
            return;
        }

        // Resource quotas are resolved before anything is provisioned or spawned: the database
        // connection limit feeds principal creation, and memory/CPU/pids feed the cgroup/job.
        // The manifest only requests; policy and the admin override may lower it.
        var quota = await _quotas.ResolveAsync(moduleId, BuildQuotaRequest(manifest), cancellationToken);

        // A module suspended for exceeding a measured quota stays down until its usage drops or an
        // admin raises the quota; starting it would immediately re-exceed the limit.
        if (await GetSandboxStatusAsync(moduleId, cancellationToken) == ModuleSandboxStatus.QuotaExceeded)
        {
            const string overQuota = "Module data usage is over its quota; raise the quota or remove data before starting it.";
            _logger.LogWarning("Module {ModuleId} cannot start: {Reason}", SanitizeForLog(moduleId), overQuota);
            RecordRejectedStart(discovered, manifest, overQuota);
            return;
        }

        // Sandboxed modules own a scoped database principal. Provision it before the host starts so
        // the process never launches without the store it was promised.
        string? moduleConnectionString = null;
        string? sandboxSchema = null;
        var useLegacyLayout = !ModuleLaunchEnvironment.IsSandboxed(manifest);
        if (ModuleLaunchEnvironment.IsSandboxed(manifest))
        {
            if (manifest.EffectiveDataStoreMode == ModuleDataStoreMode.Isolated)
            {
                var provisioned = await _dataStoreProvisioning.EnsureProvisionedAsync(
                    moduleId, quota.ConnectionLimit, cancellationToken);

                if (!provisioned.Success)
                {
                    // Transitional override (§9.5): a bundled module whose store cannot be
                    // provisioned may keep the legacy config/data layout so the server still boots.
                    // Store-tier modules never qualify.
                    if (await TryEnterLegacyOverrideAsync(moduleId, provisioned.Error ?? "unknown error", cancellationToken))
                    {
                        useLegacyLayout = true;
                    }
                    else
                    {
                        _logger.LogError(
                            "Module {ModuleId} cannot start: {Reason}", SanitizeForLog(moduleId), provisioned.Error);
                        RecordRejectedStart(
                            discovered, manifest, $"Data-store provisioning failed: {provisioned.Error}");
                        await SetSandboxStatusAsync(moduleId, ModuleSandboxStatus.Error, cancellationToken);
                        return;
                    }
                }
                else
                {
                    moduleConnectionString = await _dataStoreProvisioning.GetConnectionStringAsync(
                        moduleId, cancellationToken);
                    sandboxSchema = provisioned.SchemaName;

                    if (string.IsNullOrEmpty(moduleConnectionString))
                    {
                        const string reason = "The module data store is provisioned but its connection string could not be read.";
                        _logger.LogError("Module {ModuleId} cannot start: {Reason}", SanitizeForLog(moduleId), reason);
                        RecordRejectedStart(discovered, manifest, reason);
                        await SetSandboxStatusAsync(moduleId, ModuleSandboxStatus.Error, cancellationToken);
                        return;
                    }
                }
            }

            if (!useLegacyLayout)
            {
                await SetSandboxStatusAsync(moduleId, ModuleSandboxStatus.Active, cancellationToken);
            }
        }

        var restartPolicy = ParseRestartPolicy(manifest.RestartPolicy);
        var memoryLimitMb = quota.MemoryLimitMb ?? 0;

        // Endpoint transport is decided once for the process (see ModuleTransportSelection) and
        // minted here: a per-module Unix socket / named pipe, or a loopback TCP port.
        var endpoint = _endpoints.Resolve(moduleId, GetActiveEndpoints(moduleId));
        if (!ModuleRuntimeArtifacts.Prepare(endpoint, runDirectory: null))
        {
            _logger.LogWarning(
                "Could not prepare the {Transport} endpoint for module {ModuleId}; the module may fail to bind",
                endpoint.Transport, SanitizeForLog(moduleId));
        }

        var grpcEndpoint = endpoint.ToEndpointString();

        var handle = new ModuleProcessHandle
        {
            ModuleId = moduleId,
            ModuleName = manifest.Name,
            Version = manifest.Version,
            ExecutablePath = discovered.ExecutablePath,
            GrpcEndpoint = grpcEndpoint,
            RestartPolicy = restartPolicy,
            MaxRestartAttempts = _options.MaxRestartAttempts
        };

        handle.SetStatus(ModuleProcessStatus.Starting);

        // Mint a fresh credential for this launch. The module echoes it on every capability call,
        // so a process that did not come from this supervisor cannot impersonate the module.
        var moduleToken = _credentials.Issue(moduleId);

        var process = SpawnModuleProcess(
            discovered, manifest, grpcEndpoint, moduleToken, moduleConnectionString, sandboxSchema, useLegacyLayout);
        if (process is null)
        {
            _credentials.Revoke(moduleId);
            handle.SetStatus(ModuleProcessStatus.Failed, "Failed to spawn process");
            _modules[moduleId] = handle;
            return;
        }

        handle.SetProcess(process);
        _modules[moduleId] = handle;

        if (_options.EnableResourceLimits)
        {
            var limitBytes = memoryLimitMb > 0 ? (long)memoryLimitMb * 1024 * 1024 : (long?)null;
            var applied = _resourceLimiter.ApplyLimits(
                moduleId, process, limitBytes, quota.CpuPercent, quota.PidsLimit);
            if (!applied)
            {
                _logger.LogWarning("Could not apply resource limits for module {ModuleId}", SanitizeForLog(moduleId));
            }
        }

        var healthy = await WaitForModuleHealthyAsync(moduleId, grpcEndpoint, cancellationToken);
        if (!healthy)
        {
            _logger.LogWarning("Module {ModuleId} did not become healthy within startup timeout", SanitizeForLog(moduleId));
            handle.SetStatus(ModuleProcessStatus.Degraded, "Startup health check timed out");
        }
        else
        {
            handle.RecordHealthCheck();

            // The socket is created by Kestrel with the process umask, so restrict it to the
            // service account once the listener exists. Defence in depth only — the module
            // credential, not the file mode, is the API identity control.
            if (endpoint.IsSocketTransport)
            {
                ModuleRuntimeArtifacts.RestrictSocket(endpoint);
            }

            _logger.LogInformation(
                "Module {ModuleId} is running (PID {Pid}, endpoint {Endpoint})",
                SanitizeForLog(moduleId), handle.ProcessId, grpcEndpoint);
        }
    }

    private async Task StopModuleCoreAsync(string moduleId, CancellationToken cancellationToken)
    {
        if (!_modules.TryGetValue(moduleId, out var handle))
        {
            _logger.LogDebug("Module {ModuleId} not found in supervisor", moduleId);
            return;
        }

        if (!handle.IsRunning)
        {
            _logger.LogDebug("Module {ModuleId} is not running", moduleId);
            handle.SetStatus(ModuleProcessStatus.Stopped);
            return;
        }

        _logger.LogInformation("Stopping module {ModuleId} (PID {Pid})", moduleId, handle.ProcessId);
        handle.SetStatus(ModuleProcessStatus.Stopping);

        // 1. Send gRPC Stop request for graceful drain
        var grpcStopSuccess = await SendGrpcStopAsync(moduleId, handle.GrpcEndpoint, cancellationToken);
        if (grpcStopSuccess)
        {
            _logger.LogDebug("gRPC Stop acknowledged by module {ModuleId}", moduleId);
        }

        // 2. Wait for graceful exit
        var exited = await WaitForProcessExitAsync(handle, _options.GracefulShutdownTimeout, cancellationToken);

        // 3. Force kill if still running
        if (!exited && handle.IsRunning)
        {
            _logger.LogWarning("Module {ModuleId} did not exit gracefully, force-killing", moduleId);
            ForceKillProcess(handle);
        }

        // 4. Clean up resources
        var pid = handle.ProcessId;
        if (pid.HasValue)
        {
            _resourceLimiter.RemoveLimits(moduleId, pid.Value);
        }

        await _channelManager.RemoveChannelAsync(moduleId);
        _credentials.Revoke(moduleId);
        handle.SetStatus(ModuleProcessStatus.Stopped);

        // A stale socket file would make the next bind fail with "address already in use".
        if (ModuleEndpoint.TryParse(handle.GrpcEndpoint, out var stoppedEndpoint))
        {
            ModuleRuntimeArtifacts.Remove(stoppedEndpoint);
        }

        _logger.LogInformation("Module {ModuleId} stopped", moduleId);
    }

    // ---- Process Spawning ----

    private Process? SpawnModuleProcess(
        DiscoveredModule discovered,
        ModuleManifestData manifest,
        string grpcEndpoint,
        string moduleToken,
        string? moduleConnectionString,
        string? sandboxSchema,
        bool useLegacyLayout)
    {
        try
        {
            // Validate that the discovered executable and directory paths are
            // within the configured modules directory to prevent path traversal
            // or execution of arbitrary binaries (CWE-078).
            //
            // A prefix comparison would accept a sibling directory such as
            // "<modulesDir>-evil"; resolve both paths and compare them relatively instead.
            var modulesDir = Path.GetFullPath(GetModulesDirectory());

            if (!PathContainment.TryResolveWithin(modulesDir, discovered.ExecutablePath, out var fullExePath))
            {
                _logger.LogError(
                    "Module {ModuleId} executable path {Path} is outside the modules directory",
                    discovered.ModuleId, discovered.ExecutablePath);
                return null;
            }

            if (!PathContainment.TryResolveWithin(modulesDir, discovered.ModuleDirectory, out var fullModuleDir))
            {
                _logger.LogError(
                    "Module {ModuleId} directory {Path} is outside the modules directory",
                    discovered.ModuleId, discovered.ModuleDirectory);
                return null;
            }

            // Validate the executable actually exists on disk
            if (!File.Exists(fullExePath))
            {
                _logger.LogError(
                    "Module {ModuleId} executable not found at {Path}",
                    discovered.ModuleId, fullExePath);
                return null;
            }

            var isDll = fullExePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var startInfo = new ProcessStartInfo
            {
                FileName = isDll ? "dotnet" : fullExePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (isDll)
            {
                startInfo.ArgumentList.Add(fullExePath);
            }

            startInfo.Environment["DOTNETCLOUD_MODULE_ID"] = discovered.ModuleId;
            startInfo.Environment[ModuleIdentityMetadata.ModuleTokenEnvironmentVariable] = moduleToken;
            // Public half only: a module can verify a user assertion but cannot mint one.
            startInfo.Environment["DOTNETCLOUD_ASSERTION_PUBLIC_KEY"] = _assertions.PublicKeyBase64;
            // Socket (unix://… / net.pipe://…) or TCP (http://localhost:<port>) form, parsed by the
            // shared host binding helper. Never rewritten to http:// here.
            startInfo.Environment["DOTNETCLOUD_GRPC_ENDPOINT"] = ForceHttpForInternalGrpc(grpcEndpoint);
            startInfo.Environment["DOTNETCLOUD_CORE_ENDPOINT"] = ForceHttpForInternalGrpc(BuildCoreEndpoint());

            var launchEnvironment = ModuleLaunchEnvironment.Resolve(
                manifest,
                _options,
                discovered.ModuleId,
                GetDataRootDirectory(),
                moduleConnectionString,
                useLegacyLayout,
                Environment.GetEnvironmentVariable(ModuleLaunchEnvironment.LegacyConfigDirectoryVariable),
                Environment.GetEnvironmentVariable(ModuleLaunchEnvironment.LegacyDataDirectoryVariable));

            foreach (var (name, value) in launchEnvironment.Variables)
            {
                startInfo.Environment[name] = value;
            }

            if (launchEnvironment.Layout is { } layout)
            {
                // Sandboxed modules never see the core config directory or data directory: those
                // carry the core connection string, TLS material and the WOPI signing key. They get
                // a private directory tree containing only non-secret topology.
                layout.EnsureCreated();
                layout.WriteModuleJson(
                    manifest.Version,
                    manifest.EffectiveDataStoreMode,
                    _database.Provider.ToString(),
                    sandboxSchema,
                    ForceHttpForInternalGrpc(BuildCoreEndpoint()),
                    ForceHttpForInternalGrpc(grpcEndpoint));
            }
            else if (launchEnvironment.LegacyDirectoriesRefused)
            {
                // Transition only: product-shipped v1 modules still load the shared config.json and
                // data directory while they are migrated to the sandboxed model (Track C). With the
                // override disabled they run without either directory rather than with core secrets.
                _logger.LogError(
                    "Module {ModuleId} is not sandboxed and legacy access is disabled; refusing to forward the core config/data directories",
                    SanitizeForLog(discovered.ModuleId));
            }

            startInfo.WorkingDirectory = fullModuleDir;

            var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogError("Process.Start returned null for module {ModuleId}", discovered.ModuleId);
                return null;
            }

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    _logger.LogInformation("[{ModuleId}] {Line}", SanitizeForLog(discovered.ModuleId), SanitizeForLog(args.Data));
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    _logger.LogError("[{ModuleId}] STDERR: {Line}", SanitizeForLog(discovered.ModuleId), SanitizeForLog(args.Data));
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _logger.LogInformation("Spawned module {ModuleId} as PID {Pid}", SanitizeForLog(discovered.ModuleId), process.Id);
            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn process for module {ModuleId}", SanitizeForLog(discovered.ModuleId));
            return null;
        }
    }

    private static string SanitizeForLog(string value)
    {
        return LogSanitizer.Sanitize(value);
    }

    // ---- Health Monitoring ----

    private async Task RunHealthMonitorLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Health monitor started (interval: {Interval}s)",
            _options.HealthCheckInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.HealthCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            foreach (var (moduleId, handle) in _modules)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                if (handle.Status is ModuleProcessStatus.Stopped or ModuleProcessStatus.Stopping)
                    continue;

                await CheckModuleHealthAsync(moduleId, handle, stoppingToken);
            }
        }

        _logger.LogInformation("Health monitor stopped");
    }

    private async Task CheckModuleHealthAsync(
        string moduleId, ModuleProcessHandle handle, CancellationToken cancellationToken)
    {
        if (!handle.IsRunning)
        {
            _logger.LogWarning("Module {ModuleId} process has exited unexpectedly", moduleId);
            handle.SetStatus(ModuleProcessStatus.Crashed, "Process exited unexpectedly");
            await HandleCrashedModuleAsync(moduleId, handle, cancellationToken);
            return;
        }

        // Suppress health checks during the startup grace period to avoid
        // false unhealthy reports while the gRPC endpoint is still initializing.
        if (handle.StartedAt.HasValue &&
            DateTime.UtcNow - handle.StartedAt.Value < _options.StartupGracePeriod)
        {
            _logger.LogDebug(
                "Module {ModuleId} within startup grace period ({Elapsed}s / {Grace}s); skipping health check",
                moduleId,
                (DateTime.UtcNow - handle.StartedAt.Value).TotalSeconds.ToString("F1"),
                _options.StartupGracePeriod.TotalSeconds);
            return;
        }

        var healthy = await ProbeHealthAsync(moduleId, handle.GrpcEndpoint, cancellationToken);
        if (healthy)
        {
            handle.RecordHealthCheck();

            if (handle.StartedAt.HasValue &&
                DateTime.UtcNow - handle.StartedAt.Value >= _options.RestartCounterResetPeriod)
            {
                handle.ResetRestartCount();
            }
        }
        else
        {
            _logger.LogWarning("Health check failed for module {ModuleId}", moduleId);

            if (handle.Status != ModuleProcessStatus.Degraded)
            {
                handle.SetStatus(ModuleProcessStatus.Degraded, "Health check failed");
            }
        }
    }

    private async Task<bool> ProbeHealthAsync(
        string moduleId, string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var channel = _channelManager.GetOrCreateChannel(moduleId, endpoint);
            var client = new ModuleLifecycle.ModuleLifecycleClient(channel);
            var callOptions = _channelManager.GetCallOptions(_options.HealthCheckTimeout, cancellationToken);

            var response = await client.HealthCheckAsync(new HealthCheckRequest(), callOptions);
            return response.Status == HealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health probe failed for module {ModuleId}", moduleId);
            return false;
        }
    }

    private async Task<bool> WaitForModuleHealthyAsync(
        string moduleId, string endpoint, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.StartupTimeout);

        var delay = TimeSpan.FromSeconds(1);
        while (!timeoutCts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (await ProbeHealthAsync(moduleId, endpoint, timeoutCts.Token))
                return true;

            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 5000));
        }

        return false;
    }

    // ---- Restart Handling ----

    private async Task HandleCrashedModuleAsync(
        string moduleId, ModuleProcessHandle handle, CancellationToken cancellationToken)
    {
        var pid = handle.ProcessId;
        if (pid.HasValue)
        {
            _resourceLimiter.RemoveLimits(moduleId, pid.Value);
        }

        await _channelManager.RemoveChannelAsync(moduleId);
        _credentials.Revoke(moduleId);
        handle.IncrementRestartCount();

        // The crashed host may have left its socket behind; a stale file blocks the re-bind.
        if (ModuleEndpoint.TryParse(handle.GrpcEndpoint, out var crashedEndpoint))
        {
            ModuleRuntimeArtifacts.Remove(crashedEndpoint);
        }

        if (handle.RestartPolicy == RestartPolicy.AlertOnly)
        {
            _logger.LogWarning(
                "Module {ModuleId} crashed. Restart policy is AlertOnly - not restarting.",
                moduleId);
            handle.SetStatus(ModuleProcessStatus.Failed, "Crashed. RestartPolicy=AlertOnly.");
            return;
        }

        if (handle.ConsecutiveRestarts >= handle.MaxRestartAttempts)
        {
            _logger.LogError(
                "Module {ModuleId} exceeded max restart attempts ({Max}). Entering failed state.",
                moduleId, handle.MaxRestartAttempts);
            handle.SetStatus(ModuleProcessStatus.Failed,
                $"Exceeded {handle.MaxRestartAttempts} restart attempts");
            return;
        }

        var delay = ComputeRestartDelay(handle);
        _logger.LogInformation(
            "Restarting module {ModuleId} in {Delay}s (attempt {Attempt}/{Max})",
            moduleId, delay.TotalSeconds, handle.ConsecutiveRestarts, handle.MaxRestartAttempts);

        handle.SetStatus(ModuleProcessStatus.WaitingForRestart);

        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var discovered = _discoveryService.DiscoverModule(moduleId);
        if (discovered is not null)
        {
            await StartModuleCoreAsync(discovered, cancellationToken);
        }
        else
        {
            handle.SetStatus(ModuleProcessStatus.Failed, "Module binary no longer found after crash");
        }
    }

    private TimeSpan ComputeRestartDelay(ModuleProcessHandle handle)
    {
        if (handle.RestartPolicy == RestartPolicy.Immediate)
            return TimeSpan.Zero;

        // Exponential backoff: initialDelay * 2^(attempt-1), capped at max
        var exponent = Math.Max(0, handle.ConsecutiveRestarts - 1);
        var delayMs = _options.InitialRestartDelay.TotalMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, _options.MaxRestartDelay.TotalMilliseconds));
    }

    // ---- gRPC Stop (graceful drain) ----

    private async Task<bool> SendGrpcStopAsync(
        string moduleId, string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var channel = _channelManager.GetOrCreateChannel(moduleId, endpoint);
            var client = new ModuleLifecycle.ModuleLifecycleClient(channel);
            var callOptions = _channelManager.GetCallOptions(
                _options.GracefulShutdownTimeout, cancellationToken);

            var response = await client.StopAsync(
                new StopRequest { TimeoutSeconds = (int)_options.GracefulShutdownTimeout.TotalSeconds },
                callOptions);

            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "gRPC Stop call failed for module {ModuleId} (may already be exiting)", moduleId);
            return false;
        }
    }

    // ---- Helpers ----

    /// <summary>
    /// Whether a module runs in the sandboxed model: a version 2 manifest on a non-required module.
    /// </summary>
    /// <summary>
    /// Maps a manifest's resource requests onto a quota request. Values are requests only; the
    /// resolver applies the admin override and policy ceiling with <c>min()</c>.
    /// </summary>
    private static Quotas.ModuleQuotaRequest BuildQuotaRequest(ModuleManifestData manifest) =>
        new(
            MemoryLimitMb: manifest.EffectiveMemoryLimitMb,
            CpuPercent: manifest.Resources?.CpuPercent,
            DataDirMb: manifest.Resources?.DataDirMb,
            DatabaseMb: manifest.Resources?.DatabaseMb,
            ConnectionLimit: manifest.Resources?.ConnectionLimit);

    /// <summary>
    /// Records a module that was refused before a process was spawned, so the admin surface can
    /// show why it is not running.
    /// </summary>
    private void RecordRejectedStart(DiscoveredModule discovered, ModuleManifestData manifest, string reason)
    {
        var rejected = new ModuleProcessHandle
        {
            ModuleId = discovered.ModuleId,
            ModuleName = manifest.Name,
            Version = manifest.Version,
            ExecutablePath = discovered.ExecutablePath,
            GrpcEndpoint = _endpoints.Resolve(discovered.ModuleId, GetActiveEndpoints(discovered.ModuleId)).ToEndpointString(),
            RestartPolicy = ParseRestartPolicy(manifest.RestartPolicy),
            MaxRestartAttempts = _options.MaxRestartAttempts
        };

        rejected.SetStatus(ModuleProcessStatus.Failed, reason);
        _modules[discovered.ModuleId] = rejected;
    }

    /// <summary>
    /// Persists the sandbox state shown to operators. Best effort: a database hiccup must not stop
    /// a module from starting.
    /// </summary>
    /// <summary>
    /// Applies the transitional legacy-access override (§9.5) when a sandboxed module cannot be
    /// provisioned. Only bundled (non-required, non-store-tier) modules qualify, the override must
    /// be enabled by the operator, and every use is audit-logged. Returns false when the module
    /// must be refused instead.
    /// </summary>
    private async Task<bool> TryEnterLegacyOverrideAsync(
        string moduleId, string reason, CancellationToken cancellationToken)
    {
        if (!_options.AllowLegacyAccess)
        {
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var row = await dbContext.InstalledModules
                .AsTracking()
                .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

            if (row is null || row.IsRequired || row.TrustTier == ModuleTrustTier.Store)
            {
                return false;
            }

            row.SandboxStatus = ModuleSandboxStatus.LegacyOverride;
            row.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Module {ModuleId} is using the transitional legacy-access override because its data store could not be provisioned: {Reason}. " +
                "Fix the prerequisite and restart; this override is removed in 1.0.",
                SanitizeForLog(moduleId), reason);

            try
            {
                var auditLogger = scope.ServiceProvider.GetRequiredService<DotNetCloud.Core.Capabilities.IAuditLogger>();
                await auditLogger.LogAsync(
                    new DotNetCloud.Core.Capabilities.AuditEntry
                    {
                        Caller = DotNetCloud.Core.Authorization.CallerContext.CreateSystemContext(),
                        ModuleId = "dotnetcloud.core",
                        Action = DotNetCloud.Core.Capabilities.AuditAction.Update,
                        EntityType = "Module",
                        EntityId = Guid.CreateVersion7(),
                        Description = $"module-legacy-access-used:{moduleId}",
                    },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not audit legacy access for module {ModuleId}", SanitizeForLog(moduleId));
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not apply the legacy-access override for module {ModuleId}", SanitizeForLog(moduleId));
            return false;
        }
    }

    private async Task SetSandboxStatusAsync(
        string moduleId, ModuleSandboxStatus status, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var row = await dbContext.InstalledModules
                .AsTracking()
                .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

            if (row is null || row.SandboxStatus == status)
            {
                return;
            }

            row.SandboxStatus = status;
            row.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not persist sandbox status for module {ModuleId}", SanitizeForLog(moduleId));
        }
    }

    /// <summary>
    /// Resolves the server data directory used for per-module sandbox directories.
    /// </summary>
    private static string GetDataRootDirectory() => ModuleSandboxLayout.GetDataRoot();

    /// <summary>
    /// Reads the persisted sandbox state for a module so starts can be refused while it is
    /// suspended for exceeding a measured quota.
    /// </summary>
    private async Task<ModuleSandboxStatus?> GetSandboxStatusAsync(
        string moduleId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var row = await dbContext.InstalledModules
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

            return row?.SandboxStatus;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "Could not read the sandbox status for module {ModuleId}", SanitizeForLog(moduleId));
            return null;
        }
    }

    private ModuleManifestData LoadManifest(DiscoveredModule discovered)
    {
        if (discovered.ManifestPath is not null)
        {
            var result = _manifestLoader.LoadAndValidate(discovered.ManifestPath, discovered.ModuleId);
            if (result.IsValid && result.Manifest is not null)
            {
                // Every discovery/start path funnels through here, so this is the one place that
                // needs to publish what the module declared it publishes.
                _manifestRegistry.Record(discovered.ModuleId, result.Manifest);
                return result.Manifest;
            }

            _logger.LogWarning(
                "Manifest invalid for {ModuleId}: {Errors}. Using defaults.",
                discovered.ModuleId, string.Join("; ", result.Errors));
        }

        var fallback = _manifestLoader.CreateDefaultManifest(discovered.ModuleId);
        _manifestRegistry.Record(discovered.ModuleId, fallback);
        return fallback;
    }

    private RestartPolicy ParseRestartPolicy(string? policyString)
    {
        if (!string.IsNullOrEmpty(policyString) &&
            Enum.TryParse<RestartPolicy>(policyString, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return _options.DefaultRestartPolicy;
    }

    private string GetModulesDirectory()
    {
        var path = _options.ModulesDirectory;

        // If relative, resolve against application base directory
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        return path;
    }

    /// <summary>
    /// Normalizes an internal module↔core gRPC endpoint to cleartext HTTP.
    /// All internal gRPC is plaintext HTTP/2 (h2c) — never TLS.
    /// </summary>
    private string ForceHttpForInternalGrpc(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint;
        }

        if (endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var coerced = "http://" + endpoint["https://".Length..];
            _logger.LogWarning(
                "Internal gRPC endpoint '{Original}' used an https scheme; coercing to cleartext '{Coerced}' " +
                "(module↔core gRPC is plaintext HTTP/2 and never uses TLS).",
                endpoint, coerced);
            return coerced;
        }

        return endpoint;
    }

    private string BuildCoreEndpoint()
    {
        return $"http://localhost:{_options.TcpPortRangeStart}";
    }

    /// <summary>
    /// Gets the endpoints already handed to other module handles, so TCP port probing skips them.
    /// The module being (re)started is excluded: its own previous handle would otherwise push it
    /// to the next port on every restart.
    /// </summary>
    private IEnumerable<string> GetActiveEndpoints(string excludedModuleId) =>
        _modules
            .Where(pair => !string.Equals(pair.Key, excludedModuleId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.GrpcEndpoint)
            .OfType<string>()
            .Where(endpoint => endpoint.Length > 0);

    private static async Task<bool> WaitForProcessExitAsync(
        ModuleProcessHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.Token.IsCancellationRequested && handle.IsRunning)
        {
            try
            {
                await Task.Delay(250, timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return !handle.IsRunning;
    }

    private void ForceKillProcess(ModuleProcessHandle handle)
    {
        try
        {
            handle.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error force-killing module {ModuleId}", handle.ModuleId);
        }
    }

    // ---- Database Sync ----

    /// <summary>
    /// Ensures all discovered modules have corresponding <see cref="InstalledModule"/> records in
    /// the database, reconciling version, status, required flag and sandbox metadata on restart.
    /// </summary>
    /// <remarks>
    /// This used to be insert-only, so an upgraded module kept its original version and status in
    /// the admin UI forever. The upsert persists the trust tier from <em>provenance</em> and never
    /// upgrades an existing store-tier row: a package that claims a bundled module id must not
    /// inherit the bundled tier.
    /// </remarks>
    private async Task SyncDiscoveredModulesToDatabaseAsync(
        IReadOnlyList<DiscoveredModule> discovered, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var existingModules = await dbContext.InstalledModules
                .ToDictionaryAsync(m => m.ModuleId, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var added = 0;
            var updated = 0;

            foreach (var module in discovered)
            {
                var manifest = LoadManifest(module);
                var isRequired = RequiredModules.IsRequired(module.ModuleId);

                var isNew = !existingModules.TryGetValue(module.ModuleId, out var existingRecord);
                InstalledModule record;
                if (isNew)
                {
                    record = new InstalledModule
                    {
                        ModuleId = module.ModuleId,
                        Status = "Enabled",
                        InstalledAt = DateTime.UtcNow,
                    };

                    dbContext.InstalledModules.Add(record);
                    existingModules[module.ModuleId] = record;
                    added++;
                }
                else
                {
                    record = existingRecord!;
                    updated++;
                }

                // Trust tier is provenance-derived and persisted. Required wins; an existing row
                // keeps its tier (a store package is never silently promoted to bundled); a module
                // discovered for the first time is part of the product's module directory.
                if (isRequired)
                {
                    record.TrustTier = ModuleTrustTier.Required;
                }
                else if (isNew)
                {
                    record.TrustTier = ModuleTrustTier.Bundled;
                }

                record.Version = manifest.Version;
                record.IsRequired = isRequired;
                record.UpdatedAt = DateTime.UtcNow;

                // v2 manifests are authoritative for the data-store mode. v1 (legacy in-repo)
                // modules keep the core-managed schema model until Track C migrates them.
                record.DataStoreMode = manifest.ManifestVersion >= 2
                    ? manifest.EffectiveDataStoreMode
                    : ModuleDataStoreMode.None;

                if (isRequired)
                {
                    record.SandboxStatus = ModuleSandboxStatus.Active;
                }
                else if (isNew)
                {
                    record.SandboxStatus = record.DataStoreMode == ModuleDataStoreMode.Isolated
                        ? ModuleSandboxStatus.PendingProvisioning
                        : ModuleSandboxStatus.Active;
                }
            }

            if (added > 0 || updated > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Reconciled module registry: {Added} added, {Updated} reconciled", added, updated);
            }

            await EnsureManifestGrantsAsync(dbContext, discovered, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync discovered modules to database. Admin UI may not show all modules.");
        }
    }

    /// <summary>
    /// Persists <c>ModuleCapabilityGrants</c> rows for the restricted and privileged capabilities
    /// a module's manifest declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For product-shipped modules (required and bundled) the manifest is the module's install-time
    /// request, so accepting it here is the equivalent of an operator installing the module.
    /// </para>
    /// <para>
    /// <strong>Store-tier modules are never auto-granted.</strong> Their capabilities must be
    /// approved explicitly by an administrator at install time, otherwise a publisher would
    /// self-authorize through its own manifest. Grants whose tier is public or unknown are skipped
    /// because they are not governed by the grant table.
    /// </para>
    /// </remarks>
    private async Task EnsureManifestGrantsAsync(
        CoreDbContext dbContext,
        IReadOnlyList<DiscoveredModule> discovered,
        CancellationToken cancellationToken)
    {
        var trustTiers = await dbContext.InstalledModules
            .Select(m => new { m.ModuleId, m.TrustTier })
            .ToDictionaryAsync(m => m.ModuleId, m => m.TrustTier, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingGrants = await dbContext.ModuleCapabilityGrants
            .Select(g => new { g.ModuleId, g.CapabilityName })
            .ToListAsync(cancellationToken);

        var existingGrantKeys = existingGrants
            .Select(g => $"{g.ModuleId}\u0000{g.CapabilityName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedGrants = 0;

        foreach (var module in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trustTiers.TryGetValue(module.ModuleId, out var trustTier)
                && trustTier == ModuleTrustTier.Store)
            {
                _logger.LogDebug(
                    "Module {ModuleId} is store-tier; manifest capabilities require explicit admin approval",
                    SanitizeForLog(module.ModuleId));
                continue;
            }

            var manifest = LoadManifest(module);

            foreach (var capability in manifest.EffectiveCapabilities)
            {
                if (!CapabilityCatalog.RequiresGrant(capability))
                {
                    continue;
                }

                var tier = CapabilityCatalog.GetCapabilityTier(capability);

                var key = $"{module.ModuleId}\u0000{capability}";
                if (!existingGrantKeys.Add(key))
                {
                    continue;
                }

                dbContext.ModuleCapabilityGrants.Add(new ModuleCapabilityGrant
                {
                    ModuleId = module.ModuleId,
                    CapabilityName = capability,
                    GrantedAt = DateTime.UtcNow,
                });

                addedGrants++;
                _logger.LogInformation(
                    "Provisioned manifest-declared {Tier} capability {Capability} for module {ModuleId}",
                    tier, capability, module.ModuleId);
            }
        }

        if (addedGrants > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Provisioned {Count} manifest-declared capability grants", addedGrants);
        }
    }

    /// <summary>
    /// Creates the database schema for every discovered module before its host process
    /// starts. Core-managed (first-party) modules have their EF migrations applied here;
    /// self-managed (third-party) modules are skipped and create their own schema.
    /// A per-module failure is logged and does not block the remaining modules — the
    /// supervisor's restart policy still lets an individual host recover.
    /// </summary>
    private async Task EnsureModuleSchemasAsync(
        IReadOnlyList<DiscoveredModule> discovered, CancellationToken cancellationToken)
    {
        foreach (var module in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("Ensuring database schema for module {ModuleId}", SanitizeForLog(module.ModuleId));
                await _moduleSchemaService.EnsureModuleSchemaAsync(module.ModuleId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure database schema for module {ModuleId}", SanitizeForLog(module.ModuleId));
            }
        }
    }
}
