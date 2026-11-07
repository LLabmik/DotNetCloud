using DotNetCloud.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Runs module-schema adoption once at startup, before the process supervisor spawns any module.
/// </summary>
/// <remarks>
/// Registered before <c>ProcessSupervisor</c> so its <see cref="StartAsync"/> completes first:
/// hosted services start sequentially, and a module must never be spawned against a schema whose
/// ownership has not been handed over yet. Every failure is logged and swallowed — a blocked
/// module is held with <c>SandboxStatus = Error</c>/<c>LegacyOverride</c> by the adoption service,
/// and the server must still boot so the operator can fix the prerequisite.
/// </remarks>
internal sealed class ModuleSandboxMigrationHostedService : IHostedService
{
    private readonly ModuleAdoptionService _adoption;
    private readonly ILogger<ModuleSandboxMigrationHostedService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleSandboxMigrationHostedService"/> class.</summary>
    /// <param name="adoption">The adoption service.</param>
    /// <param name="logger">Logger.</param>
    public ModuleSandboxMigrationHostedService(
        ModuleAdoptionService adoption,
        ILogger<ModuleSandboxMigrationHostedService> logger)
    {
        _adoption = adoption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var report = await _adoption.AdoptInstalledModulesAsync(cancellationToken);

            if (report.HasBlockingProblems)
            {
                _logger.LogWarning(
                    "Module schema adoption finished with blocking problems. Fix the reported prerequisites and restart the server.");
            }
            else
            {
                _logger.LogInformation("Module schema adoption check completed");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never prevent the host from starting: modules are refused individually.
            _logger.LogError(ex, "Module schema adoption failed");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
