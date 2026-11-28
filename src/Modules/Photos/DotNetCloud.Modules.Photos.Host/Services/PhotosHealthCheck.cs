using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Photos.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Photos.Host.Services;

/// <summary>
/// Health check for the Photos module.
/// </summary>
public sealed class PhotosHealthCheck : IHealthCheck
{
    private readonly PhotosModule _module;
    private readonly PhotosDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosHealthCheck"/> class.
    /// </summary>
    public PhotosHealthCheck(PhotosModule module, PhotosDbContext db)
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
            return HealthCheckResult.Degraded("Photos module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Photos module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Photos module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Photos module is running", data);
    }
}
