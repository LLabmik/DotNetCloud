namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages the lifecycle of a locally-hosted Collabora Online (CODE) process.
/// Only active when <c>CollaboraOptions.UseBuiltInCollabora</c> is enabled.
/// </summary>
public interface ICollaboraProcessManager
{
    /// <summary>
    /// Gets whether the managed Collabora process is currently running.
    /// Returns <c>false</c> when built-in Collabora is not configured.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current status of the managed Collabora process.
    /// </summary>
    CollaboraProcessStatus Status { get; }

    /// <summary>
    /// Gets the number of times the process has been restarted due to crashes.
    /// </summary>
    int RestartCount { get; }
}

/// <summary>
/// Status of the managed Collabora process.
/// </summary>
public enum CollaboraProcessStatus
{
    /// <summary>Built-in Collabora is not configured — an external server is used.</summary>
    NotConfigured,

    /// <summary>The process is starting up.</summary>
    Starting,

    /// <summary>The process is running and healthy.</summary>
    Running,

    /// <summary>The process is running but not responding to health checks.</summary>
    Degraded,

    /// <summary>The process has been stopped.</summary>
    Stopped,

    /// <summary>The process has crashed and will be restarted.</summary>
    Crashed,

    /// <summary>The process has failed too many times and will not be restarted.</summary>
    Failed
}
