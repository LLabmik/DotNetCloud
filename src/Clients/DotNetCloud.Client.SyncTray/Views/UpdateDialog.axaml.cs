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
    }
}
