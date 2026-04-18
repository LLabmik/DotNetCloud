using System.Text;
using System.Text.RegularExpressions;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from RTF (Rich Text Format) files by stripping RTF control words.
/// </summary>
public sealed partial class RtfContentExtractor : IContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return string.Equals(mimeType, "application/rtf", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, "text/rtf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var rtf = await reader.ReadToEndAsync(cancellationToken);

        var plainText = StripRtf(rtf);

        return new ExtractedContent
        {
            Text = plainText,
            Metadata = new Dictionary<string, string>
            {
                ["mimeType"] = mimeType
            }
        };
    }

    internal static string StripRtf(string rtf)
    {
        if (string.IsNullOrEmpty(rtf))
            return string.Empty;

        var sb = new StringBuilder(rtf.Length / 2);
        var depth = 0;
        var skipDestination = false;
        var i = 0;

        while (i < rtf.Length)
        {
            var ch = rtf[i];

            switch (ch)
            {
                case '{':
                    depth++;
                    // Check if the next character indicates a destination group to skip
                    if (i + 1 < rtf.Length && rtf[i + 1] == '\\')
                    {
                        var word = ReadControlWord(rtf, i + 1);
                        // Skip known binary/metadata destinations
                        if (word is "\\fonttbl" or "\\colortbl" or "\\stylesheet" or "\\info" or
                            "\\pict" or "\\object" or "\\*" or "\\header" or "\\footer" or
                            "\\footnote" or "\\field" or "\\fldinst")
                        {
                            skipDestination = true;
                        }
                    }
                    i++;
                    break;

                case '}':
                    depth--;
                    if (depth < 0) depth = 0;
                    skipDestination = false;
                    i++;
                    break;

                case '\\' when !skipDestination:
                    i = HandleControlWord(rtf, i, sb);
                    break;

                default:
                    if (!skipDestination && depth > 0)
                    {
                        sb.Append(ch);
                    }
                    i++;
                    break;
            }
        }

        // Clean up result
        var text = sb.ToString();
        text = MultipleSpacesRegex().Replace(text, " ");
        text = MultipleNewlinesRegex().Replace(text, "\n");

        return text.Trim();
    }

    private static string ReadControlWord(string rtf, int pos)
    {
        var end = pos;
        while (end < rtf.Length && end - pos < 30)
        {
            var c = rtf[end];
            if (c == ' ' || c == '{' || c == '}' || c == '\\')
                break;
            end++;
        }
        return rtf[pos..end];
    }

    private static int HandleControlWord(string rtf, int pos, StringBuilder sb)
    {
        // Skip the backslash
        pos++;
        if (pos >= rtf.Length) return pos;

        var ch = rtf[pos];

        // Escaped special characters
        switch (ch)
        {
            case '\\' or '{' or '}':
                sb.Append(ch);
                return pos + 1;
            case '\'':
                // Hex-encoded character: \'xx
                if (pos + 2 < rtf.Length &&
                    byte.TryParse(rtf.AsSpan(pos + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    sb.Append((char)b);
                    return pos + 3;
                }
                return pos + 1;
        }

        // Read control word
        var start = pos;
        while (pos < rtf.Length && char.IsLetter(rtf[pos]))
            pos++;

        // Skip optional numeric parameter
        while (pos < rtf.Length && (char.IsDigit(rtf[pos]) || rtf[pos] == '-'))
            pos++;

        // Skip trailing space delimiter
        if (pos < rtf.Length && rtf[pos] == ' ')
            pos++;

        var word = rtf[start..pos];

        // Map common control words to text
        if (word.StartsWith("par", StringComparison.Ordinal) ||
            word.StartsWith("line", StringComparison.Ordinal))
        {
            sb.Append('\n');
        }
        else if (word.StartsWith("tab", StringComparison.Ordinal))
        {
            sb.Append(' ');
        }
        // Unicode escape: \uN
        else if (word.StartsWith("u", StringComparison.Ordinal) && word.Length > 1)
        {
            var numStr = word[1..].TrimEnd();
            if (int.TryParse(numStr, out var unicode) && unicode >= 0)
            {
                sb.Append(char.ConvertFromUtf32(unicode));
                // Skip the ANSI replacement character that follows
                if (pos < rtf.Length && rtf[pos] == '?')
                    pos++;
            }
        }

        return pos;
    }

    [GeneratedRegex(@"[ \t]+", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultipleNewlinesRegex();
}
