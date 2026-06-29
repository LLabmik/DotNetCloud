using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Music;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>Which browsing view is currently displayed.</summary>
public enum MusicView { Artists, Albums, Tracks, Playlists }

/// <summary>
/// ViewModel for the Music tab. Handles browsing artists, albums, tracks, and playlists;
/// playback control; and equalizer preset management.
/// </summary>
public sealed partial class MusicViewModel : ObservableObject
{
    private readonly IMusicRestClient _music;
    private readonly IMusicPlayerService _player;
    private readonly IEqualizerService _eq;
    private readonly IAlbumArtCache _artCache;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;

    public MusicViewModel(
        IMusicRestClient music,
        IMusicPlayerService player,
        IEqualizerService eq,
        IAlbumArtCache artCache,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore)
    {
        _music = music;
        _player = player;
        _eq = eq;
        _artCache = artCache;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _player.PlaybackStateChanged += (_, _) => UpdatePlaybackState();
        _player.TrackEnded += (_, _) => Dispatch(() => PlayNextCommand.Execute(null));
    }

    /// <summary>
    /// Safely dispatches an action to the main thread. In test context (portable MAUI assemblies),
    /// <see cref="MainThread.BeginInvokeOnMainThread"/> throws <see cref="NotImplementedException"/>;
    /// this wrapper silently swallows that exception so the ViewModel remains testable.
    /// </summary>
    private static void Dispatch(Action action)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
        catch (NotImplementedException)
        {
            // Portable assembly fallback — execute inline in test context
            action();
        }
    }

    private async Task<(string? serverUrl, string? token)> GetCredentialsAsync()
    {
        var conn = _serverStore.GetActive();
        if (conn is null) return (null, null);
        var tok = await _tokenStore.GetAccessTokenAsync(conn.ServerBaseUrl);
        return (conn.ServerBaseUrl, tok);
    }

    // ── Observable properties ──────────────────────────────────────────

    [ObservableProperty]
    private MusicView _currentView = MusicView.Artists;

    [ObservableProperty]
    private ArtistDto? _selectedArtist;

    [ObservableProperty]
    private MusicAlbumDto? _selectedAlbum;

    [ObservableProperty]
    private TrackDto? _selectedTrack;

    [ObservableProperty]
    private PlaylistDto? _selectedPlaylist;

    partial void OnSelectedArtistChanged(ArtistDto? value)
    {
        if (value is not null)
            _ = LoadAlbumsForArtistAsync(value);
    }

    partial void OnSelectedAlbumChanged(MusicAlbumDto? value)
    {
        if (value is not null)
            _ = LoadTracksForAlbumAsync(value);
    }

    partial void OnSelectedTrackChanged(TrackDto? value)
    {
        if (value is not null)
        {
            SelectedTrack = null; // reset so same track can be tapped again
            _ = PlayTrackAsync(value);
        }
    }

    partial void OnSelectedPlaylistChanged(PlaylistDto? value)
    {
        if (value is not null)
            _ = LoadTracksForPlaylistAsync(value);
    }

    [ObservableProperty]
    private string _title = "Music";

    [ObservableProperty]
    private ObservableCollection<ArtistDto> _artists = [];

    [ObservableProperty]
    private ObservableCollection<MusicAlbumDto> _albums = [];

    [ObservableProperty]
    private ObservableCollection<TrackDto> _tracks = [];

    [ObservableProperty]
    private ObservableCollection<PlaylistDto> _playlists = [];

    [ObservableProperty]
    private ObservableCollection<EqPresetDto> _eqPresets = [];

    [ObservableProperty]
    private ObservableCollection<string> _genres = [];

    [ObservableProperty]
    private TrackDto? _currentTrack;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _currentPositionSeconds;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // ── Commands ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListArtistsAsync(serverUrl, token, ct: CancellationToken.None);
            Dispatch(() =>
            {
                Artists = new ObservableCollection<ArtistDto>(items);
                CurrentView = MusicView.Artists;
                Title = "Artists";
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load artists: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task SelectArtistAsync(ArtistDto artist)
    {
        // SelectedArtist is already set by the CollectionView binding;
        // do the actual work via the internal method.
        await LoadAlbumsForArtistAsync(artist);
    }

    private async Task LoadAlbumsForArtistAsync(ArtistDto artist)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListAlbumsByArtistAsync(serverUrl, token, artist.Id, CancellationToken.None);
            Dispatch(() =>
            {
                Albums = new ObservableCollection<MusicAlbumDto>(items);
                CurrentView = MusicView.Albums;
                Title = artist.Name;
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load albums: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task LoadAlbumsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedArtist = null;
            var items = await _music.ListAlbumsAsync(serverUrl, token, ct: CancellationToken.None);
            Dispatch(() =>
            {
                Albums = new ObservableCollection<MusicAlbumDto>(items);
                CurrentView = MusicView.Albums;
                Title = "Albums";
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load albums: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task SelectAlbumAsync(MusicAlbumDto album)
    {
        // SelectedAlbum is already set by the CollectionView binding;
        // do the actual work via the internal method.
        await LoadTracksForAlbumAsync(album);
    }

    private async Task LoadTracksForAlbumAsync(MusicAlbumDto album)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListTracksByAlbumAsync(serverUrl, token, album.Id, CancellationToken.None);

            // Enqueue all tracks and start playing from the first one
            if (items.Count > 0)
            {
                _player.Enqueue(items);
                await _player.PlayAsync(items[0], serverUrl, token);
            }

            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                Title = album.Title;
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load tracks: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task LoadTracksAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedAlbum = null;
            var items = await _music.ListTracksAsync(serverUrl, token, ct: CancellationToken.None);
            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                Title = "Tracks";
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load tracks: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task LoadPlaylistsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListPlaylistsAsync(serverUrl, token, CancellationToken.None);
            Dispatch(() =>
            {
                Playlists = new ObservableCollection<PlaylistDto>(items);
                CurrentView = MusicView.Playlists;
                Title = "Playlists";
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load playlists: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task SelectPlaylistAsync(PlaylistDto playlist)
    {
        // SelectedPlaylist is already set by the CollectionView binding;
        // do the actual work via the internal method.
        await LoadTracksForPlaylistAsync(playlist);
    }

    private async Task LoadTracksForPlaylistAsync(PlaylistDto playlist)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.GetPlaylistTracksAsync(serverUrl, token, playlist.Id, CancellationToken.None);
            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                Title = playlist.Name;
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load playlist tracks: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task LoadEqPresetsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        try
        {
            var items = await _music.ListEqPresetsAsync(serverUrl, token, CancellationToken.None);
            Dispatch(() =>
            EqPresets = new ObservableCollection<EqPresetDto>(items));
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load EQ presets: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PlayTrackAsync(TrackDto track)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        try
        {
            // When playing from album/track list context, enqueue all visible tracks
            // and start playback from the tapped track.
            if (Tracks.Count > 0)
            {
                _player.Enqueue(Tracks);
            }

            await _player.PlayAsync(track, serverUrl, token);
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to play track: {ex.Message}");
        }
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_player.IsPlaying)
            _player.Pause();
        else
            _player.Resume();
    }

    [RelayCommand]
    private void PlayNext() => _player.PlayNext();

    [RelayCommand]
    private void PlayPrevious() => _player.PlayPrevious();

    [RelayCommand]
    private async Task SeekAsync(double pos) => _player.Seek(TimeSpan.FromSeconds(pos));

    [RelayCommand]
    private async Task ToggleStarAsync(object item)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null) return;

        try
        {
            if (item is TrackDto track)
            {
                await _music.ToggleStarAsync(serverUrl, token, track.Id, "track");
            }
            else if (item is MusicAlbumDto album)
            {
                await _music.ToggleStarAsync(serverUrl, token, album.Id, "album");
            }
            else if (item is ArtistDto artist)
            {
                await _music.ToggleStarAsync(serverUrl, token, artist.Id, "artist");
            }
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to toggle star: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplyEqPresetAsync(EqPresetDto preset) => _eq.ApplyPreset(preset);

    [RelayCommand]
    private async Task BackAsync()
    {
        if (CurrentView == MusicView.Tracks && SelectedAlbum is not null)
        {
            // From album tracks back to album list (or artist albums)
            if (SelectedArtist is not null)
            {
                await SelectArtistCommand.ExecuteAsync(SelectedArtist);
            }
            else
            {
                await LoadAlbumsCommand.ExecuteAsync(null);
            }
        }
        else if (CurrentView == MusicView.Tracks || CurrentView == MusicView.Albums)
        {
            await LoadArtistsCommand.ExecuteAsync(null);
        }
        else
        {
            await LoadArtistsCommand.ExecuteAsync(null);
        }
    }

    private void UpdatePlaybackState()
    {
        Dispatch(() =>
        {
            CurrentTrack = _player.CurrentTrack;
            IsPlaying = _player.IsPlaying;
            CurrentPositionSeconds = _player.CurrentPosition.TotalSeconds;
            DurationSeconds = _player.Duration.TotalSeconds;
        });
    }
}
