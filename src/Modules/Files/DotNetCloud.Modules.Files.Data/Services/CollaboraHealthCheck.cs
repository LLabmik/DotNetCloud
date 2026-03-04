using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Health check that verifies Collabora Online is reachable and responding to WOPI discovery.
/// </summary>
internal sealed class CollaboraHealthCheck : IHealthCheck
{
    private readonly ICollaboraDiscoveryService _discoveryService;

    public CollaboraHealthCheck(ICollaboraDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _discoveryService.IsAvailableAsync(cancellationToken);

            return isAvailable
                ? HealthCheckResult.Healthy("Collabora Online is available and responding.")
                : HealthCheckResult.Degraded("Collabora Online is not available. Document editing is disabled.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check Collabora Online health.", ex);
        }
    }
}
