using Serilog.Core;
using Serilog.Events;

namespace DotNetCloud.Core.ServiceDefaults.Logging;

/// <summary>
/// A Serilog destructuring policy that sanitizes string values before they are
/// written to the log. This prevents log-forging attacks where user-controlled
/// input could inject fake log entries by embedding newlines or other control
/// characters.
/// </summary>
/// <remarks>
/// <para>
/// This policy applies to all <see cref="ScalarValue"/> entries that carry a
/// <see cref="string"/> value. It performs the following sanitization:
/// </para>
/// <list type="bullet">
///   <item>Replaces <c>\r\n</c>, <c>\r</c>, and <c>\n</c> with a space to prevent
///   line-break injection.</item>
///   <item>Replaces other ASCII control characters (U+0000–U+001F, except tab)
///   with a space.</item>
///   <item>Truncates values longer than 10 000 characters to 10 000 characters.</item>
/// </list>
/// <para>
/// The original <c>string</c> instance is never mutated; a new sanitized
/// <see cref="string"/> is produced when the log event is written.
/// </para>
/// </remarks>
public sealed class SafeStringDestructuringPolicy : IDestructuringPolicy
{
    private const int MaxLength = 10_000;

    /// <inheritdoc />
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        if (value is not string str)
        {
            result = null!;
            return false;
        }

        var sanitized = Sanitize(str);

        result = new ScalarValue(sanitized);
        return true;
    }

    /// <summary>
    /// Sanitizes a string by stripping newlines and control characters.
    /// </summary>
    internal static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Fast path: if the string is clean, return it as-is
        if (IsClean(value))
        {
            return Truncate(value);
        }

        var span = value.AsSpan();
        // Cap the working span to MaxLength to avoid huge allocations
        if (span.Length > MaxLength)
        {
            span = span[..MaxLength];
        }

        // First pass: replace \r\n with space as a unit, then handle individual CR/LF
        var result = ReplaceCrlf(span);

        return result;
    }

    private static string ReplaceCrlf(ReadOnlySpan<char> span)
    {
        // Estimate buffer size: no larger than input
        var buffer = new char[span.Length];
        var write = 0;
        var i = 0;

        while (i < span.Length)
        {
            var ch = span[i];

            if (ch is '\r' && i + 1 < span.Length && span[i + 1] is '\n')
            {
                // \r\n -> space
                buffer[write++] = ' ';
                i += 2;
            }
            else if (IsControlOrNewline(ch))
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

    private static bool IsClean(string value)
    {
        // Only check up to MaxLength
        var len = value.Length > MaxLength ? MaxLength : value.Length;
        for (var i = 0; i < len; i++)
        {
            if (IsControlOrNewline(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsControlOrNewline(char ch)
    {
        if (ch is '\r' or '\n')
            return true;
        return ch < 0x20 && ch != '\t';
    }

    private static string Truncate(string value)
        => value.Length > MaxLength ? value[..MaxLength] : value;
}
