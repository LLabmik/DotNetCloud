namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// Detects the database provider from a connection string.
/// </summary>
/// <remarks>
/// Detection is based on connection string keywords and patterns:
/// <list type="bullet">
/// <item><description>PostgreSQL: Contains "Host=" or "Server=" with "Port=" or "postgres"</description></item>
/// <item><description>SQL Server: Contains "Data Source=" or "Server=" with "Initial Catalog=" or "sqlserver"</description></item>
/// <item><description>MariaDB: Contains "Server=" with "Database=" or "mysql" or "mariadb"</description></item>
/// </list>
/// </remarks>
public static class DatabaseProviderDetector
{
    /// <summary>
    /// Detects the database provider from a connection string.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The detected database provider.</returns>
    /// <exception cref="ArgumentException">Thrown when the connection string is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider cannot be detected from the connection string.</exception>
    /// <example>
    /// <code>
    /// var provider = DatabaseProviderDetector.DetectProvider("Host=localhost;Database=mydb;Username=user;Password=pass");
    /// // Returns DatabaseProvider.PostgreSQL
    /// </code>
    /// </example>
    public static DatabaseProvider DetectProvider(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var lowerConnectionString = connectionString.ToLowerInvariant();

        // PostgreSQL detection
        if (lowerConnectionString.Contains("postgres") ||
            (lowerConnectionString.Contains("host=") && lowerConnectionString.Contains("port=")) ||
            lowerConnectionString.Contains("npgsql"))
        {
            return DatabaseProvider.PostgreSQL;
        }

        // SQL Server detection
        if (lowerConnectionString.Contains("sqlserver") ||
            lowerConnectionString.Contains("data source=") ||
            lowerConnectionString.Contains("initial catalog=") ||
            lowerConnectionString.Contains("integrated security=") ||
            lowerConnectionString.Contains("trustservercertificate="))
        {
            return DatabaseProvider.SqlServer;
        }

        // MariaDB/MySQL detection
        if (lowerConnectionString.Contains("mysql") ||
            lowerConnectionString.Contains("mariadb") ||
            (lowerConnectionString.Contains("server=") &&
             lowerConnectionString.Contains("database=") &&
             !lowerConnectionString.Contains("data source=")))
        {
            return DatabaseProvider.MariaDB;
        }

        throw new InvalidOperationException(
            $"Could not detect database provider from connection string. " +
            $"Supported providers: PostgreSQL, SQL Server, MariaDB/MySQL. " +
            $"Connection string must contain recognizable provider-specific keywords.");
    }

    /// <summary>
    /// Tries to detect the database provider from a connection string.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="provider">When this method returns, contains the detected provider if successful; otherwise, the default value.</param>
    /// <returns><c>true</c> if the provider was detected successfully; otherwise, <c>false</c>.</returns>
    public static bool TryDetectProvider(string connectionString, out DatabaseProvider provider)
    {
        provider = default;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            provider = DetectProvider(connectionString);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the appropriate naming strategy for the detected provider.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The naming strategy instance for the detected provider.</returns>
    /// <exception cref="ArgumentException">Thrown when the connection string is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider cannot be detected.</exception>
    public static ITableNamingStrategy GetNamingStrategy(string connectionString)
    {
        var provider = DetectProvider(connectionString);
        return GetNamingStrategy(provider);
    }

    /// <summary>
    /// Gets the appropriate naming strategy for a specific provider.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <returns>The naming strategy instance for the provider.</returns>
    public static ITableNamingStrategy GetNamingStrategy(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.PostgreSQL => new PostgreSqlNamingStrategy(),
            DatabaseProvider.SqlServer => new SqlServerNamingStrategy(),
            DatabaseProvider.MariaDB => new MariaDbNamingStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
        };
    }
}
