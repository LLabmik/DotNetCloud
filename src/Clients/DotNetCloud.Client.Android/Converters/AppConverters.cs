using System.Globalization;

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
