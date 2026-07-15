using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Main Notes tab page with note list, search, folders, and preview.</summary>
public partial class NotesPage : ContentPage
{
    private readonly NotesViewModel _vm;

    /// <summary>Initializes a new <see cref="NotesPage"/>.</summary>
    public NotesPage(NotesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NotesViewModel.PreviewHtml))
            {
                PreviewWebView.Source = new HtmlWebViewSource { Html = _vm.PreviewHtml };
            }
        };
    }

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsPreviewVisible)
        {
            _vm.ClosePreviewCommand.Execute(null);
            return true; // Back was handled, prevent minimize
        }
        return base.OnBackButtonPressed();
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        if (_vm.Folders.Count == 0 && _vm.LoadFoldersCommand.CanExecute(null))
        {
            _vm.LoadFoldersCommand.Execute(null);
        }
        if (_vm.Notes.Count == 0 && _vm.LoadNotesCommand.CanExecute(null))
        {
            _vm.LoadNotesCommand.Execute(null);
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
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
