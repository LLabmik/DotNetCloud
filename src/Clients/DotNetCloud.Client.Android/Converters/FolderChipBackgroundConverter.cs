using System.Globalization;

namespace DotNetCloud.Client.Android.Converters;

/// <summary>
/// Resolves the background color of a note folder filter chip. Expects values = [the chip's folder
/// id, the currently selected folder id]; the chip whose folder id is the active filter is
/// highlighted. Two <c>null</c> ids are the "All Notes" chip, which stays highlighted exactly while
/// no folder filter is applied.
/// </summary>
public sealed class FolderChipBackgroundConverter : IMultiValueConverter
{
    /// <summary>Background of the chip that currently filters the note list.</summary>
    public static readonly Color HighlightColor = Color.FromArgb("#0EA5E9");

    /// <summary>Background of an inactive chip.</summary>
    public static readonly Color InactiveColor = Color.FromArgb("#1E293B");

    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        Guid? chipFolderId = values.Length > 0 ? values[0] as Guid? : null;
        Guid? selectedFolderId = values.Length > 1 ? values[1] as Guid? : null;

        return chipFolderId == selectedFolderId ? HighlightColor : InactiveColor;
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
