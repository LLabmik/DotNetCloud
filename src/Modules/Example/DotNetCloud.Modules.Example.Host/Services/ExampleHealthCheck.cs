using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.Example.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Example.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Example module.
/// Demonstrates how modules implement health checks that the core supervisor monitors.
/// </summary>
public sealed class ExampleHealthCheck : IHealthCheck
{
    private readonly ExampleModule _module;
    private readonly ExampleDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleHealthCheck"/> class.
    /// </summary>
    public ExampleHealthCheck(ExampleModule module, ExampleDbContext db)
    {
        _module = module;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["module_id"] = _module.Manifest.Id,
            ["version"] = _module.Manifest.Version
        };

        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("Example module database is unreachable.", data: data);
        }

        return HealthCheckResult.Healthy(
            description: "Example module is running",
            data: data);
    }
}
