using System.Globalization;
using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Converters;

/// <summary>Inverts a boolean value (true → false, false → true).</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}

/// <summary>Returns <c>true</c> when the value is a non-null, non-empty string.</summary>
public sealed class IsNotNullOrEmptyConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns <c>true</c> when the value is not zero.</summary>
public sealed class IsNotZeroConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i != 0;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns <c>true</c> when the value is not null.</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns <see cref="FontAttributes.Bold"/> when the unread count is greater than zero;
/// otherwise <see cref="FontAttributes.None"/>.
/// </summary>
public sealed class UnreadToBoldConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0 ? FontAttributes.Bold : FontAttributes.None;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns a red badge color for mentions, amber for ordinary unread counts.
/// </summary>
public sealed class MentionToBadgeColorConverter : IValueConverter
{
    private static readonly Color MentionColor = Color.FromArgb("#E53935");
    private static readonly Color UnreadColor = Color.FromArgb("#FB8C00");

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? MentionColor : UnreadColor;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns a green color when online, gray when offline.</summary>
public sealed class OnlineStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush OnlineBrush = new(Color.FromArgb("#22C55E"));
    private static readonly SolidColorBrush OfflineBrush = new(Color.FromArgb("#475569"));

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? OnlineBrush : OfflineBrush;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a <see cref="TimeSpan"/> to "m:ss" or "h:mm:ss" format.</summary>
public sealed class TimeSpanToMmSsConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        return "0:00";
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a boolean (playing=true) to a play/pause icon string.</summary>
public sealed class BoolToPlayPauseIconConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "⏸" : "▶";

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns <c>true</c> when <see cref="ViewModels.MusicView"/> matches the converter parameter.
/// Used to show/hide the correct CollectionView in MusicPage.
/// </summary>
public sealed class IsViewSelectedConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumVal && parameter is string paramName)
            return enumVal.ToString() == paramName;
        return false;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns a highlighted background color when the tab matches the current view.
/// </summary>
public sealed class TabSelectedConverter : IValueConverter
{
    private static readonly Color SelectedColor = Color.FromArgb("#0EA5E9");
    private static readonly Color UnselectedColor = Color.FromArgb("#1E293B");

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumVal && parameter is string paramName)
            return enumVal.ToString() == paramName ? SelectedColor : UnselectedColor;
        return UnselectedColor;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts a dB value (range approximately -12 to +12) to a 0.0-1.0 value
/// suitable for <see cref="ProgressBar"/>. 0 dB = 0.5, +12 dB = 1.0, -12 dB = 0.0.
/// </summary>
public sealed class DbToProgressConverter : IValueConverter
{
    private const double MaxDb = 12.0;
    private const double MinDb = -12.0;

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float db)
        {
            // Map [-12, +12] → [0.0, 1.0]
            var clamped = Math.Clamp(db, MinDb, MaxDb);
            return (clamped - MinDb) / (MaxDb - MinDb);
        }
        return 0.5; // default midpoint
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// ── Calendar View Converters ──────────────────────────────────────

/// <summary>Returns highlighted/active background color when the view tab matches the converter parameter.</summary>
public sealed class ViewToggleConverter : IValueConverter
{
    private static readonly Color ActiveColor = Color.FromArgb("#0EA5E9");
    private static readonly Color InactiveColor = Color.FromArgb("#1E293B");

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CalendarViewType view && parameter is string param)
            return view.ToString() == param ? ActiveColor : InactiveColor;
        return InactiveColor;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns <c>true</c> when the CalendarViewType matches the converter parameter.</summary>
public sealed class ViewVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CalendarViewType view && parameter is string param)
            return view.ToString() == param;
        return false;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns a highlighted background for today's date cell.</summary>
public sealed class TodayBackgroundConverter : IValueConverter
{
    private static readonly Color TodayColor = Color.FromArgb("#0C1929");
    private static readonly Color NormalColor = Colors.Transparent;
    private static readonly Color FadedColor = Color.FromArgb("#0A0F1A");

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isToday && isToday)
            return TodayColor;
        return parameter?.ToString() == "faded" ? FadedColor : NormalColor;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns brighter text for current-month days, dimmer for padding days.</summary>
public sealed class DayTextColorConverter : IValueConverter
{
    private static readonly Color ActiveColor = Color.FromArgb("#F1F5F9");
    private static readonly Color FadedColor = Color.FromArgb("#475569");

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCurrentMonth)
            return isCurrentMonth ? ActiveColor : FadedColor;
        return FadedColor;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns <c>true</c> when integer value is greater than zero.</summary>
public sealed class IntToBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns a border color based on the visibility toggle state.</summary>
public sealed class BoolToColorConverter : IValueConverter
{
    private static readonly Color VisibleColor = Color.FromArgb("#0EA5E9");
    private static readonly Color HiddenColor = Color.FromArgb("#475569");

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool isVisible && isVisible ? VisibleColor : HiddenColor;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
