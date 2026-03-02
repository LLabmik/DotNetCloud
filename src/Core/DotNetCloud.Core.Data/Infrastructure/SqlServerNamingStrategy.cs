using System.Text;

namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// SQL Server table naming strategy using schemas and PascalCase naming convention.
/// </summary>
/// <remarks>
/// SQL Server supports schemas natively, so each module gets its own schema:
/// <list type="bullet">
/// <item><description>[core].[ApplicationUsers]</description></item>
/// <item><description>[files].[FileItems]</description></item>
/// <item><description>[chat].[Messages]</description></item>
/// </list>
/// <para>
/// Follows SQL Server best practices:
/// <list type="bullet">
/// <item><description>PascalCase identifiers (standard .NET convention)</description></item>
/// <item><description>Schema brackets for escaping reserved words</description></item>
/// <item><description>Descriptive constraint names</description></item>
/// </list>
/// </para>
/// </remarks>
public class SqlServerNamingStrategy : ITableNamingStrategy
{
    /// <inheritdoc />
    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    /// <inheritdoc />
    public string GetTableName(string moduleName, string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        var schema = moduleName.ToLowerInvariant();
        var table = ToTableName(entityName);
        return $"[{schema}].[{table}]";
    }

    /// <inheritdoc />
    public string? GetSchemaName(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        return moduleName.ToLowerInvariant();
    }

    /// <inheritdoc />
    public string GetIndexName(string moduleName, string entityName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one column name must be specified.", nameof(columnNames));
        }

        var schema = moduleName.ToLowerInvariant();
        var table = ToTableName(entityName);
        var columns = string.Join("_", columnNames);

        return $"IX_{schema}_{table}_{columns}";
    }

    /// <inheritdoc />
    public string GetForeignKeyName(string moduleName, string entityName, string referencedEntityName, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencedEntityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var schema = moduleName.ToLowerInvariant();
        var table = ToTableName(entityName);
        var refTable = ToTableName(referencedEntityName);
        var prop = propertyName;

        return $"FK_{schema}_{table}_{refTable}_{prop}";
    }

    /// <inheritdoc />
    public string GetUniqueConstraintName(string moduleName, string entityName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Length == 0)
        {
            throw new ArgumentException("At least one column name must be specified.", nameof(columnNames));
        }

        var schema = moduleName.ToLowerInvariant();
        var table = ToTableName(entityName);
        var columns = string.Join("_", columnNames);

        return $"UQ_{schema}_{table}_{columns}";
    }

    /// <inheritdoc />
    public string ToColumnName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        // SQL Server uses PascalCase by convention
        return propertyName;
    }

    /// <inheritdoc />
    public string ToTableName(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        // SQL Server uses PascalCase by convention
        return entityName;
    }
}
