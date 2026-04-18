using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Music.Host.Services;

/// <summary>
/// Health check for the Music module.
/// </summary>
public sealed class MusicHealthCheck : IHealthCheck
{
    private readonly MusicModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicHealthCheck"/> class.
    /// </summary>
    public MusicHealthCheck(MusicModule module) => _module = module;

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
            return Task.FromResult(HealthCheckResult.Degraded("Music module is not initialized", data: data));
        if (!_module.IsRunning)
            return Task.FromResult(HealthCheckResult.Degraded("Music module is initialized but not running", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Music module is running", data));
    }
}
