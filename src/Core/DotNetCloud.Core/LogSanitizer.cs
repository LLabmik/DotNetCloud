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

        // Cap to a reasonable length before processing.
        if (value.Length > 10_000)
            value = value[..10_000];

        // Replace every line ending (CRLF, LF, and CR) with a space.
        // This call is intentionally ALWAYS on the return path — never guarded
        // by a "clean input" fast path — so that static analysis
        // (CodeQL cs/log-forging) can see untrusted input is neutralized before
        // it reaches a log sink, regardless of the runtime value.
        value = value.ReplaceLineEndings(" ");

        // Defense-in-depth: replace any remaining C0 control characters (other
        // than tab) with spaces so structured log output cannot be corrupted
        // by characters such as NUL or ESC. Line endings are already handled
        // above.
        if (HasControlCharacters(value))
            value = ReplaceControlCharacters(value);

        return value;
    }

    private static bool HasControlCharacters(string value)
    {
        foreach (var ch in value)
        {
            if (ch < 0x20 && ch is not '\t')
                return true;
        }
        return false;
    }

    private static string ReplaceControlCharacters(string value)
    {
        var buffer = new char[value.Length];
        var write = 0;
        foreach (var ch in value)
        {
            buffer[write++] = ch < 0x20 && ch is not '\t' ? ' ' : ch;
        }

        return new string(buffer, 0, write);
    }
}
