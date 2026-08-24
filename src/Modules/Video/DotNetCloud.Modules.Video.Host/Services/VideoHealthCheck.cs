using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Video.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Health check for the Video module. Verifies module health and database reachability.
/// </summary>
public sealed class VideoHealthCheck : IHealthCheck
{
    private readonly VideoDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoHealthCheck"/> class.
    /// </summary>
    public VideoHealthCheck(VideoDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("Video module database is unreachable.");
        }

        return HealthCheckResult.Healthy("Video module is healthy.");
    }
}
