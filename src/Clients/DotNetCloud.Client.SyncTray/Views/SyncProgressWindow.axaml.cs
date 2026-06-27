using Avalonia.Controls;
using Avalonia.Input;
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

    /// <summary>Handles the "View Details" link click on the error banner.</summary>
    private void OnViewDetailsClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SyncProgressViewModel vm)
            vm.OpenSettingsCommand.Execute(null);
    }

    /// <summary>Handles the "Open Conflict Editor" link click on the conflict banner.</summary>
    private void OnOpenConflictsClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SyncProgressViewModel vm)
            vm.OpenConflictsCommand.Execute(null);
    }
}
