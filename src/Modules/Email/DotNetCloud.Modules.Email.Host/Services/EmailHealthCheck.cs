using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Email.Host.Services;

/// <summary>
/// Health check for the Email module.
/// </summary>
public sealed class EmailHealthCheck : IHealthCheck
{
    private readonly EmailModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailHealthCheck"/> class.
    /// </summary>
    public EmailHealthCheck(EmailModule module) => _module = module;

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
            return Task.FromResult(HealthCheckResult.Degraded("Email module is not initialized", data: data));
        if (!_module.IsRunning)
            return Task.FromResult(HealthCheckResult.Degraded("Email module is initialized but not running", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Email module is running", data));
    }
}
