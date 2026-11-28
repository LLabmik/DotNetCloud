using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Modules.AI.Host.Services;

/// <summary>
/// Health check for the AI module.
/// Verifies connectivity to the configured Ollama instance and database reachability.
/// </summary>
public sealed class AiHealthCheck : IHealthCheck
{
    private readonly IOllamaClient _ollamaClient;
    private readonly AiDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiHealthCheck"/> class.
    /// </summary>
    public AiHealthCheck(IOllamaClient ollamaClient, AiDbContext db)
    {
        _ollamaClient = ollamaClient;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Database reachability probe — a dead DB degrades the module without crashing it.
        if (!await DbHealthProbe.CanConnectAsync(_db, cancellationToken).ConfigureAwait(false))
        {
            return HealthCheckResult.Unhealthy("AI module database is unreachable.");
        }

        try
        {
            var isHealthy = await _ollamaClient.IsHealthyAsync(cancellationToken).ConfigureAwait(false);

            return isHealthy
                ? HealthCheckResult.Healthy("Ollama instance is reachable")
                : HealthCheckResult.Degraded("Ollama instance is not reachable");
        }
        catch (Exception ex)
        {
            // Never rethrow — a probe failure must not crash the module process.
            return HealthCheckResult.Degraded(
                description: "Ollama health probe failed",
                exception: ex);
        }
    }
}
