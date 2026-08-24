using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Music.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Music.Host.Services;

/// <summary>
/// Health check for the Music module.
/// </summary>
public sealed class MusicHealthCheck : IHealthCheck
{
    private readonly MusicModule _module;
    private readonly MusicDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicHealthCheck"/> class.
    /// </summary>
    public MusicHealthCheck(MusicModule module, MusicDbContext db)
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
            return HealthCheckResult.Degraded("Music module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Music module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Music module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Music module is running", data);
    }
}
