using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Music browsing page. Shows artists, albums, tracks, playlists, and EQ
/// with a now-playing bar (including album art and seek slider) at the top.
/// </summary>
public partial class MusicPage : ContentPage
{
    private readonly MusicViewModel _vm;

    /// <summary>Initializes a new <see cref="MusicPage"/>.</summary>
    public MusicPage(MusicViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // Wire up the scroll-to-character delegate from the ViewModel
        _vm.ScrollToRequested += OnScrollToRequested;

        // Auto-focus the search Entry when the search panel opens
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MusicViewModel.IsSearchOpen) && _vm.IsSearchOpen)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(100);
                    SearchEntry?.Focus();
                });
            }
        };
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Artists.Count == 0)
            await _vm.LoadArtistsCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Called by the ViewModel when the user taps a character in the alphabet index strip.
    /// Scrolls the appropriate CollectionView to the first matching item.
    /// </summary>
    private async void OnScrollToRequested(object? targetItem, MusicView view)
    {
        if (targetItem is null)
            return;

        CollectionView? cv = view switch
        {
            MusicView.Artists => ArtistsCollectionView,
            MusicView.Albums => AlbumsCollectionView,
            MusicView.Tracks => TracksCollectionView,
            _ => null
        };

        if (cv is null)
            return;

        // Small delay to let the UI settle before scrolling
        await Task.Delay(50);
        cv.ScrollTo(targetItem, position: ScrollToPosition.Start, animate: true);
    }

    /// <summary>Called when the user completes search input (presses Search on keyboard). Dismisses the keyboard.</summary>
    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry entry)
            entry.Unfocus();
    }

    /// <summary>Called when the user starts dragging the seek slider.</summary>
    private void OnSeekDragStarted(object? sender, EventArgs e)
    {
        _vm.IsSeeking = true;
    }

    /// <summary>Called when the user releases the seek slider — performs the seek.</summary>
    private void OnSeekDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Slider slider)
            _vm.SeekToCommand.Execute(slider.Value);
    }

    /// <summary>
    /// Called when the user drags an EQ band slider. Reads the band index from the
    /// slider's <see cref="EqBandModel"/> binding context and applies the gain to the native EQ.
    /// If the equalizer has been disposed (playback stopped), shows a brief alert.
    /// </summary>
    private void OnEqSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is Slider slider && slider.BindingContext is EqBandModel band)
        {
            if (!_vm.EqAvailable)
            {
                // EQ was disposed (playback stopped). The banner should already
                // be visible, but provide a subtle hint if the user interacts.
                return;
            }

            _vm.OnEqBandChanged(band.BandIndex, (float)e.NewValue);
        }
    }
}
