using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Core.ServiceDefaults.HealthChecks;

/// <summary>
/// Database health check implementation for multiple database providers.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory.</param>
    public DatabaseHealthCheck(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Performs the database health check.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result != null && Convert.ToInt32(result) == 1)
            {
                return HealthCheckResult.Healthy("Database connection is healthy");
            }

            return HealthCheckResult.Degraded("Database connection returned unexpected result");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database connection failed",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }
}

/// <summary>
/// Factory interface for creating database connections.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the database connection.</returns>
    Task<System.Data.Common.DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
