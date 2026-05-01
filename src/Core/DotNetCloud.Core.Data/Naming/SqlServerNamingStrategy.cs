using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// SQL Server naming strategy using schemas for module separation.
/// Schema naming: [module] (e.g., [core], [files], [chat])
/// Table naming: PascalCase (standard .NET convention)
/// </summary>
public class SqlServerNamingStrategy : ITableNamingStrategy
{
    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public string? GetSchemaForModule(string moduleName)
    {
        return RequiredModules.GetSchemaName(moduleName);
    }

    public string GetTableName(string entityName, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var schema = GetSchemaForModule(moduleName);
        return $"[{schema}].[{entityName}]";
    }

    public string GetColumnName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        // SQL Server typically uses PascalCase for column names
        return propertyName;
    }

    public string GetIndexName(string tableName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);

        var columnPart = string.Join("_", columnNames);
        return $"IX_{tableName}_{columnPart}";
    }

    public string GetForeignKeyName(string sourceTable, string targetTable, string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        return $"FK_{sourceTable}_{targetTable}_{columnName}";
    }

    public string GetUniqueConstraintName(string tableName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);

        var columnPart = string.Join("_", columnNames);
        return $"UQ_{tableName}_{columnPart}";
    }
}
