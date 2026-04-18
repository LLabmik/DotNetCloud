namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Represents a parsed search query with separated terms, phrases, filters, and exclusions.
/// Produced by <see cref="SearchQueryParser"/> from raw user input.
/// </summary>
public sealed record ParsedSearchQuery
{
    /// <summary>Individual search keywords (unquoted, non-filter tokens).</summary>
    public IReadOnlyList<string> Terms { get; init; } = [];

    /// <summary>Exact phrase matches (quoted strings, e.g., "quarterly report").</summary>
    public IReadOnlyList<string> Phrases { get; init; } = [];

    /// <summary>Terms to exclude from results (prefixed with -).</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];

    /// <summary>Module filter extracted from <c>in:module</c> syntax. Null if not specified.</summary>
    public string? ModuleFilter { get; init; }

    /// <summary>Type filter extracted from <c>type:value</c> syntax. Null if not specified.</summary>
    public string? TypeFilter { get; init; }

    /// <summary>Whether the parsed query has any searchable content (terms, phrases, or exclusions).</summary>
    public bool HasSearchableContent => Terms.Count > 0 || Phrases.Count > 0;

    /// <summary>
    /// Builds a plain-text query string from terms and phrases for full-text search providers.
    /// </summary>
    public string ToPlainTextQuery()
    {
        var parts = new List<string>();
        parts.AddRange(Terms);
        parts.AddRange(Phrases.Select(p => $"\"{p}\""));
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Builds a PostgreSQL <c>to_tsquery</c>-compatible query string.
    /// Terms are AND-joined, phrases are quoted, exclusions use the <c>!</c> operator.
    /// </summary>
    public string ToPostgreSqlTsQuery()
    {
        var parts = new List<string>();

        foreach (var term in Terms)
        {
            parts.Add(SanitizeTsQueryTerm(term));
        }

        foreach (var phrase in Phrases)
        {
            // PostgreSQL phrase search: words joined with <->
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                parts.Add(string.Join(" <-> ", words.Select(SanitizeTsQueryTerm)));
            }
        }

        foreach (var exclusion in Exclusions)
        {
            parts.Add($"!{SanitizeTsQueryTerm(exclusion)}");
        }

        return string.Join(" & ", parts);
    }

    /// <summary>
    /// Builds a SQL Server <c>CONTAINS</c>-compatible query string.
    /// Terms are AND-joined, phrases are double-quoted, exclusions use <c>AND NOT</c>.
    /// </summary>
    public string ToSqlServerContainsQuery()
    {
        var parts = new List<string>();

        foreach (var term in Terms)
        {
            parts.Add($"\"{SanitizeContainsTerm(term)}\"");
        }

        foreach (var phrase in Phrases)
        {
            parts.Add($"\"{SanitizeContainsTerm(phrase)}\"");
        }

        var positive = string.Join(" AND ", parts);

        if (Exclusions.Count > 0)
        {
            var exclusionParts = Exclusions.Select(e => $"\"{SanitizeContainsTerm(e)}\"");
            positive = $"({positive}) AND NOT ({string.Join(" OR ", exclusionParts)})";
        }

        return positive;
    }

    /// <summary>
    /// Builds a MariaDB <c>MATCH ... AGAINST ... IN BOOLEAN MODE</c>-compatible query string.
    /// Terms prefixed with <c>+</c>, exclusions with <c>-</c>, phrases double-quoted.
    /// </summary>
    public string ToMariaDbBooleanQuery()
    {
        var parts = new List<string>();

        foreach (var term in Terms)
        {
            parts.Add($"+{SanitizeBooleanTerm(term)}");
        }

        foreach (var phrase in Phrases)
        {
            parts.Add($"+\"{SanitizeBooleanTerm(phrase)}\"");
        }

        foreach (var exclusion in Exclusions)
        {
            parts.Add($"-{SanitizeBooleanTerm(exclusion)}");
        }

        return string.Join(' ', parts);
    }

    private static string SanitizeTsQueryTerm(string term)
    {
        // Remove characters that are special in tsquery syntax
        return new string(term.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }

    private static string SanitizeContainsTerm(string term)
    {
        // Remove double quotes to prevent injection in CONTAINS syntax
        return term.Replace("\"", "", StringComparison.Ordinal);
    }

    private static string SanitizeBooleanTerm(string term)
    {
        // Remove characters that are special in BOOLEAN MODE: + - < > ( ) ~ * "
        return new string(term.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ').ToArray());
    }
}
