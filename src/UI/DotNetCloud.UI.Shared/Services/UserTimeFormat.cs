namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Shared constants and helpers for the user's clock-format preference.
/// The preference is persisted in user settings under module <c>dotnetcloud.ui</c>
/// with the key <c>timeFormat</c>.
/// </summary>
public static class UserTimeFormat
{
    /// <summary>The user-settings module that owns UI preferences.</summary>
    public const string SettingsModule = "dotnetcloud.ui";

    /// <summary>The user-settings key for the clock-format preference.</summary>
    public const string SettingKey = "timeFormat";

    /// <summary>The stored value for a 12-hour clock (AM/PM).</summary>
    public const string TwelveHour = "12h";

    /// <summary>The stored value for a 24-hour clock.</summary>
    public const string TwentyFourHour = "24h";

    /// <summary>
    /// Determines whether a stored setting value selects a 24-hour clock.
    /// Any value other than <see cref="TwentyFourHour"/> falls back to the 12-hour default.
    /// </summary>
    public static bool IsTwentyFourHour(string? storedValue)
        => string.Equals(storedValue, TwentyFourHour, StringComparison.OrdinalIgnoreCase);
}
