namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// Strategy interface for mapping entity types and property names to database table and column names.
/// Different database providers (PostgreSQL, SQL Server, MariaDB) have different naming conventions and constraints.
/// </summary>
public interface ITableNamingStrategy
{
    /// <summary>
    /// Gets the database provider type this strategy is for.
    /// </summary>
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the schema name for the given module.
    /// </summary>
    /// <param name="moduleName">The module name (e.g., "core", "files", "chat")</param>
    /// <returns>The schema name to use, or null if the database provider doesn't support schemas</returns>
    string? GetSchemaForModule(string moduleName);

    /// <summary>
    /// Gets the table name for an entity, applying provider-specific naming conventions.
    /// </summary>
    /// <param name="entityName">The entity class name (e.g., "ApplicationUser")</param>
    /// <param name="moduleName">The module owning the entity (e.g., "core")</param>
    /// <returns>The full table name to use in the database</returns>
    string GetTableName(string entityName, string moduleName);

    /// <summary>
    /// Gets the column name for a property, applying provider-specific naming conventions.
    /// </summary>
    /// <param name="propertyName">The property name (e.g., "CreatedAt")</param>
    /// <returns>The column name to use in the database</returns>
    string GetColumnName(string propertyName);

    /// <summary>
    /// Gets the index name for a database index, applying provider-specific naming conventions.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="columnNames">The column names being indexed</param>
    /// <returns>The index name to use in the database</returns>
    string GetIndexName(string tableName, params string[] columnNames);

    /// <summary>
    /// Gets the foreign key name for a relationship, applying provider-specific naming conventions.
    /// </summary>
    /// <param name="sourceTable">The source table name</param>
    /// <param name="targetTable">The target table name</param>
    /// <param name="columnName">The foreign key column name</param>
    /// <returns>The foreign key name to use in the database</returns>
    string GetForeignKeyName(string sourceTable, string targetTable, string columnName);

    /// <summary>
    /// Gets the constraint name for a unique constraint, applying provider-specific naming conventions.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="columnNames">The column names in the constraint</param>
    /// <returns>The constraint name to use in the database</returns>
    string GetUniqueConstraintName(string tableName, params string[] columnNames);
}
