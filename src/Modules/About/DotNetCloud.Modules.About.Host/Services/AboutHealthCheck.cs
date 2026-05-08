using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.About.Host.Services;

/// <summary>
/// ASP.NET Core health check for the About module.
/// </summary>
public sealed class AboutHealthCheck : IHealthCheck
{
    private readonly AboutModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutHealthCheck"/> class.
    /// </summary>
    public AboutHealthCheck(AboutModule module)
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
            description: "About module is running",
            data: data));
    }
}
