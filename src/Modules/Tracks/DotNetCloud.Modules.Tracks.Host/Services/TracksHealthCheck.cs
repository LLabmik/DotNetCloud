using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Tracks.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// Health check for the Tracks module.
/// </summary>
public sealed class TracksHealthCheck : IHealthCheck
{
    private readonly TracksModule _module;
    private readonly TracksDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksHealthCheck"/> class.
    /// </summary>
    public TracksHealthCheck(TracksModule module, TracksDbContext db)
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
            return HealthCheckResult.Degraded("Tracks module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Tracks module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Tracks module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Tracks module is running", data);
    }
}
