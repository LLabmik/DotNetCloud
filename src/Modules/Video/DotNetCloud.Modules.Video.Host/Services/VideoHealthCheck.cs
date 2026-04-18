using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Health check for the Video module.
/// </summary>
public sealed class VideoHealthCheck : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // In a real deployment, this could verify DB connectivity, disk space, etc.
        return Task.FromResult(HealthCheckResult.Healthy("Video module is healthy."));
    }
}
