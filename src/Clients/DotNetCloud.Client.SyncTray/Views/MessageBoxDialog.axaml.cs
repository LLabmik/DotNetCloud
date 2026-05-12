using Avalonia;
using Avalonia.Controls;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Enumeration of supported message box button combinations.
/// </summary>
public enum MessageBoxButtons
{
    /// <summary>OK button only.</summary>
    OK,

    /// <summary>Yes and No buttons.</summary>
    YesNo,
}

/// <summary>
/// Enumeration of possible message box results.
/// </summary>
public enum MessageBoxResult
{
    /// <summary>Dialog was closed without a selection.</summary>
    None,

    /// <summary>User clicked OK or Yes.</summary>
    Yes,

    /// <summary>User clicked No or Cancel.</summary>
    No,
}

/// <summary>
/// A simple modal message box dialog for confirmation prompts.
/// </summary>
public partial class MessageBoxDialog : Window
{
    /// <summary>Gets the dialog result after the user makes a selection.</summary>
    public MessageBoxResult DialogResult { get; private set; } = MessageBoxResult.None;

    /// <summary>Parameterless constructor required by Avalonia XAML loader.</summary>
    public MessageBoxDialog() : this(string.Empty, string.Empty) { }

    /// <summary>
    /// Initializes a new <see cref="MessageBoxDialog"/> with the given title and message.
    /// </summary>
    /// <param name="title">Window title.</param>
    /// <param name="message">Message body text.</param>
    /// <param name="buttons">Button combination to show.</param>
    public MessageBoxDialog(string title, string message, MessageBoxButtons buttons = MessageBoxButtons.OK)
    {
        InitializeComponent();

        Title = title;

        DataContext = new MessageBoxDataContext
        {
            Title = title,
            Message = message,
            YesText = buttons == MessageBoxButtons.YesNo ? "Yes" : "OK",
        };

        if (buttons == MessageBoxButtons.OK)
            NoButton.Content = "Cancel";

        // Center on owner window
        Loaded += (_, _) =>
        {
            if (Owner is Window ownerWindow)
            {
                double x = ownerWindow.Position.X + (ownerWindow.Width - Width) / 2;
                double y = ownerWindow.Position.Y + (ownerWindow.Height - Height) / 2;
                Position = new PixelPoint((int)x, (int)y);
            }
        };
    }

    private void OnYesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DialogResult = MessageBoxResult.Yes;
        Close();
    }

    private void OnNoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DialogResult = MessageBoxResult.No;
        Close();
    }
}

/// <summary>
/// Data context for the message box dialog.
/// </summary>
internal sealed class MessageBoxDataContext
{
    /// <summary>Window title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Message body text.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Text for the affirmative button (OK/Yes).</summary>
    public string YesText { get; init; } = "OK";
}
