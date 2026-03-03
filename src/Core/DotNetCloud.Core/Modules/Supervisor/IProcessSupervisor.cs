namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Manages the lifecycle of module processes in the DotNetCloud system.
/// </summary>
/// <remarks>
/// <para>
/// The process supervisor is responsible for:
/// <list type="bullet">
///   <item><description>Spawning module processes on system startup</description></item>
///   <item><description>Monitoring module health via periodic gRPC health checks</description></item>
///   <item><description>Applying restart policies when modules crash or become unhealthy</description></item>
///   <item><description>Gracefully shutting down all modules on system stop</description></item>
///   <item><description>Enforcing resource limits (memory, CPU) per module</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Architecture:</b>
/// <code>
/// dotnetcloud (core process — supervisor)
/// ├── dotnetcloud-module files      (child process, gRPC)
/// ├── dotnetcloud-module chat       (child process, gRPC)
/// ├── dotnetcloud-module calendar   (child process, gRPC)
/// └── dotnetcloud-module music      (child process, gRPC)
/// </code>
/// Each module runs in its own process, communicating with the core via gRPC
/// over Unix domain sockets (Linux) or Named Pipes (Windows).
/// </para>
/// </remarks>
public interface IProcessSupervisor
{
    /// <summary>
    /// Starts all enabled modules by spawning their processes.
    /// Called during application startup after database initialization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the startup operation.</param>
    /// <returns>A task representing the asynchronous startup operation.</returns>
    Task StartAllModulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops all running module processes.
    /// Signals each module to stop, waits for graceful termination, then force-kills if needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the shutdown operation.</param>
    /// <returns>A task representing the asynchronous shutdown operation.</returns>
    Task StopAllModulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a specific module by spawning its process.
    /// </summary>
    /// <param name="moduleId">The unique module identifier (e.g., "dotnetcloud.files").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    Task StartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops a specific module process.
    /// </summary>
    /// <param name="moduleId">The unique module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a specific module by stopping and starting it.
    /// </summary>
    /// <param name="moduleId">The unique module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous restart operation.</returns>
    Task RestartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current process information for a specific module.
    /// </summary>
    /// <param name="moduleId">The unique module identifier.</param>
    /// <returns>The module's process info, or null if the module is not known.</returns>
    ModuleProcessInfo? GetModuleInfo(string moduleId);

    /// <summary>
    /// Gets process information for all known modules.
    /// </summary>
    /// <returns>A read-only collection of module process information.</returns>
    IReadOnlyCollection<ModuleProcessInfo> GetAllModuleInfo();

    /// <summary>
    /// Checks whether a specific module's process is currently running and healthy.
    /// </summary>
    /// <param name="moduleId">The unique module identifier.</param>
    /// <returns>True if the module is running and healthy; false otherwise.</returns>
    bool IsModuleRunning(string moduleId);
}
