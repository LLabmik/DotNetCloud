using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Bookmarks.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Bookmarks.Host.Services;

/// <summary>
/// Health check for the Bookmarks module.
/// </summary>
public sealed class BookmarksHealthCheck : IHealthCheck
{
    private readonly BookmarksModule _module;
    private readonly BookmarksDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksHealthCheck"/> class.
    /// </summary>
    public BookmarksHealthCheck(BookmarksModule module, BookmarksDbContext db)
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
            return HealthCheckResult.Degraded("Bookmarks module is not initialized", data: data);
        if (!_module.IsRunning)
            return HealthCheckResult.Degraded("Bookmarks module is initialized but not running", data: data);

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
            return HealthCheckResult.Unhealthy("Bookmarks module database is unreachable.", data: data);

        return HealthCheckResult.Healthy("Bookmarks module is running", data);
    }
}
