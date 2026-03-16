using Avalonia.Controls;
using System;
using Avalonia.Interactivity;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Dialog window for the selective sync folder browser.
/// </summary>
public partial class FolderBrowserDialog : Window
{
    /// <summary>Whether the user saved their selection (vs. skipping/closing).</summary>
    public bool Saved { get; private set; }

    /// <summary>Initializes a new <see cref="FolderBrowserDialog"/>.</summary>
    public FolderBrowserDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new <see cref="FolderBrowserDialog"/> with the given view-model
    /// and triggers loading the folder tree.
    /// </summary>
    public FolderBrowserDialog(FolderBrowserViewModel vm) : this()
    {
        BrowserView.DataContext = vm;
        Opened += async (_, _) => await vm.LoadTreeAsync();
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (BrowserView.DataContext is not FolderBrowserViewModel vm)
        {
            Saved = true;
            Close();
            return;
        }

        try
        {
            await vm.SaveAsync();
            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            vm.SetSaveError($"Failed to save folder selection: {ex.Message}");
        }
    }

    private void OnSkip(object? sender, RoutedEventArgs e) => Close();
}
