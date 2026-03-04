using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Core.ServiceDefaults.HealthChecks;

/// <summary>
/// Health check that reports healthy only after the application has fully started.
/// Used as a readiness probe to prevent traffic routing before initialization is complete.
/// </summary>
public sealed class StartupHealthCheck : IHealthCheck
{
    private volatile bool _isReady;

    /// <summary>
    /// Marks the application as ready to receive traffic.
    /// </summary>
    public void MarkReady() => _isReady = true;

    /// <summary>
    /// Checks whether the application has completed startup.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Healthy if the application is ready; Unhealthy otherwise.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _isReady
            ? HealthCheckResult.Healthy("Application has completed startup.")
            : HealthCheckResult.Unhealthy("Application is still starting up.");

        return Task.FromResult(result);
    }
}
