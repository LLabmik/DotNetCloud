namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// Supported database providers for DotNetCloud.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// PostgreSQL database (recommended for production).
    /// Uses schemas for organization and snake_case naming convention.
    /// </summary>
    PostgreSQL,

    /// <summary>
    /// Microsoft SQL Server database.
    /// Uses schemas for organization and PascalCase naming convention.
    /// </summary>
    SqlServer,

    /// <summary>
    /// MariaDB/MySQL database.
    /// Uses table prefixes for organization (no schema support) and snake_case naming convention.
    /// </summary>
    MariaDB
}
