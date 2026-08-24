using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using HealthStatusGrpc = DotNetCloud.Core.Grpc.Lifecycle.HealthStatus;
using HealthStatusCore = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace DotNetCloud.Core.Server.HealthChecks;

/// <summary>
/// Aggregate health check that queries every process-isolated module via gRPC
/// <c>ModuleLifecycle.HealthCheck()</c> and reports per-module status.
/// Registered in the Core Server's health check pipeline so that
/// <c>GET /api/v1/core/admin/health</c> shows health for all modules.
/// </summary>
/// <remarks>
/// Each module's health is reported as a sub-entry in the <c>Data</c> dictionary.
/// The overall status is the worst status across all modules
/// (Healthy → Degraded → Unhealthy).
/// Modules that are not running or have no endpoint are reported as Unhealthy.
/// gRPC calls are made in parallel with a per-module timeout.
/// </remarks>
internal sealed class ModulesAggregateHealthCheck : IHealthCheck
{
    private readonly IProcessSupervisor _supervisor;
    private readonly GrpcChannelManager _channelManager;
    private readonly DatabaseConnectivityState _dbState;
    private readonly ILogger<ModulesAggregateHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModulesAggregateHealthCheck"/> class.
    /// </summary>
    /// <param name="supervisor">The process supervisor providing module process information.</param>
    /// <param name="channelManager">The gRPC channel manager for module communication.</param>
    /// <param name="dbState">Cached database availability (drives the 503 gate and database health entry).</param>
    /// <param name="logger">Logger instance.</param>
    internal ModulesAggregateHealthCheck(
        IProcessSupervisor supervisor,
        GrpcChannelManager channelManager,
        DatabaseConnectivityState dbState,
        ILogger<ModulesAggregateHealthCheck> logger)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
        _dbState = dbState ?? throw new ArgumentNullException(nameof(dbState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var modules = _supervisor.GetAllModuleInfo();

        if (modules.Count == 0)
        {
            return HealthCheckResult.Healthy("No modules discovered or supervised yet.");
        }

        // Query each module's gRPC health endpoint in parallel.
        var tasks = modules.Select(m => CheckModuleHealthAsync(m, cancellationToken));
        var results = await Task.WhenAll(tasks);

        var entries = new Dictionary<string, object>();
        var overallStatus = HealthStatusCore.Healthy;
        var descriptions = new List<string>();

        foreach (var result in results)
        {
            entries[result.ModuleId] = new
            {
                status = result.Status.ToString(),
                description = result.Description,
                moduleName = result.ModuleName,
                version = result.Version,
                processStatus = result.ProcessStatus.ToString(),
            };

            if (result.Status == HealthStatusCore.Unhealthy)
            {
                overallStatus = HealthStatusCore.Unhealthy;
                descriptions.Add($"Module '{result.ModuleId}' is unhealthy: {result.Description}");
            }
            else if (result.Status == HealthStatusCore.Degraded && overallStatus != HealthStatusCore.Unhealthy)
            {
                overallStatus = HealthStatusCore.Degraded;
                descriptions.Add($"Module '{result.ModuleId}' is degraded: {result.Description}");
            }
        }

        var overallDescription = overallStatus switch
        {
            HealthStatusCore.Healthy => $"{results.Length} module(s) — all healthy.",
            HealthStatusCore.Degraded => $"{results.Length} module(s) — one or more degraded: {string.Join("; ", descriptions)}",
            HealthStatusCore.Unhealthy => $"{results.Length} module(s) — one or more unhealthy: {string.Join("; ", descriptions)}",
            _ => $"{results.Length} module(s) — status unknown."
        };

        return new HealthCheckResult(overallStatus, overallDescription, data: entries);
    }

    private async Task<ModuleHealthEntry> CheckModuleHealthAsync(
        ModuleProcessInfo moduleInfo,
        CancellationToken cancellationToken)
    {
        var moduleId = moduleInfo.ModuleId;
        var moduleName = moduleInfo.ModuleName;

        // If the module process isn't in a running state, report unhealthy.
        if (moduleInfo.Status != ModuleProcessStatus.Running &&
            moduleInfo.Status != ModuleProcessStatus.Degraded)
        {
            return new ModuleHealthEntry
            {
                ModuleId = moduleId,
                ModuleName = moduleName,
                Version = moduleInfo.Version,
                Status = HealthStatusCore.Unhealthy,
                Description = $"Process status is '{moduleInfo.Status}' — not available for health check.",
                ProcessStatus = moduleInfo.Status
            };
        }

        var endpoint = moduleInfo.GrpcEndpoint;
        if (string.IsNullOrEmpty(endpoint))
        {
            return new ModuleHealthEntry
            {
                ModuleId = moduleId,
                ModuleName = moduleName,
                Version = moduleInfo.Version,
                Status = HealthStatusCore.Unhealthy,
                Description = "No gRPC endpoint available.",
                ProcessStatus = moduleInfo.Status
            };
        }

        try
        {
            var channel = _channelManager.GetOrCreateChannel(moduleId, endpoint);
            var client = new ModuleLifecycle.ModuleLifecycleClient(channel);
            var callOptions = _channelManager.GetCallOptions(
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken: cancellationToken);

            var response = await client.HealthCheckAsync(new HealthCheckRequest(), callOptions);

            var mappedStatus = response.Status switch
            {
                HealthStatusGrpc.Healthy => HealthStatusCore.Healthy,
                HealthStatusGrpc.Degraded => HealthStatusCore.Degraded,
                _ => HealthStatusCore.Unhealthy
            };

            // Module hosts share the single platform database. During a database outage the
            // module's process-level gRPC health would otherwise keep reporting Healthy, so
            // reflect the outage here — the supervisor aggregate then reports modules as
            // Degraded/Unhealthy until the DB recovers (matching plan §11.3).
            var databaseUnavailable = !_dbState.IsAvailable;
            if (databaseUnavailable && mappedStatus != HealthStatusCore.Unhealthy)
            {
                mappedStatus = HealthStatusCore.Degraded;
            }

            return new ModuleHealthEntry
            {
                ModuleId = moduleId,
                ModuleName = moduleName,
                Version = moduleInfo.Version,
                Status = mappedStatus,
                Description = databaseUnavailable
                    ? "Database unavailable — module is degraded."
                    : (string.IsNullOrEmpty(response.Description)
                        ? $"gRPC health check returned {response.Status}"
                        : response.Description),
                ProcessStatus = moduleInfo.Status
            };
        }
        catch (OperationCanceledException)
        {
            return new ModuleHealthEntry
            {
                ModuleId = moduleId,
                ModuleName = moduleName,
                Version = moduleInfo.Version,
                Status = HealthStatusCore.Unhealthy,
                Description = "gRPC health check timed out.",
                ProcessStatus = moduleInfo.Status
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "gRPC health check failed for module {ModuleId}", moduleId);
            return new ModuleHealthEntry
            {
                ModuleId = moduleId,
                ModuleName = moduleName,
                Version = moduleInfo.Version,
                Status = HealthStatusCore.Unhealthy,
                Description = $"gRPC health check failed: {ex.Message}",
                ProcessStatus = moduleInfo.Status
            };
        }
    }

    /// <summary>
    /// Internal result record for a single module's health check.
    /// </summary>
    private sealed class ModuleHealthEntry
    {
        public required string ModuleId { get; init; }
        public required string ModuleName { get; init; }
        public required string Version { get; init; }
        public required HealthStatusCore Status { get; init; }
        public required string Description { get; init; }
        public required ModuleProcessStatus ProcessStatus { get; init; }
    }
}
