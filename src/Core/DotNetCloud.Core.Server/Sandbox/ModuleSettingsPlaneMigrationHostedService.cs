using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Runs the one-time settings migration for converted modules at startup.
/// </summary>
/// <remarks>
/// Registered after <see cref="ModuleSandboxMigrationHostedService"/> and before the process
/// supervisor, so a module starts with its administrator settings already in the settings plane it
/// reads. Failures never block the host: the module simply keeps falling back to its own defaults, and
/// the next start retries.
/// </remarks>
internal sealed class ModuleSettingsPlaneMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModuleSettingsPlaneMigrationHostedService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleSettingsPlaneMigrationHostedService"/> class.</summary>
    /// <param name="scopeFactory">Creates the scope the database context and settings store come from.</param>
    /// <param name="logger">Logger.</param>
    public ModuleSettingsPlaneMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ModuleSettingsPlaneMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var settings = scope.ServiceProvider.GetRequiredService<IModuleSettingsStore>();

            var report = await ModuleSettingsPlaneMigrator.MigrateAsync(db, settings, cancellationToken);

            if (report.RemovedSystemSettings > 0)
            {
                _logger.LogInformation(
                    "Moved {Copied} module setting(s) into the module settings plane and removed {Removed} stale system setting row(s); {Skipped} needed no copy.",
                    report.Copied,
                    report.RemovedSystemSettings,
                    report.Skipped);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never prevent the host from starting: the module falls back to its own defaults and the
            // migration runs again on the next start.
            _logger.LogError(ex, "Migrating module settings into the module settings plane failed");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
