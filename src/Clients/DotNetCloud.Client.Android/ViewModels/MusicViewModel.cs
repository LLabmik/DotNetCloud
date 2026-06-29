using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Music;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>Which browsing view is currently displayed.</summary>
public enum MusicView { Artists, Albums, Tracks, Playlists, Eq }

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
        if (conn is null)
            return (null, null);
        var tok = await _tokenStore.GetAccessTokenAsync(conn.ServerBaseUrl);
        return (conn.ServerBaseUrl, tok);
    }

    // ── Pagination state (for infinite scroll) ─────────────────────

    private const int PageSize = 50;

    private int _artistsSkip;
    private bool _hasMoreArtists = true;
    private CancellationTokenSource? _artistsLoadCts;

    private int _albumsSkip;
    private bool _hasMoreAlbums = true;
    private CancellationTokenSource? _albumsLoadCts;

    private int _tracksSkip;
    private bool _hasMoreTracks = true;
    private CancellationTokenSource? _tracksLoadCts;

    // ── Filtered-mode state (scoped drill-down) ────────────────────

    /// <summary>When set, albums view is scoped to a single artist; infinite scroll is disabled.</summary>
    private Guid? _albumsFilteredByArtistId;

    /// <summary>When set, tracks view is scoped to a single album; infinite scroll is disabled.</summary>
    private Guid? _tracksFilteredByAlbumId;

    /// <summary>When set, tracks view is scoped to a single playlist; infinite scroll is disabled.</summary>
    private Guid? _tracksFilteredByPlaylistId;

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

    // ── Alphabet index ────────────────────────────────────────────

    /// <summary>Unique sorted first-character strings for the Artists alphabet strip.</summary>
    [ObservableProperty]
    private ObservableCollection<string> _artistAlphabet = [];

    /// <summary>Unique sorted first-character strings for the Albums alphabet strip.</summary>
    [ObservableProperty]
    private ObservableCollection<string> _albumAlphabet = [];

    /// <summary>Unique sorted first-character strings for the Tracks alphabet strip.</summary>
    [ObservableProperty]
    private ObservableCollection<string> _trackAlphabet = [];

    // ── Back-navigation visibility ─────────────────────────────────

    /// <summary>True when albums view is scoped to a specific artist (show back-arrow to artists).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private bool _canGoBackToArtist;

    /// <summary>True when tracks view is scoped to a specific album (show back-arrow to albums).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private bool _canGoBackToAlbum;

    /// <summary>True when tracks view is scoped to a specific playlist (show back-arrow to playlists).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private bool _canGoBackToPlaylist;

    /// <summary>True when any contextual view is active (artist-scoped albums, album-scoped tracks, or playlist tracks).</summary>
    public bool CanGoBack => CanGoBackToArtist || CanGoBackToAlbum || CanGoBackToPlaylist;

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

    // ── EQ state ───────────────────────────────────────────────────

    [ObservableProperty]
    private bool _eqAvailable;

    [ObservableProperty]
    private int _numberOfBands;

    /// <summary>
    /// Current gain levels per band (10 values matching server band frequencies:
    /// 31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 Hz).
    /// Values are in dB (range approximately -12 to +12).
    /// Initialized to all zeros (flat EQ).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<float> _bandLevels = new(Enumerable.Repeat(0f, 10));

    // ── Seek state ─────────────────────────────────────────────────

    /// <summary>
    /// When true, the user is currently dragging the seek slider.
    /// Position updates from the playback timer are suppressed to avoid fighting the user's drag.
    /// </summary>
    [ObservableProperty]
    private bool _isSeeking;

    // ── Album art ──────────────────────────────────────────────────

    /// <summary>Album art image for the currently playing track. Loaded via <see cref="IAlbumArtCache"/>.</summary>
    [ObservableProperty]
    private ImageSource? _albumArtImage;

    /// <summary>True when album art is loaded and available for display.</summary>
    [ObservableProperty]
    private bool _hasAlbumArt;

    private CancellationTokenSource? _albumArtLoadCts;
    private TrackDto? _lastArtTrack;

    // ── Scroll-to-character delegate ───────────────────────────────

    /// <summary>
    /// Delegated to the code-behind so it can call <see cref="Microsoft.Maui.Controls.CollectionView.ScrollTo"/>.
    /// Invoked when the user taps a character in the alphabet index strip.
    /// </summary>
    public Action<object?, MusicView>? ScrollToRequested;

    // ── Commands ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
        _artistsSkip = 0;
        _hasMoreArtists = true;
        _artistsLoadCts?.Cancel();

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListArtistsAsync(serverUrl, token, skip: 0, take: PageSize, ct: CancellationToken.None);
            Dispatch(() =>
            {
                Artists = new ObservableCollection<ArtistDto>(items);
                CurrentView = MusicView.Artists;
                Title = "Artists";
                _ = LoadArtistAlphabetAsync(serverUrl, token);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = false;
                CanGoBackToPlaylist = false;
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
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListAlbumsByArtistAsync(serverUrl, token, artist.Id, CancellationToken.None);
            _albumsFilteredByArtistId = artist.Id;
            Dispatch(() =>
            {
                Albums = new ObservableCollection<MusicAlbumDto>(items);
                CurrentView = MusicView.Albums;
                Title = artist.Name;
                AlbumAlphabet = ComputeAlphabetLocal(items, a => a.Title);
                CanGoBackToArtist = true;
                CanGoBackToAlbum = false;
                CanGoBackToPlaylist = false;
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
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
        _albumsSkip = 0;
        _hasMoreAlbums = true;
        _albumsLoadCts?.Cancel();
        _albumsFilteredByArtistId = null;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedArtist = null;
            var items = await _music.ListAlbumsAsync(serverUrl, token, skip: 0, take: PageSize, ct: CancellationToken.None);
            Dispatch(() =>
            {
                Albums = new ObservableCollection<MusicAlbumDto>(items);
                CurrentView = MusicView.Albums;
                Title = "Albums";
                _ = LoadAlbumAlphabetAsync(serverUrl, token);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = false;
                CanGoBackToPlaylist = false;
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
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListTracksByAlbumAsync(serverUrl, token, album.Id, CancellationToken.None);
            _tracksFilteredByAlbumId = album.Id;

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
                TrackAlphabet = ComputeAlphabetLocal(items, t => t.Title);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = true;
                CanGoBackToPlaylist = false;
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
        if (serverUrl is null || token is null)
            return;

        // Reset pagination for fresh load
        _tracksSkip = 0;
        _hasMoreTracks = true;
        _tracksLoadCts?.Cancel();
        _tracksFilteredByAlbumId = null;
        _tracksFilteredByPlaylistId = null;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedAlbum = null;
            var items = await _music.ListTracksAsync(serverUrl, token, skip: 0, take: PageSize, ct: CancellationToken.None);
            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                Title = "Tracks";
                _ = LoadTrackAlphabetAsync(serverUrl, token);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = false;
                CanGoBackToPlaylist = false;
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
        if (serverUrl is null || token is null)
            return;

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
    private async Task LoadMoreArtistsAsync()
    {
        if (!_hasMoreArtists || IsLoading)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        _artistsLoadCts?.Cancel();
        _artistsLoadCts = new CancellationTokenSource();
        var ct = _artistsLoadCts.Token;

        try
        {
            var nextSkip = _artistsSkip + PageSize;
            var items = await _music.ListArtistsAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
            if (ct.IsCancellationRequested)
                return;

            _artistsSkip = nextSkip;
            if (items.Count < PageSize)
                _hasMoreArtists = false;

            Dispatch(() =>
            {
                foreach (var artist in items)
                    Artists.Add(artist);
            });
        }
        catch (OperationCanceledException) { /* cancelled — ignore */ }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load more artists: {ex.Message}");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task LoadMoreAlbumsAsync()
    {
        if (!_hasMoreAlbums || IsLoading)
            return;

        // When viewing albums scoped to a specific artist, don't load all albums
        if (_albumsFilteredByArtistId is not null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        _albumsLoadCts?.Cancel();
        _albumsLoadCts = new CancellationTokenSource();
        var ct = _albumsLoadCts.Token;

        try
        {
            var nextSkip = _albumsSkip + PageSize;
            var items = await _music.ListAlbumsAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
            if (ct.IsCancellationRequested)
                return;

            _albumsSkip = nextSkip;
            if (items.Count < PageSize)
                _hasMoreAlbums = false;

            Dispatch(() =>
            {
                foreach (var album in items)
                    Albums.Add(album);
            });
        }
        catch (OperationCanceledException) { /* cancelled */ }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load more albums: {ex.Message}");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task LoadMoreTracksAsync()
    {
        if (!_hasMoreTracks || IsLoading)
            return;

        // When viewing tracks scoped to a specific album or playlist, don't load all tracks
        if (_tracksFilteredByAlbumId is not null || _tracksFilteredByPlaylistId is not null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        _tracksLoadCts?.Cancel();
        _tracksLoadCts = new CancellationTokenSource();
        var ct = _tracksLoadCts.Token;

        try
        {
            var nextSkip = _tracksSkip + PageSize;
            var items = await _music.ListTracksAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
            if (ct.IsCancellationRequested)
                return;

            _tracksSkip = nextSkip;
            if (items.Count < PageSize)
                _hasMoreTracks = false;

            Dispatch(() =>
            {
                foreach (var track in items)
                    Tracks.Add(track);
            });
        }
        catch (OperationCanceledException) { /* cancelled */ }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load more tracks: {ex.Message}");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
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
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.GetPlaylistTracksAsync(serverUrl, token, playlist.Id, CancellationToken.None);
            _tracksFilteredByPlaylistId = playlist.Id;
            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                Title = playlist.Name;
                TrackAlphabet = ComputeAlphabetLocal(items, t => t.Title);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = false;
                CanGoBackToPlaylist = true;
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
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListEqPresetsAsync(serverUrl, token, CancellationToken.None);
            Dispatch(() =>
            {
                EqPresets = new ObservableCollection<EqPresetDto>(items);
                EqAvailable = _eq.IsAvailable;
                NumberOfBands = _eq.NumberOfBands;
                CurrentView = MusicView.Eq;
                Title = "Equalizer";
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load EQ presets: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task PlayTrackAsync(TrackDto track)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

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

    /// <summary>
    /// Seeks to the specified position (seconds). Called on seek-slider drag-completed.
    /// </summary>
    [RelayCommand]
    private void SeekTo(double pos)
    {
        _player.Seek(TimeSpan.FromSeconds(pos));
        CurrentPositionSeconds = pos;
        IsSeeking = false;
    }

    [RelayCommand]
    private async Task ToggleStarAsync(object item)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

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

    /// <summary>Resets the equalizer to flat (0 dB on all bands), clearing any applied preset.</summary>
    [RelayCommand]
    private void ResetEq()
    {
        _eq.Reset();
        Dispatch(() =>
        {
            for (int i = 0; i < BandLevels.Count; i++)
                BandLevels[i] = 0f;
        });
    }

    [RelayCommand]
    private async Task ApplyEqPresetAsync(EqPresetDto preset)
    {
        _eq.ApplyPreset(preset);

        // Update the band levels visualization from the preset's band dictionary
        var serverFreqs = new[] { "31", "63", "125", "250", "500", "1000", "2000", "4000", "8000", "16000" };
        Dispatch(() =>
        {
            for (int i = 0; i < serverFreqs.Length && i < BandLevels.Count; i++)
            {
                if (preset.Bands.TryGetValue(serverFreqs[i], out var gainDb))
                    BandLevels[i] = (float)gainDb;
                else
                    BandLevels[i] = 0f;
            }
        });
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (CurrentView == MusicView.Tracks && CanGoBackToPlaylist)
        {
            // From playlist tracks back to Playlists
            if (Playlists.Count == 0)
                await LoadPlaylistsCommand.ExecuteAsync(null);
            else
            {
                _tracksFilteredByPlaylistId = null;
                Dispatch(() =>
                {
                    CurrentView = MusicView.Playlists;
                    Title = "Playlists";
                    CanGoBackToPlaylist = false;
                });
            }
        }
        else if (CurrentView == MusicView.Tracks && CanGoBackToAlbum)
        {
            // From album tracks back to that artist's albums (or all albums if no artist context)
            _tracksFilteredByAlbumId = null;
            if (SelectedArtist is not null)
            {
                await SelectArtistCommand.ExecuteAsync(SelectedArtist);
            }
            else
            {
                if (Albums.Count == 0)
                    await LoadAlbumsCommand.ExecuteAsync(null);
                else
                    SwitchToAlbumsView();
            }
        }
        else if (CurrentView == MusicView.Tracks)
        {
            // From "All Tracks" back to Artists
            if (Artists.Count == 0)
                await LoadArtistsCommand.ExecuteAsync(null);
            else
                SwitchToArtistsView();
        }
        else if (CurrentView == MusicView.Albums && CanGoBackToArtist)
        {
            // From artist-scoped albums back to Artists
            _albumsFilteredByArtistId = null;
            if (Artists.Count == 0)
                await LoadArtistsCommand.ExecuteAsync(null);
            else
                SwitchToArtistsView();
        }
        else if (CurrentView == MusicView.Albums)
        {
            // From all albums back to Artists
            if (Artists.Count == 0)
                await LoadArtistsCommand.ExecuteAsync(null);
            else
                SwitchToArtistsView();
        }
        else if (CurrentView == MusicView.Eq)
        {
            // From EQ back to Artists
            if (Artists.Count == 0)
                await LoadArtistsCommand.ExecuteAsync(null);
            else
                SwitchToArtistsView();
        }
        else
        {
            await LoadArtistsCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Switches view to Artists without reloading data from server.</summary>
    private void SwitchToArtistsView()
    {
        Dispatch(() =>
        {
            _albumsFilteredByArtistId = null;
            _tracksFilteredByAlbumId = null;
            _tracksFilteredByPlaylistId = null;
            SelectedArtist = null;
            SelectedAlbum = null;
            CurrentView = MusicView.Artists;
            Title = "Artists";
            CanGoBackToArtist = false;
            CanGoBackToAlbum = false;
            CanGoBackToPlaylist = false;
        });
    }

    /// <summary>Switches view to Albums without reloading data from server.</summary>
    private void SwitchToAlbumsView()
    {
        Dispatch(() =>
        {
            _tracksFilteredByAlbumId = null;
            _tracksFilteredByPlaylistId = null;
            SelectedArtist = null;
            SelectedAlbum = null;
            CurrentView = MusicView.Albums;
            Title = "Albums";
            CanGoBackToArtist = false;
            CanGoBackToAlbum = false;
            CanGoBackToPlaylist = false;
        });
    }

    private void UpdatePlaybackState()
    {
        Dispatch(() =>
        {
            var newTrack = _player.CurrentTrack;
            CurrentTrack = newTrack;
            IsPlaying = _player.IsPlaying;
            DurationSeconds = _player.Duration.TotalSeconds;

            // Don't overwrite position while user is dragging the slider
            if (!IsSeeking)
                CurrentPositionSeconds = _player.CurrentPosition.TotalSeconds;

            // Load album art when track changes
            if (newTrack is not null && newTrack != _lastArtTrack)
            {
                _lastArtTrack = newTrack;
                _ = LoadAlbumArtForCurrentTrackAsync();
            }
            else if (newTrack is null)
            {
                _lastArtTrack = null;
                ClearAlbumArt();
            }
        });
    }

    // ── Alphabet index helpers ──────────────────────────────────────

    /// <summary>
    /// Computes the unique sorted set of first characters from a locally-loaded collection
    /// (used for filtered views where all items are already in memory).
    /// </summary>
    private static ObservableCollection<string> ComputeAlphabetLocal<T>(IReadOnlyList<T> items, Func<T, string?> nameSelector)
    {
        var chars = new SortedSet<char>();
        foreach (var item in items)
        {
            var name = nameSelector(item);
            if (!string.IsNullOrEmpty(name))
            {
                var c = char.ToUpperInvariant(name[0]);
                if (char.IsLetterOrDigit(c))
                    chars.Add(c);
            }
        }
        return new ObservableCollection<string>(chars.Select(c => c.ToString()));
    }

    /// <summary>Loads the artist alphabet from the server (scans all entries efficiently).</summary>
    private async Task LoadArtistAlphabetAsync(string serverUrl, string token)
    {
        try
        {
            var chars = await _music.GetArtistAlphabetAsync(serverUrl, token);
            Dispatch(() => ArtistAlphabet = new ObservableCollection<string>(chars));
        }
        catch
        {
            // Best-effort — strip will be empty
        }
    }

    /// <summary>Loads the album alphabet from the server (scans all entries efficiently).</summary>
    private async Task LoadAlbumAlphabetAsync(string serverUrl, string token)
    {
        try
        {
            var chars = await _music.GetAlbumAlphabetAsync(serverUrl, token);
            Dispatch(() => AlbumAlphabet = new ObservableCollection<string>(chars));
        }
        catch
        {
            // Best-effort
        }
    }

    /// <summary>Loads the track alphabet from the server (scans all entries efficiently).</summary>
    private async Task LoadTrackAlphabetAsync(string serverUrl, string token)
    {
        try
        {
            var chars = await _music.GetTrackAlphabetAsync(serverUrl, token);
            Dispatch(() => TrackAlphabet = new ObservableCollection<string>(chars));
        }
        catch
        {
            // Best-effort
        }
    }

    [RelayCommand]
    private void ScrollToCharacter(string character)
    {
        if (string.IsNullOrEmpty(character))
            return;

        object? target = null;
        var c = character[0];

        if (CurrentView == MusicView.Artists)
        {
            target = Artists.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Name) && char.ToUpperInvariant(a.Name[0]) == c);
        }
        else if (CurrentView == MusicView.Albums)
        {
            target = Albums.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Title) && char.ToUpperInvariant(a.Title[0]) == c);
        }
        else if (CurrentView == MusicView.Tracks)
        {
            target = Tracks.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.Title) && char.ToUpperInvariant(t.Title[0]) == c);
        }

        if (target is not null)
            ScrollToRequested?.Invoke(target, CurrentView);
    }

    // ── Album art helpers ────────────────────────────────────────────

    private async Task LoadAlbumArtForCurrentTrackAsync()
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null || CurrentTrack is null)
            return;

        await LoadAlbumArtAsync(CurrentTrack, serverUrl, token);
    }

    /// <summary>
    /// Loads album art for the given track's album, if it has one.
    /// Cancels any in-flight art load to avoid stale images on rapid track changes.
    /// </summary>
    private async Task LoadAlbumArtAsync(TrackDto track, string serverUrl, string token)
    {
        if (track.AlbumId is null)
        {
            ClearAlbumArt();
            return;
        }

        _albumArtLoadCts?.Cancel();
        _albumArtLoadCts = new CancellationTokenSource();
        var ct = _albumArtLoadCts.Token;

        try
        {
            var source = await _artCache.GetAlbumArtAsync(track.AlbumId.Value, serverUrl, token, ct);
            if (ct.IsCancellationRequested)
                return;

            Dispatch(() =>
            {
                AlbumArtImage = source;
                HasAlbumArt = source is not null;
            });
        }
        catch (OperationCanceledException) { /* cancelled */ }
        catch
        {
            Dispatch(() => HasAlbumArt = false);
        }
    }

    /// <summary>Clears the currently displayed album art.</summary>
    private void ClearAlbumArt()
    {
        _albumArtLoadCts?.Cancel();
        Dispatch(() =>
        {
            AlbumArtImage = null;
            HasAlbumArt = false;
        });
    }
}
