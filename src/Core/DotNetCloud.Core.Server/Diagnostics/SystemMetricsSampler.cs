using System.Diagnostics;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Samples CPU and memory usage for the core process and every running module host
/// once per second and keeps the latest snapshot for the admin System page.
/// </summary>
/// <remarks>
/// Registered as a singleton <b>and</b> a hosted service so the admin API reads a
/// cached snapshot instead of sampling the process table per request. Sampling is
/// defensive: a single failed process read is skipped and never stops the loop.
/// </remarks>
public sealed class SystemMetricsSampler : BackgroundService
{
    /// <summary>How often the sampler runs.</summary>
    internal static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    /// <summary>Identifier used for the core process in the snapshot.</summary>
    internal const string CoreProcessId = "core";

    private readonly IProcessSupervisor _supervisor;
    private readonly TrafficAccounting _traffic;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SystemMetricsSampler> _logger;
    private readonly Process _coreProcess;
    private readonly DateTimeOffset _startedAtUtc;
    private readonly Dictionary<string, CpuBaseline> _baselines = new(StringComparer.OrdinalIgnoreCase);

    private long? _lastSampleTimestamp;
    private volatile SystemMetricsSnapshot? _latest;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemMetricsSampler"/> class.
    /// </summary>
    public SystemMetricsSampler(
        IProcessSupervisor supervisor,
        TrafficAccounting traffic,
        TimeProvider timeProvider,
        ILogger<SystemMetricsSampler> logger)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _traffic = traffic ?? throw new ArgumentNullException(nameof(traffic));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _coreProcess = Process.GetCurrentProcess();
        _startedAtUtc = TryGetStartTime(_coreProcess) ?? _timeProvider.GetUtcNow();
    }

    /// <summary>Gets the most recent snapshot, or <c>null</c> before the first sample.</summary>
    internal SystemMetricsSnapshot? Latest => _latest;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);
        try
        {
            _latest = Sample();

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    _latest = Sample();
                }
                catch (Exception ex)
                {
                    // A failed sample must never stop the hosted service.
                    _logger.LogWarning(ex, "System metrics sample failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            _coreProcess.Dispose();
        }
    }

    /// <summary>
    /// Captures a new snapshot. Exposed internally so tests can drive sampling
    /// deterministically.
    /// </summary>
    internal SystemMetricsSnapshot Sample()
    {
        // Cheap once a second and keeps the per-user traffic map bounded without a new timer.
        _traffic.PruneIdle();

        var now = _timeProvider.GetUtcNow();
        var timestamp = _timeProvider.GetTimestamp();
        var processorCount = Environment.ProcessorCount;
        var wallDelta = _lastSampleTimestamp is { } previous
            ? _timeProvider.GetElapsedTime(previous, timestamp)
            : TimeSpan.Zero;

        var processes = new List<ProcessMetricsSnapshot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalCpuDelta = TimeSpan.Zero;
        var hasCpuDelta = false;

        // ── Core process ────────────────────────────────────────────────────
        var gcHeap = GC.GetTotalMemory(forceFullCollection: false);
        var (coreSnapshot, coreDelta) = BuildProcessSnapshot(
            CoreProcessId,
            "DotNetCloud",
            TryGetProcessId(_coreProcess),
            TryGetCpuTime(_coreProcess),
            TryGetWorkingSet(_coreProcess),
            gcHeap,
            wallDelta,
            processorCount);

        processes.Add(coreSnapshot);
        seen.Add(CoreProcessId);
        if (coreDelta is { } coreDeltaValue)
        {
            totalCpuDelta += coreDeltaValue;
            hasCpuDelta = true;
        }

        // ── Module hosts ────────────────────────────────────────────────────
        IReadOnlyCollection<ModuleProcessInfo> modules;
        try
        {
            modules = _supervisor.GetAllModuleInfo();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to enumerate module processes for metrics");
            modules = [];
        }

        foreach (var module in modules.OrderBy(m => m.ModuleId, StringComparer.OrdinalIgnoreCase))
        {
            if (module.ProcessId is not { } pid)
            {
                continue;
            }

            var (moduleSnapshot, moduleDelta) = BuildProcessSnapshot(
                module.ModuleId,
                module.ModuleName,
                pid,
                module.CpuTime,
                module.MemoryUsageBytes,
                gcHeapBytes: null,
                wallDelta,
                processorCount);

            processes.Add(moduleSnapshot);
            seen.Add(module.ModuleId);

            if (moduleDelta is { } moduleDeltaValue)
            {
                totalCpuDelta += moduleDeltaValue;
                hasCpuDelta = true;
            }
        }

        // Drop baselines for processes that are no longer running.
        foreach (var staleId in _baselines.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _baselines.Remove(staleId);
        }

        _lastSampleTimestamp = timestamp;

        _traffic.RecordSample(now);
        var (inPerSecond, outPerSecond) = _traffic.GetRates();

        var cpuAvailable = wallDelta > TimeSpan.Zero && hasCpuDelta;

        var snapshot = new SystemMetricsSnapshot
        {
            SampledAtUtc = now,
            StartedAtUtc = _startedAtUtc,
            UptimeSeconds = Math.Max(0, (long)(now - _startedAtUtc).TotalSeconds),
            ProcessorCount = processorCount,
            CpuAvailable = cpuAvailable,
            CpuPercent = cpuAvailable
                ? CpuUsageCalculator.Compute(totalCpuDelta, wallDelta, processorCount)
                : null,
            WorkingSetBytes = processes.Sum(p => p.WorkingSetBytes ?? 0),
            GcHeapBytes = gcHeap,
            Processes = processes,
            InPerSecond = inPerSecond,
            OutPerSecond = outPerSecond,
        };

        _latest = snapshot;
        return snapshot;
    }

    private (ProcessMetricsSnapshot Snapshot, TimeSpan? Delta) BuildProcessSnapshot(
        string id,
        string name,
        int pid,
        TimeSpan? cpuTime,
        long? workingSetBytes,
        long? gcHeapBytes,
        TimeSpan wallDelta,
        int processorCount)
    {
        double? cpuPercent = null;
        TimeSpan? delta = null;

        if (cpuTime is { } cpu)
        {
            if (_baselines.TryGetValue(id, out var baseline) && baseline.Pid == pid)
            {
                var processDelta = cpu - baseline.CpuTime;
                if (processDelta < TimeSpan.Zero)
                {
                    // Counter went backwards (clock skew or a recycled PID) —
                    // report 0 instead of a negative value.
                    processDelta = TimeSpan.Zero;
                }

                delta = processDelta;
                if (wallDelta > TimeSpan.Zero)
                {
                    cpuPercent = CpuUsageCalculator.Compute(processDelta, wallDelta, processorCount);
                }

                baseline.CpuTime = cpu;
            }
            else
            {
                // First sample for this PID (startup or restart) — no delta yet.
                _baselines[id] = new CpuBaseline(pid, cpu);
            }
        }
        else
        {
            _baselines.Remove(id);
        }

        return (new ProcessMetricsSnapshot
        {
            Id = id,
            Name = name,
            Pid = pid,
            CpuPercent = cpuPercent,
            WorkingSetBytes = workingSetBytes,
            GcHeapBytes = gcHeapBytes,
        }, delta);
    }

    private static TimeSpan? TryGetCpuTime(Process process)
    {
        try
        {
            process.Refresh();
            return process.TotalProcessorTime;
        }
        catch
        {
            return null;
        }
    }

    private static long? TryGetWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return null;
        }
    }

    private static int TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTimeOffset? TryGetStartTime(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch
        {
            return null;
        }
    }

    private sealed class CpuBaseline
    {
        public CpuBaseline(int pid, TimeSpan cpuTime)
        {
            Pid = pid;
            CpuTime = cpuTime;
        }

        public int Pid { get; }

        public TimeSpan CpuTime { get; set; }
    }
}
