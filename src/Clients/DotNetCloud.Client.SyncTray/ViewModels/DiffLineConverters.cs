using System.Globalization;
using Avalonia.Data.Converters;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// Static IValueConverter instances for binding <see cref="DiffLineType"/> to CSS-like classes.
/// </summary>
public static class DiffLineConverters
{
    /// <summary>Returns true when the line type is <see cref="DiffLineType.Inserted"/>.</summary>
    public static readonly IValueConverter IsInserted = new DiffLineTypeConverter(DiffLineType.Inserted);

    /// <summary>Returns true when the line type is <see cref="DiffLineType.Deleted"/>.</summary>
    public static readonly IValueConverter IsDeleted = new DiffLineTypeConverter(DiffLineType.Deleted);

    /// <summary>Returns true when the line type is <see cref="DiffLineType.Modified"/>.</summary>
    public static readonly IValueConverter IsModified = new DiffLineTypeConverter(DiffLineType.Modified);

    /// <summary>Returns true when the line type is <see cref="DiffLineType.Filler"/>.</summary>
    public static readonly IValueConverter IsFiller = new DiffLineTypeConverter(DiffLineType.Filler);

    private sealed class DiffLineTypeConverter : IValueConverter
    {
        private readonly DiffLineType _target;
        internal DiffLineTypeConverter(DiffLineType target) => _target = target;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is DiffLineType lineType && lineType == _target;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
