using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Bookmarks.Host.Services;

/// <summary>
/// Health check for the Bookmarks module.
/// </summary>
public sealed class BookmarksHealthCheck : IHealthCheck
{
    private readonly BookmarksModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksHealthCheck"/> class.
    /// </summary>
    public BookmarksHealthCheck(BookmarksModule module) => _module = module;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
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
            return Task.FromResult(HealthCheckResult.Degraded("Bookmarks module is not initialized", data: data));
        if (!_module.IsRunning)
            return Task.FromResult(HealthCheckResult.Degraded("Bookmarks module is initialized but not running", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Bookmarks module is running", data));
    }
}
