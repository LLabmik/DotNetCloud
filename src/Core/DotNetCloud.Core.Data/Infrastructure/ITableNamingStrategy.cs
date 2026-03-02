namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// Defines a strategy for naming database objects (tables, indexes, foreign keys, constraints)
/// based on the target database provider.
/// </summary>
/// <remarks>
/// Different database systems support different naming conventions and organizational structures:
/// <list type="bullet">
/// <item><description>PostgreSQL: Uses schemas (core.users, files.file_items)</description></item>
/// <item><description>SQL Server: Uses schemas ([core].[Users], [files].[FileItems])</description></item>
/// <item><description>MariaDB/MySQL: Uses table prefixes (core_users, files_file_items)</description></item>
/// </list>
/// </remarks>
public interface ITableNamingStrategy
{
    /// <summary>
    /// Gets the database provider this naming strategy is designed for.
    /// </summary>
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the fully qualified table name for an entity.
    /// </summary>
    /// <param name="moduleName">The module/schema name (e.g., "core", "files").</param>
    /// <param name="entityName">The entity class name (e.g., "ApplicationUser", "FileItem").</param>
    /// <returns>The fully qualified table name (e.g., "core.application_users" or "[core].[ApplicationUsers]").</returns>
    string GetTableName(string moduleName, string entityName);

    /// <summary>
    /// Gets the schema name (if supported by the database provider).
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <returns>The schema name or null if schemas aren't supported.</returns>
    string? GetSchemaName(string moduleName);

    /// <summary>
    /// Gets the name for an index.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="entityName">The entity name.</param>
    /// <param name="columnNames">The indexed column names.</param>
    /// <returns>The index name (e.g., "IX_core_users_email").</returns>
    string GetIndexName(string moduleName, string entityName, params string[] columnNames);

    /// <summary>
    /// Gets the name for a foreign key constraint.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="entityName">The entity name.</param>
    /// <param name="referencedEntityName">The referenced entity name.</param>
    /// <param name="propertyName">The foreign key property name.</param>
    /// <returns>The foreign key constraint name (e.g., "FK_core_teams_core_organizations_organization_id").</returns>
    string GetForeignKeyName(string moduleName, string entityName, string referencedEntityName, string propertyName);

    /// <summary>
    /// Gets the name for a unique constraint.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="entityName">The entity name.</param>
    /// <param name="columnNames">The column names that must be unique.</param>
    /// <returns>The unique constraint name (e.g., "UQ_core_users_email").</returns>
    string GetUniqueConstraintName(string moduleName, string entityName, params string[] columnNames);

    /// <summary>
    /// Converts a C# property name to a database column name following the provider's conventions.
    /// </summary>
    /// <param name="propertyName">The C# property name (e.g., "DisplayName").</param>
    /// <returns>The column name (e.g., "display_name" or "DisplayName").</returns>
    string ToColumnName(string propertyName);

    /// <summary>
    /// Converts a C# entity name to a table name following the provider's conventions.
    /// </summary>
    /// <param name="entityName">The C# entity name (e.g., "ApplicationUser").</param>
    /// <returns>The table name (e.g., "application_users" or "ApplicationUsers").</returns>
    string ToTableName(string entityName);
}
