using System.ComponentModel;
using Avalonia.Controls;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Dialog that shows update availability, release notes, and download progress.
/// </summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateViewModel _vm;

    /// <summary>Parameterless constructor required by Avalonia XAML loader.</summary>
    public UpdateDialog() : this(null!) { }

    /// <summary>Initializes a new <see cref="UpdateDialog"/> with the given view-model.</summary>
    public UpdateDialog(UpdateViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        if (vm is not null)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateViewModel.ShouldClose) && _vm.ShouldClose)
        {
            Close();
        }
        else if (e.PropertyName == nameof(UpdateViewModel.IsDownloading) && _vm.IsDownloading)
        {
            // Scroll the download-progress card into view so the user can see
            // the download happening even when the release notes are long.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ContentScroll.ScrollToEnd());
        }
        else if (e.PropertyName == nameof(UpdateViewModel.IsDownloadComplete))
        {
            // Keep the completion state visible once the download finishes.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ContentScroll.ScrollToEnd());
        }
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Prevent the window (title-bar X) from closing while a download or
        // apply is in flight — those operations must be cancelled or completed
        // through the view-model's buttons.
        if (_vm is { IsBusy: true })
        {
            e.Cancel = true;
        }
    }
}
