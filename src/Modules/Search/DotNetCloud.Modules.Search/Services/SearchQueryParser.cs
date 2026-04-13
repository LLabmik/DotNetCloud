using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Parses raw user search input into a structured <see cref="ParsedSearchQuery"/>.
/// Supports keywords, quoted phrases, module filters (<c>in:module</c>),
/// type filters (<c>type:value</c>), and term exclusions (<c>-term</c>).
/// </summary>
public static partial class SearchQueryParser
{
    /// <summary>
    /// Parses user input into a structured search query.
    /// </summary>
    /// <param name="input">Raw search text from the user.</param>
    /// <returns>A parsed query with separated terms, phrases, filters, and exclusions.</returns>
    public static ParsedSearchQuery Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedSearchQuery();
        }

        var terms = new List<string>();
        var phrases = new List<string>();
        var exclusions = new List<string>();
        string? moduleFilter = null;
        string? typeFilter = null;

        // Extract quoted phrases first (both double and single quotes)
        var remaining = input.Trim();
        var phraseMatches = QuotedPhraseRegex().Matches(remaining);

        foreach (Match match in phraseMatches)
        {
            var phrase = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(phrase))
            {
                phrases.Add(phrase);
            }
        }

        // Remove quoted phrases from input for further parsing
        remaining = QuotedPhraseRegex().Replace(remaining, " ").Trim();

        // Tokenize the remaining input
        var tokens = remaining.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            // Module filter: in:notes, in:files
            if (token.StartsWith("in:", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
            {
                moduleFilter = token[3..].ToLowerInvariant();
                continue;
            }

            // Type filter: type:pdf, type:Note
            if (token.StartsWith("type:", StringComparison.OrdinalIgnoreCase) && token.Length > 5)
            {
                typeFilter = token[5..];
                continue;
            }

            // Exclusion: -draft, -template
            if (token.StartsWith('-') && token.Length > 1 && !token.StartsWith("--", StringComparison.Ordinal))
            {
                exclusions.Add(token[1..]);
                continue;
            }

            // Regular search term — skip bare punctuation like standalone dashes or stray quotes
            if (!string.IsNullOrWhiteSpace(token))
            {
                var cleaned = token.Trim('"', '\'');
                if (!string.IsNullOrEmpty(cleaned) && cleaned != "-")
                {
                    terms.Add(cleaned);
                }
            }
        }

        return new ParsedSearchQuery
        {
            Terms = terms,
            Phrases = phrases,
            Exclusions = exclusions,
            ModuleFilter = moduleFilter,
            TypeFilter = typeFilter
        };
    }

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex QuotedPhraseRegex();
}
