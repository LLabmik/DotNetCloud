using Avalonia.Controls;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Folder browser user control for selective sync folder selection.
/// </summary>
public partial class FolderBrowserView : UserControl
{
    /// <summary>Initializes a new <see cref="FolderBrowserView"/>.</summary>
    public FolderBrowserView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is FolderBrowserViewModel vm && e.AddedItems.Count > 0
            && e.AddedItems[0] is FolderBrowserItemViewModel item)
        {
            vm.SelectFolder(item);
        }
    }
}
