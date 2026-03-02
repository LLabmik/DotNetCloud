using System.Text;

namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// MariaDB/MySQL table naming strategy using table prefixes and snake_case naming convention.
/// </summary>
/// <remarks>
/// MariaDB/MySQL doesn't support schemas in the same way as PostgreSQL or SQL Server,
/// so we use table prefixes instead:
/// <list type="bullet">
/// <item><description>core_application_users</description></item>
/// <item><description>files_file_items</description></item>
/// <item><description>chat_messages</description></item>
/// </list>
/// <para>
/// Follows MariaDB/MySQL best practices:
/// <list type="bullet">
/// <item><description>Lowercase identifiers (case-sensitive on some systems)</description></item>
/// <item><description>Snake_case for multi-word names</description></item>
/// <item><description>Table prefixes for logical grouping</description></item>
/// <item><description>Identifier length limits (64 characters maximum)</description></item>
/// </list>
/// </para>
/// </remarks>
public class MariaDbNamingStrategy : ITableNamingStrategy
{
    private const int MaxIdentifierLength = 64; // MySQL/MariaDB identifier length limit

    /// <inheritdoc />
    public DatabaseProvider Provider => DatabaseProvider.MariaDB;

    /// <inheritdoc />
    public string GetTableName(string moduleName, string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        var prefix = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        return TruncateIdentifier($"{prefix}_{table}");
    }

    /// <inheritdoc />
    public string? GetSchemaName(string moduleName)
    {
        // MariaDB/MySQL doesn't support schemas in the same way
        // Return null to indicate no schema support
        return null;
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

        var prefix = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        var columns = string.Join("_", columnNames.Select(ToSnakeCase));

        return TruncateIdentifier($"ix_{prefix}_{table}_{columns}");
    }

    /// <inheritdoc />
    public string GetForeignKeyName(string moduleName, string entityName, string referencedEntityName, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencedEntityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var prefix = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        var refTable = ToTableName(referencedEntityName);
        var prop = ToSnakeCase(propertyName);

        return TruncateIdentifier($"fk_{prefix}_{table}_{refTable}_{prop}");
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

        var prefix = ToSnakeCase(moduleName);
        var table = ToTableName(entityName);
        var columns = string.Join("_", columnNames.Select(ToSnakeCase));

        return TruncateIdentifier($"uq_{prefix}_{table}_{columns}");
    }

    /// <inheritdoc />
    public string ToColumnName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return TruncateIdentifier(ToSnakeCase(propertyName));
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

    /// <summary>
    /// Truncates an identifier to the MySQL/MariaDB maximum length (64 characters).
    /// </summary>
    /// <param name="identifier">The identifier to truncate.</param>
    /// <returns>The truncated identifier.</returns>
    /// <remarks>
    /// If truncation is needed, a hash suffix is added to ensure uniqueness:
    /// very_long_identifier_name_that_exceeds_limit => very_long_identifier_name_that_excee_a1b2c3
    /// </remarks>
    private static string TruncateIdentifier(string identifier)
    {
        if (identifier.Length <= MaxIdentifierLength)
        {
            return identifier;
        }

        // Create a short hash for uniqueness
        var hash = Math.Abs(identifier.GetHashCode()).ToString("x");
        var maxBaseLength = MaxIdentifierLength - hash.Length - 1; // -1 for underscore

        return $"{identifier[..maxBaseLength]}_{hash}";
    }
}
