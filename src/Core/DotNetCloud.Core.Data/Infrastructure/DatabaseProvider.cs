namespace DotNetCloud.Core.Data.Naming
{
    /// <summary>
    /// Database-related constant values.
    /// </summary>
    public static class DatabaseConstants
    {
        /// <summary>
        /// Error message used when MariaDB support is requested but not yet available.
        /// MariaDB support is deferred until Pomelo.EntityFrameworkCore.MySql updates to .NET 10.
        /// </summary>
        public const string MariaDbNotSupportedMessage =
            "MariaDB support is temporarily disabled pending Pomelo.EntityFrameworkCore.MySql .NET 10 support update";
    }
}

namespace DotNetCloud.Core.Data.Infrastructure
{
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
}
