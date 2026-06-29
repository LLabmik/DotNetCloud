using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Music browsing page. Shows artists, albums, tracks, and playlists
/// with a now-playing bar at the top.
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
}
