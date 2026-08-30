using System.Globalization;
using DotNetCloud.Client.Android.Ai;
using Microsoft.Maui.Controls;

namespace DotNetCloud.Client.Android.Converters;

/// <summary>
/// XAML value converter that renders Markdown to a full HTML document wrapped in a
/// <see cref="HtmlWebViewSource"/> (for binding a <see cref="WebView.Source"/> directly).
/// </summary>
public sealed class MarkdownHtmlConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new HtmlWebViewSource { Html = MarkdownHtmlFormatter.ToHtmlDocument(value as string) };

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// XAML value converter that reports whether a message's Markdown contains block-level constructs
/// that require the HTML/WebView renderer. Pass parameter <c>"Invert"</c> to negate the result
/// (e.g. to show the lightweight <see cref="Label"/> path for simple messages).
/// </summary>
public sealed class IsRichMarkdownConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isRich = MarkdownHtmlFormatter.NeedsHtmlRendering(value as string);
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !isRich : isRich;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
