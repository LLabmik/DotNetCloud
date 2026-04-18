using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Photos.Host.Services;

/// <summary>
/// Health check for the Photos module.
/// </summary>
public sealed class PhotosHealthCheck : IHealthCheck
{
    private readonly PhotosModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosHealthCheck"/> class.
    /// </summary>
    public PhotosHealthCheck(PhotosModule module) => _module = module;

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
            return Task.FromResult(HealthCheckResult.Degraded("Photos module is not initialized", data: data));
        if (!_module.IsRunning)
            return Task.FromResult(HealthCheckResult.Degraded("Photos module is initialized but not running", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Photos module is running", data));
    }
}
