using Avalonia.Controls;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Compact sync progress popup shown when the user left-clicks the tray icon
/// while a sync is in progress.
/// </summary>
public partial class SyncProgressWindow : Window
{
    /// <summary>Initializes a new <see cref="SyncProgressWindow"/>.</summary>
    public SyncProgressWindow()
    {
        InitializeComponent();
    }

    /// <summary>Initializes a new <see cref="SyncProgressWindow"/> with the specified view-model.</summary>
    public SyncProgressWindow(SyncProgressViewModel vm) : this()
    {
        DataContext = vm;
    }
}
