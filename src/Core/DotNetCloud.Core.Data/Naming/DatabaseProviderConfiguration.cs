namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// Helper methods for parsing and serializing configured database provider values.
/// </summary>
public static class DatabaseProviderConfiguration
{
    /// <summary>
    /// Parse a configured provider value (for example from config or environment).
    /// </summary>
    /// <param name="providerValue">The configured provider value.</param>
    /// <param name="provider">The parsed provider enum when parsing succeeds.</param>
    /// <returns><c>true</c> when parsing succeeds; otherwise <c>false</c>.</returns>
    public static bool TryParseConfiguredProvider(string? providerValue, out DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(providerValue))
        {
            provider = default;
            return false;
        }

        provider = providerValue.Trim().ToLowerInvariant() switch
        {
            "postgresql" or "postgres" or "postgre" => DatabaseProvider.PostgreSQL,
            "sqlserver" or "sql-server" or "mssql" => DatabaseProvider.SqlServer,
            "mariadb" or "mysql" => DatabaseProvider.MariaDB,
            _ => default
        };

        return providerValue.Trim().ToLowerInvariant() is
            "postgresql" or "postgres" or "postgre" or
            "sqlserver" or "sql-server" or "mssql" or
            "mariadb" or "mysql";
    }

    /// <summary>
    /// Convert a provider enum value to the canonical config value.
    /// </summary>
    public static string ToConfigValue(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.PostgreSQL => "PostgreSQL",
            DatabaseProvider.SqlServer => "SqlServer",
            DatabaseProvider.MariaDB => "MariaDB",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
        };
    }
}