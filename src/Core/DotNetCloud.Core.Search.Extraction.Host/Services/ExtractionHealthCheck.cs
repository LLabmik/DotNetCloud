using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Core.Search.Extraction.Host.Services;

/// <summary>
/// Trivial health check for the extraction worker process.
/// The worker is stateless — it is healthy whenever the process is running.
/// </summary>
public sealed class ExtractionHealthCheck : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Extraction worker is running"));
    }
}
