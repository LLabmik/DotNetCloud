using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>File browser screen — browse, upload, download, and manage cloud files.</summary>
public partial class FileBrowserPage : ContentPage
{
    private readonly FileBrowserViewModel _vm;

    /// <summary>Initializes a new <see cref="FileBrowserPage"/>.</summary>
    public FileBrowserPage(FileBrowserViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        if (_vm.LoadFilesCommand.CanExecute(null))
            _vm.LoadFilesCommand.Execute(null);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.IsActive = false;
        _vm.ErrorMessage = null;
    }
}
