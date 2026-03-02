using System.Text;

namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// PostgreSQL table naming strategy using schemas and snake_case naming convention.
/// </summary>
/// <remarks>
/// PostgreSQL supports schemas natively, so each module gets its own schema:
/// <list type="bullet">
/// <item><description>core.application_users</description></item>
/// <item><description>files.file_items</description></item>
/// <item><description>chat.messages</description></item>
/// </list>
/// <para>
/// Follows PostgreSQL best practices:
/// <list type="bullet">
/// <item><description>Lowercase identifiers (case-insensitive without quotes)</description></item>
/// <item><description>Snake_case for multi-word names</description></item>
/// <item><description>Descriptive constraint names</description></item>
/// </list>
/// </para>
/// </remarks>
public class PostgreSqlNamingStrategy : ITableNamingStrategy
{
    /// <inheritdoc />
    public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

    /// <inheritdoc />
    public string GetTableName(string moduleName, string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        var schema = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        return $"{schema}.{table}";
    }

    /// <inheritdoc />
    public string? GetSchemaName(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        return ToSnakeCase(moduleName);
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

        var schema = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        var columns = string.Join("_", columnNames.Select(ToSnakeCase));

        return $"ix_{schema}_{table}_{columns}";
    }

    /// <inheritdoc />
    public string GetForeignKeyName(string moduleName, string entityName, string referencedEntityName, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencedEntityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var schema = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        var refTable = ToTableName(referencedEntityName);
        var prop = ToSnakeCase(propertyName);

        return $"fk_{schema}_{table}_{refTable}_{prop}";
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

        var schema = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        var columns = string.Join("_", columnNames.Select(ToSnakeCase));

        return $"uq_{schema}_{table}_{columns}";
    }

    /// <inheritdoc />
    public string ToColumnName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return ToSnakeCase(propertyName);
    }

    /// <inheritdoc />
    public string ToTableName(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        return ToSnakeCase(entityName);
    }

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    /// <param name="input">The PascalCase input string.</param>
    /// <returns>The snake_case output string.</returns>
    /// <example>
    /// ToSnakeCase("ApplicationUser") => "application_user"
    /// ToSnakeCase("FileItem") => "file_item"
    /// ToSnakeCase("HTTPResponse") => "http_response"
    /// </example>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var builder = new StringBuilder(input.Length + 5); // Estimate extra space for underscores
        var previousWasUpper = false;
        var previousWasDigit = false;

        for (int i = 0; i < input.Length; i++)
        {
            var currentChar = input[i];
            var isUpper = char.IsUpper(currentChar);
            var isDigit = char.IsDigit(currentChar);

            // Add underscore before uppercase letter if:
            // 1. Not the first character
            // 2. Previous was not uppercase (e.g., userName => user_name)
            // 3. OR next character exists and is lowercase (e.g., HTTPResponse => http_response)
            if (isUpper && i > 0)
            {
                var shouldAddUnderscore = !previousWasUpper ||
                    (i + 1 < input.Length && char.IsLower(input[i + 1]));

                if (shouldAddUnderscore)
                {
                    builder.Append('_');
                }
            }
            // Add underscore before digit if previous was not a digit
            else if (isDigit && i > 0 && !previousWasDigit)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(currentChar));
            previousWasUpper = isUpper;
            previousWasDigit = isDigit;
        }

        return builder.ToString();
    }
}
