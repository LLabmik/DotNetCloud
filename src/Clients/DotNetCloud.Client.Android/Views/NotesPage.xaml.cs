using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Main Notes tab page with note list, search, folders, and preview.</summary>
public partial class NotesPage : ContentPage
{
    private readonly NotesViewModel _vm;

    /// <summary>Consumes the Android system back press while the note preview is open.</summary>
    private readonly SystemBackNavigation _back;

    /// <summary>Initializes a new <see cref="NotesPage"/>.</summary>
    public NotesPage(NotesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // The system back button closes the preview like the preview's own close button does;
        // Page.OnBackButtonPressed is never called for a Shell drawer page on MAUI 10.0.90.
        _back = new SystemBackNavigation(
            stateNotifier: _vm,
            canHandle: () => _vm.CanHandleSystemBack,
            handle: () => _vm.HandleSystemBackAsync());

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NotesViewModel.PreviewHtml))
            {
                PreviewWebView.Source = new HtmlWebViewSource { Html = _vm.PreviewHtml };
            }
        };
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _back.Attach();

        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        if (_vm.Folders.Count == 0 && _vm.LoadFoldersCommand.CanExecute(null))
        {
            _vm.LoadFoldersCommand.Execute(null);
        }
        _vm.LoadNotesCommand.Execute(null);
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

    private void OnNoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is NoteDto note)
        {
            _vm.SelectNoteCommand.Execute(note);
        }
        // Clear selection to allow re-selecting the same item
        if (sender is CollectionView cv)
            cv.SelectedItem = null;
    }
}
