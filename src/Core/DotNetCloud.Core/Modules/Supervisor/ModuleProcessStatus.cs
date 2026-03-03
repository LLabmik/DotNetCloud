namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Represents the current state of a module's process within the process supervisor.
/// </summary>
/// <remarks>
/// <para>
/// <b>State Transitions:</b>
/// <code>
/// [Stopped] → Starting → Running → Stopping → [Stopped]
///                ↓                      ↓
///             Failed ←─────────── [Crashed]
///                ↓
///          [Restarting] → Starting → ...
/// </code>
/// </para>
/// </remarks>
public enum ModuleProcessStatus
{
    /// <summary>
    /// The module process is not running. Initial state before first start.
    /// </summary>
    Stopped,

    /// <summary>
    /// The module process is being spawned and initialized.
    /// Transitions to <see cref="Running"/> on success or <see cref="Failed"/> on error.
    /// </summary>
    Starting,

    /// <summary>
    /// The module process is running and healthy.
    /// </summary>
    Running,

    /// <summary>
    /// The module process is being gracefully shut down.
    /// Transitions to <see cref="Stopped"/> on completion.
    /// </summary>
    Stopping,

    /// <summary>
    /// The module process exited unexpectedly (crash, unhandled exception).
    /// The supervisor will apply the configured <see cref="RestartPolicy"/>.
    /// </summary>
    Crashed,

    /// <summary>
    /// The module process failed to start or is being restarted after a crash.
    /// May transition back to <see cref="Starting"/> based on restart policy.
    /// </summary>
    Failed,

    /// <summary>
    /// The module is waiting for a restart attempt according to the configured backoff policy.
    /// </summary>
    WaitingForRestart,

    /// <summary>
    /// The module process is running but health checks are failing.
    /// The supervisor may restart the module if health checks continue to fail.
    /// </summary>
    Degraded
}
