namespace DotNetCloud.Core.Modules;

/// <summary>
/// Validation rules for module setting keys and values.
/// </summary>
/// <remarks>
/// Module-supplied keys and values are untrusted input: keys become part of a composite primary
/// key and are echoed into logs, values are persisted verbatim. Both are length-capped and keys are
/// restricted to a conservative character set so they cannot carry control characters or path-like
/// text into other subsystems.
/// </remarks>
public static class ModuleSettingLimits
{
    /// <summary>Maximum key length.</summary>
    public const int MaxKeyLength = 200;

    /// <summary>Maximum value length.</summary>
    public const int MaxValueLength = 10000;

    /// <summary>
    /// Determines whether a setting key is well formed.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <returns><see langword="true"/> when the key is 1–200 characters of a conservative alphabet.</returns>
    public static bool IsValidKey(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
        {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(key[0]))
        {
            return false;
        }

        foreach (var ch in key)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '_' or '-' or ':'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a setting value is within the persisted size limit.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns><see langword="true"/> when the value fits.</returns>
    public static bool IsValidValue(string? value) =>
        value is not null && value.Length <= MaxValueLength;
}
