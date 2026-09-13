using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>File browser screen — browse, upload, download, and manage cloud files.</summary>
public partial class FileBrowserPage : ContentPage
{
    private readonly FileBrowserViewModel _vm;

    /// <summary>Consumes the Android system back press while this tab has a parent folder to show.</summary>
    private readonly SystemBackNavigation _back;

    /// <summary>Initializes a new <see cref="FileBrowserPage"/>.</summary>
    public FileBrowserPage(FileBrowserViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // The system back button mirrors the in-page back button (up one folder) instead of
        // dropping the user out of the app while they are browsing a folder tree.
        _back = new SystemBackNavigation(
            stateNotifier: _vm,
            canHandle: () => _vm.CanHandleSystemBack,
            handle: () => _vm.HandleSystemBackAsync());
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _back.Attach();

        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        if (_vm.LoadFilesCommand.CanExecute(null))
            _vm.LoadFilesCommand.Execute(null);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Stop consuming back presses while another tab (or a pushed page) is showing.
        _back.Detach();

        _vm.IsActive = false;
        _vm.ErrorMessage = null;
    }
}
