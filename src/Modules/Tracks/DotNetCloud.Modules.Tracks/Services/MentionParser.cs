using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Parses @username mentions from text content (comments, descriptions).
/// </summary>
public static partial class MentionParser
{
    // Matches @username where username is 1-39 alphanumeric/hyphen/underscore characters
    // Does not match email addresses (preceded by non-whitespace)
    [GeneratedRegex(@"(?<=^|[\s(])@([A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?)", RegexOptions.Compiled)]
    private static partial Regex MentionPattern();

    /// <summary>
    /// Extracts unique @username mentions from the given text.
    /// </summary>
    /// <param name="text">The text to parse for mentions.</param>
    /// <returns>A distinct list of usernames (without the @ prefix), case-preserved.</returns>
    public static IReadOnlyList<string> ParseMentions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var matches = MentionPattern().Matches(text);
        if (matches.Count == 0)
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (Match match in matches)
        {
            var username = match.Groups[1].Value;
            if (seen.Add(username))
            {
                result.Add(username);
            }
        }

        return result;
    }
}
