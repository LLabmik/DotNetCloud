using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Search.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Search.Host.Services;

/// <summary>
/// Health check for the Search module.
/// </summary>
public sealed class SearchHealthCheck : IHealthCheck
{
    private readonly SearchModule _module;
    private readonly SearchDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchHealthCheck"/> class.
    /// </summary>
    public SearchHealthCheck(SearchModule module, SearchDbContext db)
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
            return HealthCheckResult.Degraded("Search module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Search module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Search module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Search module is running", data);
    }
}
