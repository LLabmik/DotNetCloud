using Avalonia.Controls;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Settings window code-behind.  Binds to <see cref="SettingsViewModel"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>Initializes a new <see cref="SettingsWindow"/>.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>Initializes a new <see cref="SettingsWindow"/> with the given view-model.</summary>
    public SettingsWindow(SettingsViewModel vm) : this()
    {
        DataContext = vm;
    }
}
