using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// Health check for the Tracks module.
/// </summary>
public sealed class TracksHealthCheck : IHealthCheck
{
    private readonly TracksModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksHealthCheck"/> class.
    /// </summary>
    public TracksHealthCheck(TracksModule module) => _module = module;

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
            return Task.FromResult(HealthCheckResult.Degraded("Tracks module is not initialized", data: data));
        if (!_module.IsRunning)
            return Task.FromResult(HealthCheckResult.Degraded("Tracks module is initialized but not running", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Tracks module is running", data));
    }
}
