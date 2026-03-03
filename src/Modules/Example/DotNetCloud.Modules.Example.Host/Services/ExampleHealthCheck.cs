using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.Example.Host.Services;

/// <summary>
/// ASP.NET Core health check for the Example module.
/// Demonstrates how modules implement health checks that the core supervisor monitors.
/// </summary>
public sealed class ExampleHealthCheck : IHealthCheck
{
    private readonly ExampleModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleHealthCheck"/> class.
    /// </summary>
    public ExampleHealthCheck(ExampleModule module)
    {
        _module = module;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["module_id"] = _module.Manifest.Id,
            ["version"] = _module.Manifest.Version
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            description: "Example module is running",
            data: data));
    }
}
