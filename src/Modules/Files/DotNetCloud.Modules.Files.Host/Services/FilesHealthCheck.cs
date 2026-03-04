using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Files.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Files module.
/// Reports module status and basic storage metrics.
/// </summary>
public sealed class FilesHealthCheck : IHealthCheck
{
    private readonly FilesModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesHealthCheck"/> class.
    /// </summary>
    public FilesHealthCheck(FilesModule module)
    {
        _module = module;
    }

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
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: "Files module is not initialized",
                data: data));
        }

        if (!_module.IsRunning)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: "Files module is initialized but not running",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            description: "Files module is running",
            data: data));
    }
}
