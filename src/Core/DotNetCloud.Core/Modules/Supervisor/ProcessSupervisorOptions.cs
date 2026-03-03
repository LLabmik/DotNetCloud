namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Configuration options for the process supervisor.
/// </summary>
/// <remarks>
/// These options control how the supervisor manages module processes, including
/// health check intervals, shutdown timeouts, and default restart policies.
/// Bind from <c>appsettings.json</c> section <c>"ProcessSupervisor"</c>.
/// </remarks>
public sealed class ProcessSupervisorOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ProcessSupervisor";

    /// <summary>
    /// The directory where module binaries are discovered.
    /// Defaults to <c>"modules"</c> relative to the application root.
    /// </summary>
    public string ModulesDirectory { get; set; } = "modules";

    /// <summary>
    /// Interval between health check probes for each module.
    /// Default: 15 seconds.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum time to wait for a health check response before considering it failed.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of consecutive health check failures before marking a module as unhealthy.
    /// Default: 3.
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Maximum time to wait for a module to shut down gracefully before force-killing.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time to wait for a module process to start and respond to health checks.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Default restart policy for modules that don't specify one.
    /// Default: <see cref="RestartPolicy.ExponentialBackoff"/>.
    /// </summary>
    public RestartPolicy DefaultRestartPolicy { get; set; } = RestartPolicy.ExponentialBackoff;

    /// <summary>
    /// Maximum number of restart attempts before switching to alert-only mode.
    /// Default: 5.
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 5;

    /// <summary>
    /// Initial delay before the first restart attempt (for exponential backoff).
    /// Default: 1 second.
    /// </summary>
    public TimeSpan InitialRestartDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between restart attempts (cap for exponential backoff).
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan MaxRestartDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Duration a module must run successfully before its restart counter resets.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan RestartCounterResetPeriod { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable resource limiting (cgroups on Linux, Job Objects on Windows).
    /// Default: true.
    /// </summary>
    public bool EnableResourceLimits { get; set; } = true;

    /// <summary>
    /// Default memory limit per module in megabytes. 0 means no limit.
    /// Default: 512 MB.
    /// </summary>
    public int DefaultMemoryLimitMb { get; set; } = 512;

    /// <summary>
    /// The base directory for Unix domain sockets (Linux only).
    /// Default: <c>/run/dotnetcloud</c>.
    /// </summary>
    public string UnixSocketDirectory { get; set; } = "/run/dotnetcloud";

    /// <summary>
    /// The named pipe prefix for Windows IPC.
    /// Default: <c>dotnetcloud</c> (results in pipes like <c>\\.\pipe\dotnetcloud-files</c>).
    /// </summary>
    public string NamedPipePrefix { get; set; } = "dotnetcloud";

    /// <summary>
    /// TCP port range start for fallback transport (Docker/Kubernetes).
    /// Default: 50100.
    /// </summary>
    public int TcpPortRangeStart { get; set; } = 50100;

    /// <summary>
    /// TCP port range end for fallback transport (Docker/Kubernetes).
    /// Default: 50200.
    /// </summary>
    public int TcpPortRangeEnd { get; set; } = 50200;

    /// <summary>
    /// Whether to prefer TCP transport over Unix sockets/Named pipes.
    /// Useful in containerized environments.
    /// Default: false.
    /// </summary>
    public bool PreferTcpTransport { get; set; }
}
