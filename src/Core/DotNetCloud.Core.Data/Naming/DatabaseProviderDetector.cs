using System.Data.Common;

namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// Detects the database provider type from a connection string.
/// </summary>
public static class DatabaseProviderDetector
{
    /// <summary>
    /// Detects the database provider from a connection string.
    /// </summary>
    /// <param name="connectionString">The database connection string</param>
    /// <returns>The detected database provider</returns>
    /// <exception cref="InvalidOperationException">If the provider cannot be determined from the connection string</exception>
    public static DatabaseProvider DetectProvider(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Check for PostgreSQL connection string patterns
        if (IsPostgreSqlConnectionString(connectionString))
        {
            return DatabaseProvider.PostgreSQL;
        }

        // Check for SQL Server connection string patterns
        if (IsSqlServerConnectionString(connectionString))
        {
            return DatabaseProvider.SqlServer;
        }

        throw new InvalidOperationException(
            $"Unable to determine database provider from connection string. " +
            $"Connection string should contain 'Server=' (SQL Server/PostgreSQL) or 'Data Source=' (SQL Server).");
    }

    /// <summary>
    /// Gets the naming strategy instance for the given provider.
    /// </summary>
    /// <param name="provider">The database provider</param>
    /// <returns>An instance of the appropriate naming strategy</returns>
    public static ITableNamingStrategy GetNamingStrategy(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.PostgreSQL => new PostgreSqlNamingStrategy(),
            DatabaseProvider.SqlServer => new SqlServerNamingStrategy(),
            _ => throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider))
        };
    }

    private static bool IsPostgreSqlConnectionString(string connectionString)
    {
        // PostgreSQL connection string patterns:
        // - Contains "Host=" or "host="
        // - Contains "Database=" or "database="
        // - Port 5432 (default) or specified port
        var lowerConnectionString = connectionString.ToLowerInvariant();
        return lowerConnectionString.Contains("host=") || lowerConnectionString.Contains("server=postgresql");
    }

    private static bool IsSqlServerConnectionString(string connectionString)
    {
        // SQL Server connection string patterns:
        // - Contains "Data Source=" or "Server=" with .mssql or SQL Server indicators
        // - Contains "Initial Catalog=" or "Database="
        // - Port 1433 (default) or specified port
        var lowerConnectionString = connectionString.ToLowerInvariant();

        // SQL Server-specific keywords take priority
        if (lowerConnectionString.Contains("data source=") ||
            lowerConnectionString.Contains("initial catalog=") ||
            lowerConnectionString.Contains("integrated security=") ||
            lowerConnectionString.Contains("trustservercertificate=") ||
            lowerConnectionString.Contains("multipleactiveresultsets="))
        {
            return true;
        }

        // Generic "Server=" + "Database=" pattern
        if (lowerConnectionString.Contains("server=") &&
            !lowerConnectionString.Contains("host=") &&
            !lowerConnectionString.Contains("server=postgresql"))
        {
            return true;
        }

        return false;
    }
}
