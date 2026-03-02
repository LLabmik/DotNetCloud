using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Core.ServiceDefaults.HealthChecks;

/// <summary>
/// Adapter that wraps a module health check as an ASP.NET Core health check.
/// </summary>
public class ModuleHealthCheckAdapter : IHealthCheck
{
    private readonly IModuleHealthCheck _moduleHealthCheck;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleHealthCheckAdapter"/> class.
    /// </summary>
    /// <param name="moduleHealthCheck">The module health check to wrap.</param>
    public ModuleHealthCheckAdapter(IModuleHealthCheck moduleHealthCheck)
    {
        _moduleHealthCheck = moduleHealthCheck ?? throw new ArgumentNullException(nameof(moduleHealthCheck));
    }

    /// <summary>
    /// Performs the health check.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _moduleHealthCheck.CheckHealthAsync(cancellationToken);

            var status = result.Status switch
            {
                ModuleHealthStatus.Healthy => HealthStatus.Healthy,
                ModuleHealthStatus.Degraded => HealthStatus.Degraded,
                ModuleHealthStatus.Unhealthy => HealthStatus.Unhealthy,
                _ => HealthStatus.Unhealthy
            };

            return new HealthCheckResult(
                status,
                result.Description,
                result.Exception,
                result.Data);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                $"Health check for module '{_moduleHealthCheck.ModuleName}' failed",
                ex);
        }
    }
}
