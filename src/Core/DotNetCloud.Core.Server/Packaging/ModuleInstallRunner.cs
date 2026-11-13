using DotNetCloud.Core;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// The result of starting a freshly installed module.
/// </summary>
/// <param name="IsHealthy">Whether the module reached a healthy running state within the timeout.</param>
/// <param name="Reason">Why it did not, when it did not.</param>
public sealed record ModuleInstallHealth(bool IsHealthy, string? Reason);

/// <summary>
/// Starts and stops module processes for the installer.
/// </summary>
/// <remarks>
/// The installer needs this seam because it runs in two very different hosts: inside the server
/// (where a process supervisor exists and a failed health check must roll the install back) and
/// inside the CLI (where no module process may be spawned — the running server owns them). The CLI
/// implementation reports <see cref="CanStartModules"/> = <see langword="false"/>, so the same
/// state machine records the install and leaves the start to the server's 30-second reconcile.
/// </remarks>
public interface IModuleInstallRunner
{
    /// <summary>Gets whether this process may start module processes.</summary>
    bool CanStartModules { get; }

    /// <summary>
    /// Stops a module if it is running.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a module and waits for it to report healthy.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModuleInstallHealth> StartAndAwaitHealthyAsync(
        string moduleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Starts modules through the running <see cref="IProcessSupervisor"/>.
/// </summary>
public sealed class SupervisorModuleInstallRunner : IModuleInstallRunner
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IProcessSupervisor _supervisor;
    private readonly IOptions<ModuleStoreOptions> _storeOptions;
    private readonly ILogger<SupervisorModuleInstallRunner> _logger;

    /// <summary>Initializes a new instance of the <see cref="SupervisorModuleInstallRunner"/> class.</summary>
    /// <param name="supervisor">The process supervisor.</param>
    /// <param name="storeOptions">Store options, which carry the health-check timeout.</param>
    /// <param name="logger">Logger.</param>
    public SupervisorModuleInstallRunner(
        IProcessSupervisor supervisor,
        IOptions<ModuleStoreOptions> storeOptions,
        ILogger<SupervisorModuleInstallRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        ArgumentNullException.ThrowIfNull(storeOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _supervisor = supervisor;
        _storeOptions = storeOptions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanStartModules => true;

    /// <inheritdoc/>
    public async Task StopAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        await _supervisor.StopModuleAsync(moduleId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ModuleInstallHealth> StartAndAwaitHealthyAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        await _supervisor.StartModuleAsync(moduleId, cancellationToken);

        var timeout = _storeOptions.Value.HealthCheckTimeout;
        var deadline = DateTime.UtcNow + timeout;
        ModuleProcessInfo? last = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            last = _supervisor.GetModuleInfo(moduleId);

            if (last is null)
            {
                return new ModuleInstallHealth(
                    false, "The module was not discovered after install (no executable in the module directory).");
            }

            if (last.Status == ModuleProcessStatus.Running)
            {
                return new ModuleInstallHealth(true, null);
            }

            if (last.Status is ModuleProcessStatus.Failed or ModuleProcessStatus.Crashed)
            {
                return new ModuleInstallHealth(
                    false, last.LastError ?? $"The module process reported {last.Status}.");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        _logger.LogWarning(
            "Module {ModuleId} did not become healthy within {Timeout}",
            LogSanitizer.Sanitize(moduleId), timeout);

        return new ModuleInstallHealth(
            false,
            last is null
                ? $"The module did not start within {timeout.TotalSeconds:0}s."
                : $"The module reported {last.Status} after {timeout.TotalSeconds:0}s.");
    }
}

/// <summary>
/// The CLI implementation: records the install but never spawns a module process.
/// </summary>
/// <remarks>
/// Module processes belong to the running server. The CLI writes the installation record with
/// <c>Status = Enabled</c> (or <c>RestartPending</c> for an upgrade) and the supervisor's 30-second
/// reconcile applies it, so health checking from the CLI would be checking the wrong process.
/// </remarks>
public sealed class OfflineModuleInstallRunner : IModuleInstallRunner
{
    /// <summary>The shared instance.</summary>
    public static readonly OfflineModuleInstallRunner Instance = new();

    /// <inheritdoc/>
    public bool CanStartModules => false;

    /// <inheritdoc/>
    public Task StopAsync(string moduleId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ModuleInstallHealth> StartAndAwaitHealthyAsync(
        string moduleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModuleInstallHealth(
            false, "No supervisor in this process; the running server starts the module within 30 seconds."));
}
