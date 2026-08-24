using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Files.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Files.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Files module.
/// Reports module status and basic storage metrics.
/// </summary>
public sealed class FilesHealthCheck : IHealthCheck
{
    private readonly FilesModule _module;
    private readonly FilesDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesHealthCheck"/> class.
    /// </summary>
    public FilesHealthCheck(FilesModule module, FilesDbContext db)
    {
        _module = module;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["module_id"] = _module.Manifest.Id,
            ["version"] = _module.Manifest.Version,
            ["initialized"] = _module.IsInitialized,
            ["running"] = _module.IsRunning
        };

        if (!_module.IsInitialized)
        {
            return HealthCheckResult.Degraded(
                description: "Files module is not initialized",
                data: data);
        }

        if (!_module.IsRunning)
        {
            return HealthCheckResult.Degraded(
                description: "Files module is initialized but not running",
                data: data);
        }

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("Files module database is unreachable.", data: data);
        }

        return HealthCheckResult.Healthy(
            description: "Files module is running",
            data: data);
    }
}
