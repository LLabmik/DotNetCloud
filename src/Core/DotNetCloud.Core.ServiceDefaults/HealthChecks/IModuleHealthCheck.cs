namespace DotNetCloud.Core.ServiceDefaults.HealthChecks;

/// <summary>
/// Interface for module-specific health checks.
/// </summary>
public interface IModuleHealthCheck
{
    /// <summary>
    /// Gets the name of the module.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Performs a health check for the module.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the health check result.</returns>
    Task<ModuleHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a module health check.
/// </summary>
public class ModuleHealthCheckResult
{
    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public ModuleHealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the description of the health status.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets additional data about the health check.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Gets or sets the exception if the health check failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    /// <param name="description">Optional description.</param>
    /// <param name="data">Optional data.</param>
    /// <returns>A healthy result.</returns>
    public static ModuleHealthCheckResult Healthy(
        string? description = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return new ModuleHealthCheckResult
        {
            Status = ModuleHealthStatus.Healthy,
            Description = description,
            Data = data
        };
    }

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    /// <param name="description">Optional description.</param>
    /// <param name="data">Optional data.</param>
    /// <returns>A degraded result.</returns>
    public static ModuleHealthCheckResult Degraded(
        string? description = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return new ModuleHealthCheckResult
        {
            Status = ModuleHealthStatus.Degraded,
            Description = description,
            Data = data
        };
    }

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    /// <param name="description">Optional description.</param>
    /// <param name="exception">Optional exception.</param>
    /// <param name="data">Optional data.</param>
    /// <returns>An unhealthy result.</returns>
    public static ModuleHealthCheckResult Unhealthy(
        string? description = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return new ModuleHealthCheckResult
        {
            Status = ModuleHealthStatus.Unhealthy,
            Description = description,
            Exception = exception,
            Data = data
        };
    }
}

/// <summary>
/// Represents the health status of a module.
/// </summary>
public enum ModuleHealthStatus
{
    /// <summary>
    /// The module is healthy.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// The module is degraded but still operational.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// The module is unhealthy.
    /// </summary>
    Unhealthy = 2
}
