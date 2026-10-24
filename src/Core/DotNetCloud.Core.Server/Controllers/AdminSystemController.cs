using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Diagnostics;
using DotNetCloud.Core.Services;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Admin System page endpoint: aggregated CPU, memory, traffic, presence and
/// background-task status for the core process and all module hosts.
/// </summary>
/// <remarks>
/// No database access happens on this polling path. CPU/memory come from the cached
/// <see cref="SystemMetricsSampler"/> snapshot, traffic from the process-wide
/// <see cref="TrafficAccounting"/> counters, presence from <see cref="IPresenceTracker"/>
/// and background tasks from the local tracker plus a best-effort gRPC fan-out to each
/// running module host.
/// </remarks>
[ApiController]
[Route("api/v1/core/admin")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminSystemController : ControllerBase
{
    private static readonly TimeSpan ModuleCallTimeout = TimeSpan.FromMilliseconds(1500);

    private readonly SystemMetricsSampler _sampler;
    private readonly TrafficAccounting _traffic;
    private readonly IPresenceTracker _presence;
    private readonly IBackgroundServiceTracker _backgroundServiceTracker;
    private readonly IProcessSupervisor _supervisor;
    private readonly ILogger<AdminSystemController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSystemController"/> class.
    /// </summary>
    public AdminSystemController(
        SystemMetricsSampler sampler,
        TrafficAccounting traffic,
        IPresenceTracker presence,
        IBackgroundServiceTracker backgroundServiceTracker,
        IProcessSupervisor supervisor,
        ILogger<AdminSystemController> logger)
    {
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        _traffic = traffic ?? throw new ArgumentNullException(nameof(traffic));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _backgroundServiceTracker = backgroundServiceTracker ?? throw new ArgumentNullException(nameof(backgroundServiceTracker));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a snapshot of live system metrics.
    /// </summary>
    /// <returns>The system status payload wrapped in the standard API envelope.</returns>
    [HttpGet("system")]
    public async Task<IActionResult> GetSystemStatusAsync()
    {
        var snapshot = _sampler.Latest;
        var now = DateTimeOffset.UtcNow;

        var onlineUsersTask = _presence.GetOnlineUsersAsync();
        var connectionCountTask = _presence.GetActiveConnectionCountAsync();
        var moduleTasksTask = CollectModuleTasksAsync();

        await Task.WhenAll(onlineUsersTask, connectionCountTask, moduleTasksTask).ConfigureAwait(false);

        var (moduleTasks, unavailableModules) = moduleTasksTask.Result;

        var tasks = new List<TaskEntry>();
        tasks.AddRange(_backgroundServiceTracker.GetAll().Values.Select(s => new TaskEntry(
            Module: "core",
            ModuleName: "DotNetCloud",
            Status: s)));
        tasks.AddRange(moduleTasks);

        var orderedTasks = tasks
            .OrderByDescending(t => t.Status.IsRunning)
            .ThenByDescending(t => t.Status.LastRunAt)
            .Select(t => new
            {
                module = t.Module,
                moduleName = t.ModuleName,
                serviceName = t.Status.ServiceName,
                isRunning = t.Status.IsRunning,
                currentRunStartedAt = t.Status.CurrentRunStartedAt,
                lastRunAt = t.Status.LastRunAt,
                lastRunDurationMs = t.Status.LastRunDuration.TotalMilliseconds,
                lastRunSuccess = t.Status.LastRunSuccess,
                lastRunMessage = t.Status.LastRunMessage,
                totalRuns = t.Status.TotalRuns,
                totalFailures = t.Status.TotalFailures,
            })
            .ToList();

        var startedAtUtc = snapshot?.StartedAtUtc ?? _traffic.StartedAtUtc;
        var uptimeSeconds = snapshot?.UptimeSeconds
            ?? Math.Max(0, (long)(now - startedAtUtc).TotalSeconds);

        var payload = new
        {
            sampledAtUtc = snapshot?.SampledAtUtc ?? now,
            startedAtUtc,
            uptimeSeconds,
            processorCount = snapshot?.ProcessorCount ?? Environment.ProcessorCount,
            cpuAvailable = snapshot?.CpuAvailable ?? false,
            cpuPercent = snapshot?.CpuPercent,
            workingSetBytes = snapshot?.WorkingSetBytes ?? 0L,
            gcHeapBytes = snapshot?.GcHeapBytes ?? 0L,
            processes = snapshot?.Processes.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                pid = p.Pid,
                cpuPercent = p.CpuPercent,
                workingSetBytes = p.WorkingSetBytes,
                gcHeapBytes = p.GcHeapBytes,
            }).ToList() ?? [],
            users = new
            {
                online = onlineUsersTask.Result.Count,
                connections = connectionCountTask.Result,
            },
            traffic = new
            {
                bytesIn = _traffic.BytesIn,
                bytesOut = _traffic.BytesOut,
                bytesTotal = _traffic.BytesIn + _traffic.BytesOut,
                inPerSecond = snapshot?.InPerSecond ?? 0d,
                outPerSecond = snapshot?.OutPerSecond ?? 0d,
                sinceUtc = _traffic.StartedAtUtc,
            },
            tasks = orderedTasks,
            unavailableModules,
        };

        return Ok(new { success = true, data = payload });
    }

    private async Task<(List<TaskEntry> Tasks, List<string> UnavailableModules)> CollectModuleTasksAsync()
    {
        IReadOnlyCollection<ModuleProcessInfo> modules;
        try
        {
            modules = _supervisor.GetAllModuleInfo();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to enumerate modules for system status");
            return ([], []);
        }

        var running = modules
            .Where(m => m.Status is ModuleProcessStatus.Running or ModuleProcessStatus.Degraded)
            .Where(m => !string.IsNullOrWhiteSpace(m.GrpcEndpoint))
            .ToList();

        if (running.Count == 0)
        {
            return ([], []);
        }

        var results = await Task.WhenAll(running.Select(FetchModuleTasksAsync)).ConfigureAwait(false);

        var tasks = new List<TaskEntry>();
        var unavailable = new List<string>();

        foreach (var result in results)
        {
            if (result.Available)
            {
                tasks.AddRange(result.Tasks);
            }
            else
            {
                unavailable.Add(result.ModuleId);
            }
        }

        return (tasks, unavailable.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private async Task<ModuleTaskResult> FetchModuleTasksAsync(ModuleProcessInfo module)
    {
        var moduleId = module.ModuleId;

        try
        {
            using var timeout = new CancellationTokenSource(ModuleCallTimeout);
            using var channel = CreateGrpcChannel(module.GrpcEndpoint!);
            var client = new ModuleLifecycle.ModuleLifecycleClient(channel);

            var callOptions = new global::Grpc.Core.CallOptions(
                deadline: DateTime.UtcNow.Add(ModuleCallTimeout),
                cancellationToken: timeout.Token);

            var response = await client
                .GetBackgroundServicesAsync(new GetBackgroundServicesRequest(), callOptions)
                .ConfigureAwait(false);

            var tasks = response.Services
                .Select(s => new TaskEntry(
                    Module: moduleId,
                    ModuleName: module.ModuleName,
                    Status: new BackgroundServiceStatus
                    {
                        ServiceName = s.ServiceName,
                        IsRunning = s.IsRunning,
                        CurrentRunStartedAt = s.CurrentRunStartedAt?.ToDateTimeOffset(),
                        LastRunAt = s.LastRunAt?.ToDateTimeOffset() ?? DateTimeOffset.MinValue,
                        LastRunDuration = TimeSpan.FromMilliseconds(s.LastRunDurationMs),
                        LastRunSuccess = s.LastRunSuccess,
                        LastRunMessage = string.IsNullOrEmpty(s.LastRunMessage) ? null : s.LastRunMessage,
                        TotalRuns = s.TotalRuns,
                        TotalFailures = s.TotalFailures,
                    }))
                .ToList();

            return new ModuleTaskResult(moduleId, Available: true, tasks);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background service query failed for module {ModuleId}", moduleId);
            return new ModuleTaskResult(moduleId, Available: false, []);
        }
    }

    private static GrpcChannel CreateGrpcChannel(string endpoint)
    {
        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(2)
        };

        return GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
        {
            HttpHandler = handler,
            MaxReceiveMessageSize = 1024 * 1024,
            MaxSendMessageSize = 1024 * 1024,
        });
    }

    private sealed record TaskEntry(string Module, string ModuleName, BackgroundServiceStatus Status);

    private sealed record ModuleTaskResult(string ModuleId, bool Available, List<TaskEntry> Tasks);
}
