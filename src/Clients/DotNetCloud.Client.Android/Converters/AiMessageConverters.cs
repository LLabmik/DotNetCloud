using System.Globalization;
using Microsoft.Maui.Controls;

namespace DotNetCloud.Client.Android.Converters;

/// <summary>
/// Maps an AI message role ("assistant"/"user") to a display name ("Assistant"/"You"),
/// mirroring the role labels shown in the Blazor AI module.
/// </summary>
public sealed class RoleNameConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value as string switch
        {
            "assistant" => "Assistant",
            "user" => "You",
            null => "",
            var role => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(role),
        };

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// True when the message role is "assistant" (used to show the robot avatar and the copy action).
/// </summary>
public sealed class IsAssistantConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, "assistant", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// True when the message role is "user" (used to show the person avatar).
/// </summary>
public sealed class IsUserConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, "user", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Multi-value converter: returns "Copied!" when the bound message id matches the
/// ViewModel's last-copied id, otherwise "Copy". Mirrors the Blazor module's feedback.
/// </summary>
public sealed class CopiedStateConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is Guid id && values[1] is Guid copiedId && id == copiedId)
            return "Copied!";
        return "Copy";
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
