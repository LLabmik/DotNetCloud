using DotNetCloud.Modules.AI.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.AI.Host.Services;

/// <summary>
/// Health check for the AI module.
/// Verifies connectivity to the configured Ollama instance.
/// </summary>
public sealed class AiHealthCheck : IHealthCheck
{
    private readonly IOllamaClient _ollamaClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiHealthCheck"/> class.
    /// </summary>
    public AiHealthCheck(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isHealthy = await _ollamaClient.IsHealthyAsync(cancellationToken);

        return isHealthy
            ? HealthCheckResult.Healthy("Ollama instance is reachable")
            : HealthCheckResult.Degraded("Ollama instance is not reachable");
    }
}
