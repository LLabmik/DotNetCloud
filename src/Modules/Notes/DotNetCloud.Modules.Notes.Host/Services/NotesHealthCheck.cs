using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Notes.Host.Services;

/// <summary>
/// Health check for the Notes module.
/// </summary>
public sealed class NotesHealthCheck : IHealthCheck
{
    private readonly NotesModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesHealthCheck"/> class.
    /// </summary>
    public NotesHealthCheck(NotesModule module) => _module = module;

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
            return Task.FromResult(HealthCheckResult.Degraded("Notes module is not initialized", data: data));
        if (!_module.IsRunning)
            return Task.FromResult(HealthCheckResult.Degraded("Notes module is initialized but not running", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Notes module is running", data));
    }
}
