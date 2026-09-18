using System.Globalization;

namespace DotNetCloud.Client.Android.Converters;

/// <summary>
/// Formats a stored UTC <see cref="DateTime"/> in the device's local time zone, so calendar
/// times read as the wall clock the user actually experiences instead of UTC.
/// </summary>
/// <remarks>
/// Calendar DTOs carry UTC timestamps, and JSON deserialization leaves
/// <see cref="DateTime.Kind"/> as <see cref="DateTimeKind.Unspecified"/> — a value that
/// <see cref="DateTime.ToLocalTime"/> would otherwise treat as already-local and shift twice.
/// Unspecified values are therefore pinned to UTC before the conversion.
/// </remarks>
public sealed class LocalTimeConverter : IValueConverter
{
    /// <summary>Format used when no <c>ConverterParameter</c> is supplied.</summary>
    internal const string DefaultFormat = "HH:mm";

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime utc)
        {
            return string.Empty;
        }

        var format = parameter as string;
        if (string.IsNullOrEmpty(format))
        {
            format = DefaultFormat;
        }

        var local = (utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc)
            .ToLocalTime();

        return local.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
