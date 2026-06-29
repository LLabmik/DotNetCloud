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
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Artists.Count == 0)
            await _vm.LoadArtistsCommand.ExecuteAsync(null);
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
}
