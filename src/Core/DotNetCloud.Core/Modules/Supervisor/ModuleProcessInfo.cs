namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Contains runtime information about a supervised module process.
/// </summary>
/// <remarks>
/// This record is maintained by the <see cref="IProcessSupervisor"/> and provides
/// a snapshot of a module's process state, including health status, restart history,
/// and resource usage.
/// </remarks>
public sealed record ModuleProcessInfo
{
    /// <summary>
    /// The unique module identifier (e.g., "dotnetcloud.files").
    /// </summary>
    public required string ModuleId { get; init; }

    /// <summary>
    /// The human-readable module name (e.g., "Files").
    /// </summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// The semantic version of the running module.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The current status of the module's process.
    /// </summary>
    public required ModuleProcessStatus Status { get; init; }

    /// <summary>
    /// The OS process ID of the running module, or null if not running.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// The gRPC endpoint address for communicating with this module
    /// (e.g., "unix:///run/dotnetcloud/files.sock" or "pipe://dotnetcloud-files").
    /// </summary>
    public string? GrpcEndpoint { get; init; }

    /// <summary>
    /// The configured restart policy for this module.
    /// </summary>
    public required RestartPolicy RestartPolicy { get; init; }

    /// <summary>
    /// When the module process was last started.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// When the last successful health check was received.
    /// </summary>
    public DateTime? LastHealthCheckAt { get; init; }

    /// <summary>
    /// The number of consecutive restart attempts since the last successful run.
    /// Resets to zero when the module runs successfully for the configured stability period.
    /// </summary>
    public int ConsecutiveRestarts { get; init; }

    /// <summary>
    /// The maximum number of restart attempts before giving up (alert-only mode).
    /// </summary>
    public int MaxRestartAttempts { get; init; } = 5;

    /// <summary>
    /// The total number of times this module has been restarted since the supervisor started.
    /// </summary>
    public long TotalRestarts { get; init; }

    /// <summary>
    /// Current memory usage of the module process in bytes, or null if not available.
    /// </summary>
    public long? MemoryUsageBytes { get; init; }

    /// <summary>
    /// The configured memory limit in bytes, or null if no limit is set.
    /// </summary>
    public long? MemoryLimitBytes { get; init; }

    /// <summary>
    /// Optional error message from the last failure.
    /// </summary>
    public string? LastError { get; init; }
}
