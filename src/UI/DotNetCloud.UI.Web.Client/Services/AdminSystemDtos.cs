namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// Snapshot returned by <c>GET /api/v1/core/admin/system</c> for the admin System page.
/// </summary>
public sealed class AdminSystemStatusDto
{
    /// <summary>When the server captured the snapshot (UTC).</summary>
    public DateTimeOffset SampledAtUtc { get; set; }

    /// <summary>When the core process started (UTC).</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>Core process uptime in seconds.</summary>
    public long UptimeSeconds { get; set; }

    /// <summary>Logical processor count used as the CPU capacity denominator.</summary>
    public int ProcessorCount { get; set; }

    /// <summary>Whether a valid CPU delta exists (false for the first sample).</summary>
    public bool CpuAvailable { get; set; }

    /// <summary>Aggregate CPU usage across all DotNetCloud processes.</summary>
    public double? CpuPercent { get; set; }

    /// <summary>Aggregate working set across all DotNetCloud processes.</summary>
    public long WorkingSetBytes { get; set; }

    /// <summary>Managed GC heap size of the core process.</summary>
    public long GcHeapBytes { get; set; }

    /// <summary>Per-process metrics, core first then module hosts.</summary>
    public List<AdminSystemProcessDto> Processes { get; set; } = [];

    /// <summary>Presence summary.</summary>
    public AdminSystemUsersDto Users { get; set; } = new();

    /// <summary>Traffic counters since the core process started.</summary>
    public AdminSystemTrafficDto Traffic { get; set; } = new();

    /// <summary>Per-user traffic for users that are currently active.</summary>
    public AdminSystemUsersTrafficDto UsersTraffic { get; set; } = new();

    /// <summary>Background tasks across the core and module hosts.</summary>
    public List<AdminSystemTaskDto> Tasks { get; set; } = [];

    /// <summary>Modules whose background-service query timed out or failed.</summary>
    public List<string> UnavailableModules { get; set; } = [];
}

/// <summary>Metrics for one DotNetCloud process.</summary>
public sealed class AdminSystemProcessDto
{
    /// <summary>Stable identifier — <c>core</c> or the module ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable process name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Operating-system process ID.</summary>
    public int Pid { get; set; }

    /// <summary>CPU usage percentage, or null until a delta exists.</summary>
    public double? CpuPercent { get; set; }

    /// <summary>Working set in bytes.</summary>
    public long? WorkingSetBytes { get; set; }

    /// <summary>Managed GC heap size (core process only).</summary>
    public long? GcHeapBytes { get; set; }
}

/// <summary>Online-user counts.</summary>
public sealed class AdminSystemUsersDto
{
    /// <summary>Distinct users with an active SignalR connection.</summary>
    public int Online { get; set; }

    /// <summary>Total active connections across all users.</summary>
    public int Connections { get; set; }
}

/// <summary>Global traffic counters.</summary>
public sealed class AdminSystemTrafficDto
{
    /// <summary>Bytes received since the core process started.</summary>
    public long BytesIn { get; set; }

    /// <summary>Bytes sent since the core process started.</summary>
    public long BytesOut { get; set; }

    /// <summary>Total bytes in both directions.</summary>
    public long BytesTotal { get; set; }

    /// <summary>Average bytes received per second over the recent window.</summary>
    public double InPerSecond { get; set; }

    /// <summary>Average bytes sent per second over the recent window.</summary>
    public double OutPerSecond { get; set; }

    /// <summary>When counting started (core process start).</summary>
    public DateTimeOffset SinceUtc { get; set; }
}

/// <summary>Run status of one background task.</summary>
public sealed class AdminSystemTaskDto
{
    /// <summary>Owning module ID, or <c>core</c>.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Owning module display name.</summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>Human-readable service name.</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Whether a cycle is currently executing.</summary>
    public bool IsRunning { get; set; }

    /// <summary>When the currently running cycle started.</summary>
    public DateTimeOffset? CurrentRunStartedAt { get; set; }

    /// <summary>When the last cycle completed.</summary>
    public DateTimeOffset LastRunAt { get; set; }

    /// <summary>Duration of the last cycle in milliseconds.</summary>
    public double LastRunDurationMs { get; set; }

    /// <summary>Whether the last cycle succeeded.</summary>
    public bool LastRunSuccess { get; set; }

    /// <summary>Optional result message from the last run.</summary>
    public string? LastRunMessage { get; set; }

    /// <summary>Total successful cycles since startup.</summary>
    public int TotalRuns { get; set; }

    /// <summary>Total failed cycles since startup.</summary>
    public int TotalFailures { get; set; }
}

/// <summary>
/// Per-user traffic rows for users with tracked traffic in this process lifetime.
/// </summary>
/// <remarks>
/// Counters are live and in-memory: a user who goes quiet is dropped after the idle grace
/// window and restarts at zero when they return, so these numbers are "since this session
/// became active", not a cumulative total.
/// </remarks>
public sealed class AdminSystemUsersTrafficDto
{
    /// <summary>Total number of users currently tracked (before truncation).</summary>
    public int Total { get; set; }

    /// <summary>Number of rows actually returned.</summary>
    public int Showing { get; set; }

    /// <summary>Top talkers, ordered by total bytes descending.</summary>
    public List<AdminSystemUserTrafficDto> Entries { get; set; } = [];
}

/// <summary>Traffic for one user while they are active.</summary>
public sealed class AdminSystemUserTrafficDto
{
    /// <summary>The user ID.</summary>
    public Guid UserId { get; set; }

    /// <summary>Resolved display name, or the user ID when the lookup failed.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Bytes received from the user in this session.</summary>
    public long BytesIn { get; set; }

    /// <summary>Bytes sent to the user in this session.</summary>
    public long BytesOut { get; set; }

    /// <summary>Total bytes in both directions.</summary>
    public long BytesTotal { get; set; }

    /// <summary>When the user last moved a quantum of traffic (UTC).</summary>
    public DateTimeOffset LastActivityUtc { get; set; }

    /// <summary>Whether the user has been quiet past the idle threshold.</summary>
    public bool IsIdle { get; set; }

    /// <summary>
    /// SignalR-derived presence: <c>Online</c>, <c>Away</c>, <c>DoNotDisturb</c> or
    /// <c>NotConnected</c>. Display-only — never used for counting or expiry.
    /// </summary>
    public string PresenceState { get; set; } = string.Empty;
}
