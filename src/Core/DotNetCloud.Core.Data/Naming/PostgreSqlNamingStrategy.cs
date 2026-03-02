using System.Text.RegularExpressions;

namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// PostgreSQL naming strategy using schemas for module separation.
/// Schema naming: {module}.* (e.g., core.*, files.*, chat.*)
/// Table naming: lowercase_with_underscores
/// </summary>
public class PostgreSqlNamingStrategy : ITableNamingStrategy
{
    public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

    public string? GetSchemaForModule(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        return moduleName.ToLowerInvariant();
    }

    public string GetTableName(string entityName, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var schema = GetSchemaForModule(moduleName);
        var tableName = ConvertToPascalCaseToSnakeCase(entityName);
        return $"{schema}.{tableName}";
    }

    public string GetColumnName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return ConvertToPascalCaseToSnakeCase(propertyName);
    }

    public string GetIndexName(string tableName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);

        var columnPart = string.Join("_", columnNames.Select(ConvertToPascalCaseToSnakeCase));
        return $"idx_{tableName}_{columnPart}".ToLowerInvariant();
    }

    public string GetForeignKeyName(string sourceTable, string targetTable, string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        return $"fk_{sourceTable}_{targetTable}_{ConvertToPascalCaseToSnakeCase(columnName)}".ToLowerInvariant();
    }

    public string GetUniqueConstraintName(string tableName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);

        var columnPart = string.Join("_", columnNames.Select(ConvertToPascalCaseToSnakeCase));
        return $"uq_{tableName}_{columnPart}".ToLowerInvariant();
    }

    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    private static string ConvertToPascalCaseToSnakeCase(string input)
    {
        // Insert underscore before uppercase letters that follow lowercase letters
        var withUnderscores = Regex.Replace(input, "([a-z])([A-Z])", "$1_$2");
        // Insert underscore before uppercase letters that are followed by lowercase letters and preceded by uppercase
        withUnderscores = Regex.Replace(withUnderscores, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        return withUnderscores.ToLowerInvariant();
    }
}
