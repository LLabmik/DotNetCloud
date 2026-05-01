using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
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
    private readonly GrpcChannelManager _channelManager;
    private readonly ResourceLimiter _resourceLimiter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, ModuleProcessHandle> _modules = new();
    private readonly SemaphoreSlim _startStopLock = new(1, 1);

    public ProcessSupervisor(
        ILogger<ProcessSupervisor> logger,
        IOptions<ProcessSupervisorOptions> options,
        ModuleDiscoveryService discoveryService,
        ModuleManifestLoader manifestLoader,
        GrpcChannelManager channelManager,
        ResourceLimiter resourceLimiter,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _discoveryService = discoveryService;
        _manifestLoader = manifestLoader;
        _channelManager = channelManager;
        _resourceLimiter = resourceLimiter;
        _scopeFactory = scopeFactory;
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

            foreach (var module in discovered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await StartModuleCoreAsync(module, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to start module {ModuleId}", module.ModuleId);
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
            _logger.LogWarning("Cannot start module {ModuleId}: not found on filesystem", moduleId);
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

        _logger.LogInformation("Restarting module {ModuleId}", moduleId);
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
            _logger.LogWarning("Module {ModuleId} is already running (PID {Pid})", moduleId, existing.ProcessId);
            return;
        }

        _logger.LogInformation("Starting module {ModuleId} from {Path}", moduleId, discovered.ExecutablePath);

        var manifest = LoadManifest(discovered);
        var restartPolicy = ParseRestartPolicy(manifest.RestartPolicy);
        var memoryLimitMb = manifest.MemoryLimitMb ?? _options.DefaultMemoryLimitMb;
        var grpcEndpoint = BuildGrpcEndpoint(moduleId);

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

        var process = SpawnModuleProcess(discovered, grpcEndpoint);
        if (process is null)
        {
            handle.SetStatus(ModuleProcessStatus.Failed, "Failed to spawn process");
            _modules[moduleId] = handle;
            return;
        }

        handle.SetProcess(process);
        _modules[moduleId] = handle;

        if (_options.EnableResourceLimits && memoryLimitMb > 0)
        {
            var limitBytes = (long)memoryLimitMb * 1024 * 1024;
            var applied = _resourceLimiter.ApplyLimits(moduleId, process, limitBytes, cpuPercent: null);
            if (!applied)
            {
                _logger.LogWarning("Could not apply resource limits for module {ModuleId}", moduleId);
            }
        }

        var healthy = await WaitForModuleHealthyAsync(moduleId, grpcEndpoint, cancellationToken);
        if (!healthy)
        {
            _logger.LogWarning("Module {ModuleId} did not become healthy within startup timeout", moduleId);
            handle.SetStatus(ModuleProcessStatus.Degraded, "Startup health check timed out");
        }
        else
        {
            handle.RecordHealthCheck();
            _logger.LogInformation(
                "Module {ModuleId} is running (PID {Pid}, endpoint {Endpoint})",
                moduleId, handle.ProcessId, grpcEndpoint);
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
        handle.SetStatus(ModuleProcessStatus.Stopped);

        _logger.LogInformation("Module {ModuleId} stopped", moduleId);
    }

    // ---- Process Spawning ----

    private Process? SpawnModuleProcess(DiscoveredModule discovered, string grpcEndpoint)
    {
        try
        {
            var isDll = discovered.ExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var startInfo = new ProcessStartInfo
            {
                FileName = isDll ? "dotnet" : discovered.ExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (isDll)
            {
                startInfo.ArgumentList.Add(discovered.ExecutablePath);
            }

            startInfo.Environment["DOTNETCLOUD_MODULE_ID"] = discovered.ModuleId;
            startInfo.Environment["DOTNETCLOUD_GRPC_ENDPOINT"] = grpcEndpoint;
            startInfo.Environment["DOTNETCLOUD_CORE_ENDPOINT"] = BuildCoreEndpoint();
            startInfo.WorkingDirectory = discovered.ModuleDirectory;

            var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogError("Process.Start returned null for module {ModuleId}", discovered.ModuleId);
                return null;
            }

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    _logger.LogDebug("[{ModuleId}] {Line}", discovered.ModuleId, args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    _logger.LogWarning("[{ModuleId}] STDERR: {Line}", discovered.ModuleId, args.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _logger.LogInformation("Spawned module {ModuleId} as PID {Pid}", discovered.ModuleId, process.Id);
            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn process for module {ModuleId}", discovered.ModuleId);
            return null;
        }
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
        handle.IncrementRestartCount();

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

    private ModuleManifestData LoadManifest(DiscoveredModule discovered)
    {
        if (discovered.ManifestPath is not null)
        {
            var result = _manifestLoader.LoadAndValidate(discovered.ManifestPath, discovered.ModuleId);
            if (result.IsValid && result.Manifest is not null)
                return result.Manifest;

            _logger.LogWarning(
                "Manifest invalid for {ModuleId}: {Errors}. Using defaults.",
                discovered.ModuleId, string.Join("; ", result.Errors));
        }

        return _manifestLoader.CreateDefaultManifest(discovered.ModuleId);
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

    private string BuildGrpcEndpoint(string moduleId)
    {
        if (_options.PreferTcpTransport)
        {
            var port = AllocateTcpPort(moduleId);
            return $"http://localhost:{port}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var safeName = moduleId.Replace('.', '-');
            return $"unix://{Path.Combine(_options.UnixSocketDirectory, $"{safeName}.sock")}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var safeName = moduleId.Replace('.', '-');
            return $"net.pipe://{_options.NamedPipePrefix}-{safeName}";
        }

        var fallbackPort = AllocateTcpPort(moduleId);
        return $"http://localhost:{fallbackPort}";
    }

    private string BuildCoreEndpoint()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $"unix://{Path.Combine(_options.UnixSocketDirectory, "core.sock")}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"net.pipe://{_options.NamedPipePrefix}-core";
        }

        return "http://localhost:50100";
    }

    private int AllocateTcpPort(string moduleId)
    {
        var hash = Math.Abs(moduleId.GetHashCode());
        var range = _options.TcpPortRangeEnd - _options.TcpPortRangeStart;
        return _options.TcpPortRangeStart + (hash % Math.Max(1, range));
    }

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
    /// Ensures all discovered modules have corresponding <see cref="InstalledModule"/>
    /// records in the database so the admin UI and module UI registration can see them.
    /// </summary>
    private async Task SyncDiscoveredModulesToDatabaseAsync(
        IReadOnlyList<DiscoveredModule> discovered, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            var existingModuleIds = await dbContext.InstalledModules
                .Select(m => m.ModuleId)
                .ToListAsync(cancellationToken);

            var existingSet = existingModuleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;

            foreach (var module in discovered)
            {
                if (existingSet.Contains(module.ModuleId))
                    continue;

                var manifest = LoadManifest(module);

                dbContext.InstalledModules.Add(new InstalledModule
                {
                    ModuleId = module.ModuleId,
                    Version = manifest.Version,
                    Status = "Enabled",
                    InstalledAt = DateTime.UtcNow,
                    IsRequired = RequiredModules.IsRequired(module.ModuleId),
                });

                added++;
                _logger.LogInformation(
                    "Auto-registered discovered module {ModuleId} (v{Version}) in database",
                    module.ModuleId, manifest.Version);
            }

            if (added > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Registered {Count} newly discovered modules in database", added);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync discovered modules to database. Admin UI may not show all modules.");
        }
    }
}
