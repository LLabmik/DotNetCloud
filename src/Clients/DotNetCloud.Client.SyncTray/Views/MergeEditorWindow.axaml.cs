using Avalonia.Controls;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Three-pane merge editor window for resolving sync conflicts.
/// Left: local version, Right: server version, Bottom: merged result (editable).
/// </summary>
public partial class MergeEditorWindow : Window
{
    /// <summary>Parameterless constructor required by Avalonia runtime loader.</summary>
    public MergeEditorWindow()
    {
        InitializeComponent();
    }

    /// <summary>Initializes the merge editor with the given view-model.</summary>
    public MergeEditorWindow(MergeEditorViewModel vm) : this()
    {
        DataContext = vm;

        // Wire the close action: when the VM's onCompleted fires, close this window.
        vm.CloseRequested += (_, _) => Close();
    }
}
