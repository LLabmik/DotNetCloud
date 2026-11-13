using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Derives PostgreSQL schema / role names and SQL Server schema / user names for a module's
/// isolated data store.
/// </summary>
/// <remarks>
/// <para>
/// Names are derived only from a validated module id and always end with a hash of that id, so two
/// ids that slugify to the same text cannot collide. PostgreSQL truncates identifiers at 63 bytes
/// and both engines are case-insensitive by default, so the total length is capped below that and
/// the slug is lower-cased.
/// </para>
/// <para>
/// The derivation is deterministic: re-provisioning an existing module produces the same names.
/// </para>
/// </remarks>
internal static class ModuleDataStoreNaming
{
    /// <summary>Maximum identifier length shared by PostgreSQL (63 bytes) and our SQL Server names.</summary>
    public const int MaxIdentifierLength = 63;

    private const int MaxSlugLength = 40;
    private const int HashLength = 8;

    /// <summary>
    /// Derives the schema and principal names for a module.
    /// </summary>
    /// <param name="moduleId">The module id (already validated against the manifest id rules).</param>
    /// <param name="schemaName">The derived schema name.</param>
    /// <param name="principalName">The derived role / contained-user name.</param>
    /// <param name="error">Why the module id cannot be used, when derivation fails.</param>
    /// <returns><see langword="true"/> when names could be derived.</returns>
    public static bool TryCreate(
        string moduleId,
        out string schemaName,
        out string principalName,
        out string? error)
    {
        schemaName = string.Empty;
        principalName = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            error = "Module id is required.";
            return false;
        }

        if (moduleId.Length > 200)
        {
            error = "Module id is too long (max 200).";
            return false;
        }

        foreach (var ch in moduleId)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '-' or '_'))
            {
                error = "Module id contains unsupported characters.";
                return false;
            }
        }

        var slug = BuildSlug(moduleId);
        var hash = BuildHash(moduleId);

        schemaName = $"dnc_{slug}_{hash}";
        principalName = $"dnc_mod_{slug}_{hash}";

        if (schemaName.Length > MaxIdentifierLength || principalName.Length > MaxIdentifierLength)
        {
            error = "Derived identifier exceeds the maximum length.";
            return false;
        }

        return true;
    }

    /// <summary>Quotes a PostgreSQL identifier, doubling embedded double quotes defensively.</summary>
    public static string QuotePostgreSql(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// Determines whether a value is a plain, unquoted identifier of the shape this codebase
    /// generates and accepts for schemas, roles and relations.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value is lower-case ASCII letters, digits or underscores.</returns>
    /// <remarks>
    /// This is a defence-in-depth check applied to names that come back from a database catalog or
    /// from a module's historical naming, <i>before</i> they are interpolated into DDL. Catalog
    /// names are already quoted through <see cref="QuotePostgreSql"/>/<see cref="QuoteSqlServer"/>,
    /// but rejecting anything that is not a plain identifier means a surprising name can never reach
    /// a statement at all.
    /// </remarks>
    public static bool IsPlainIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxIdentifierLength)
        {
            return false;
        }

        if (!char.IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Quotes a SQL Server identifier, doubling embedded closing brackets defensively.</summary>
    public static string QuoteSqlServer(string identifier) =>
        "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    /// <summary>Escapes a string literal for inline DDL (<c>'x'</c>). Generated secrets contain no quotes.</summary>
    public static string EscapeLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string BuildSlug(string moduleId)
    {
        var builder = new StringBuilder(MaxSlugLength);
        var lastWasSeparator = false;

        foreach (var ch in moduleId.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }

            if (builder.Length >= MaxSlugLength)
            {
                break;
            }
        }

        return builder.ToString().TrimEnd('_');
    }

    private static string BuildHash(string moduleId)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(moduleId));
        return Convert.ToHexString(digest)[..HashLength].ToLowerInvariant();
    }
}
