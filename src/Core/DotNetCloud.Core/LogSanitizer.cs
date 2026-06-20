namespace DotNetCloud.Core;

/// <summary>
/// Provides a centralized sanitization method for user-controlled data
/// before it is passed to logger calls. Prevents log-forging attacks
/// (CWE-117) where embedded newlines or control characters could inject
/// fake log entries.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Sanitizes a string value for safe logging by replacing newline and
    /// control characters with spaces.
    /// </summary>
    /// <param name="value">The user-controlled input to sanitize.</param>
    /// <returns>A sanitized string. Returns "(null)" if value is null.</returns>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? "(null)";

        // Fast path: if the string has no problem chars, return as-is
        if (IsClean(value))
            return value;

        // Cap to reasonable length
        if (value.Length > 10_000)
            value = value[..10_000];

        return ReplaceCrlf(value);
    }

    private static bool IsClean(string value)
    {
        foreach (var ch in value)
        {
            if (ch is '\r' or '\n')
                return false;
            if (ch < 0x20 && ch is not '\t')
                return false;
        }
        return true;
    }

    private static string ReplaceCrlf(string value)
    {
        var buffer = new char[value.Length];
        var write = 0;
        var i = 0;

        while (i < value.Length)
        {
            var ch = value[i];

            if (ch is '\r' && i + 1 < value.Length && value[i + 1] is '\n')
            {
                buffer[write++] = ' ';
                i += 2;
            }
            else if (ch is '\r' or '\n')
            {
                buffer[write++] = ' ';
                i++;
            }
            else if (ch < 0x20 && ch is not '\t')
            {
                buffer[write++] = ' ';
                i++;
            }
            else
            {
                buffer[write++] = ch;
                i++;
            }
        }

        return new string(buffer, 0, write);
    }
}
