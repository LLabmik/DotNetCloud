using System.Globalization;
using Avalonia.Data.Converters;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// Static <see cref="IValueConverter"/> instances for binding enum properties to UI controls
/// such as radio buttons, combo boxes, and visibility toggles.
/// </summary>
public static class EnumConverters
{
    /// <summary>
    /// Returns <c>true</c> when the bound enum value equals the
    /// <see cref="IValueConverter.Parameter"/> value.
    /// </summary>
    public static readonly IValueConverter IsEqual = new EnumEqualityConverter(match: true);

    /// <summary>
    /// Returns <c>true</c> when the bound enum value does NOT equal the
    /// <see cref="IValueConverter.Parameter"/> value.
    /// </summary>
    public static readonly IValueConverter IsNotEqual = new EnumEqualityConverter(match: false);

    private sealed class EnumEqualityConverter : IValueConverter
    {
        private readonly bool _match;

        internal EnumEqualityConverter(bool match) => _match = match;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
                return false;

            // parameter comes in as a string from x:Static reference in AXAML;
            // dereference it via the type system.
            var paramValue = parameter is Enum enumParam ? enumParam
                : parameter is string strParam && value.GetType().IsEnum
                    ? Enum.Parse(value.GetType(), strParam)
                    : parameter;

            var equal = Equals(value, paramValue);
            return _match ? equal : !equal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is Enum enumParam)
                return enumParam;

            if (value is true && parameter is string strParam && targetType.IsEnum)
                return Enum.Parse(targetType, strParam);

            return Avalonia.Data.BindingNotification.UnsetValue;
        }
    }
}
