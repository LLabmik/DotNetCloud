namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Metrics for a single DotNetCloud process (the core process or a module host).
/// </summary>
public sealed record ProcessMetricsSnapshot
{
    /// <summary>Stable identifier — <c>core</c> or the module ID.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable process name.</summary>
    public required string Name { get; init; }

    /// <summary>Operating-system process ID.</summary>
    public int Pid { get; init; }

    /// <summary>
    /// CPU usage as a percentage of total machine capacity, or <c>null</c> on the
    /// first sample (or immediately after a restart) when no delta is available.
    /// </summary>
    public double? CpuPercent { get; init; }

    /// <summary>Working set in bytes, or <c>null</c> when unavailable.</summary>
    public long? WorkingSetBytes { get; init; }

    /// <summary>
    /// Managed GC heap size in bytes for the core process, or <c>null</c> for
    /// module hosts (which are not sampled in-process).
    /// </summary>
    public long? GcHeapBytes { get; init; }
}

/// <summary>
/// Immutable snapshot of the core process and all running module hosts.
/// Produced once per second by <see cref="SystemMetricsSampler"/>.
/// </summary>
public sealed record SystemMetricsSnapshot
{
    /// <summary>When this snapshot was captured (UTC).</summary>
    public required DateTimeOffset SampledAtUtc { get; init; }

    /// <summary>When the core process started (UTC).</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Core process uptime in seconds.</summary>
    public long UptimeSeconds { get; init; }

    /// <summary>Number of logical processors used as the CPU capacity denominator.</summary>
    public int ProcessorCount { get; init; }

    /// <summary>Whether a CPU delta has been computed (false on the first sample).</summary>
    public bool CpuAvailable { get; init; }

    /// <summary>Aggregate CPU usage across all DotNetCloud processes.</summary>
    public double? CpuPercent { get; init; }

    /// <summary>Aggregate working set across all DotNetCloud processes.</summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>Managed GC heap size of the core process.</summary>
    public long GcHeapBytes { get; init; }

    /// <summary>Per-process metrics, core first then modules ordered by ID.</summary>
    public IReadOnlyList<ProcessMetricsSnapshot> Processes { get; init; } = [];

    /// <summary>Bytes per second received. Zero until enough rate samples exist.</summary>
    public double InPerSecond { get; init; }

    /// <summary>Bytes per second sent. Zero until enough rate samples exist.</summary>
    public double OutPerSecond { get; init; }
}
