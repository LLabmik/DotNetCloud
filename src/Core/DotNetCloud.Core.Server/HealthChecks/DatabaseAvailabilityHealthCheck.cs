using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Core.Server.HealthChecks;

/// <summary>
/// Health check that reports the cached database availability (updated by
/// <see cref="DatabaseReconnectMonitor"/>). Cheap — no live DB query per probe.
/// Registered with the "database" tag so it is included in /health and /health/ready.
/// </summary>
internal sealed class DatabaseAvailabilityHealthCheck : IHealthCheck
{
    private readonly DatabaseConnectivityState _state;

    /// <summary>Creates a health check backed by the given connectivity state.</summary>
    public DatabaseAvailabilityHealthCheck(DatabaseConnectivityState state)
    {
        _state = state;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _state.IsAvailable
            ? HealthCheckResult.Healthy("Database is reachable.")
            : HealthCheckResult.Unhealthy("Database is unreachable.");

        return Task.FromResult(result);
    }
}
