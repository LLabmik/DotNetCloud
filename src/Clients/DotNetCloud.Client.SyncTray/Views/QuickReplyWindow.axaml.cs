using Avalonia.Controls;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Quick-reply popup window for sending a message to a chat channel from the tray.
/// </summary>
public partial class QuickReplyWindow : Window
{
    /// <summary>Parameterless constructor required by Avalonia runtime loader.</summary>
    public QuickReplyWindow()
    {
        InitializeComponent();
    }

    /// <summary>Initializes the quick-reply window with the given view-model.</summary>
    public QuickReplyWindow(QuickReplyViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += (_, _) => Close();
    }
}
