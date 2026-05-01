using System.Text.RegularExpressions;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// MariaDB/MySQL naming strategy using table prefixes for module separation.
/// MariaDB doesn't support schemas like PostgreSQL/SQL Server, so we use prefixes instead.
/// Table naming: {module}_{entityname} (e.g., core_ApplicationUser, files_FileEntry)
/// Prefix naming: lowercase module name followed by underscore
/// </summary>
public class MariaDbNamingStrategy : ITableNamingStrategy
{
    public DatabaseProvider Provider => DatabaseProvider.MariaDB;

    public string? GetSchemaForModule(string moduleName)
    {
        // MariaDB doesn't use schemas, return null
        return null;
    }

    public string GetTableName(string entityName, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var prefix = RequiredModules.GetSchemaName(moduleName);
        var tableName = ConvertToPascalCaseToSnakeCase(entityName);
        return $"{prefix}_{tableName}";
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
        // MySQL has a 64 character limit for identifier names, truncate if necessary
        var name = $"idx_{tableName}_{columnPart}".ToLowerInvariant();
        return TruncateIdentifier(name, 64);
    }

    public string GetForeignKeyName(string sourceTable, string targetTable, string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var name = $"fk_{sourceTable}_{targetTable}_{ConvertToPascalCaseToSnakeCase(columnName)}".ToLowerInvariant();
        // MySQL has a 64 character limit for identifier names, truncate if necessary
        return TruncateIdentifier(name, 64);
    }

    public string GetUniqueConstraintName(string tableName, params string[] columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);

        var columnPart = string.Join("_", columnNames.Select(ConvertToPascalCaseToSnakeCase));
        var name = $"uq_{tableName}_{columnPart}".ToLowerInvariant();
        // MySQL has a 64 character limit for identifier names, truncate if necessary
        return TruncateIdentifier(name, 64);
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

    /// <summary>
    /// Truncates an identifier to the specified maximum length.
    /// Uses a hash suffix to ensure uniqueness when truncating.
    /// </summary>
    private static string TruncateIdentifier(string identifier, int maxLength)
    {
        if (identifier.Length <= maxLength)
        {
            return identifier;
        }

        // Create a hash of the full identifier to ensure uniqueness
        using (var hashAlgorithm = System.Security.Cryptography.SHA256.Create())
        {
            var hash = hashAlgorithm.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identifier));
            var hashHex = BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLowerInvariant();

            // Reserve 9 characters for "_" + 8-char hash
            var maxPrefixLength = maxLength - 9;
            var truncated = identifier.Substring(0, maxPrefixLength);
            return $"{truncated}_{hashHex}";
        }
    }
}
