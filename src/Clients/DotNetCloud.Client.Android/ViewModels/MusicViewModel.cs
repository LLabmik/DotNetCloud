using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Core;
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
        _player.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.TrackStarted += OnPlayerTrackStarted;
        _player.TrackEnded += OnPlayerTrackEnded;
        _player.RepeatModeChanged += OnRepeatModeChanged;
        _eq.AvailabilityChanged += OnEqAvailabilityChanged;
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

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => UpdatePlaybackState();

    private void OnPlayerTrackStarted(object? sender, EventArgs e)
    {
        RecordPlayFireAndForget();
        // When the whole-library queue is near its loaded end, prefetch the next page
        // so playback keeps advancing through the rest of the list in display order.
        Dispatch(MaybePrefetchQueue);
    }

    private void OnPlayerTrackEnded(object? sender, EventArgs e) =>
        Dispatch(() => _player.PlayNextAfterEnd());

    private void OnRepeatModeChanged(object? sender, EventArgs e) => Dispatch(UpdateRepeatState);

    private void OnEqAvailabilityChanged(object? sender, EventArgs e) => Dispatch(InitEqFromDevice);

    private async Task<(string? serverUrl, string? token)> GetCredentialsAsync()
    {
        var conn = _serverStore.GetActive();
        if (conn is null)
            return (null, null);
        var tok = await _tokenStore.GetAccessTokenAsync(conn.ServerBaseUrl);
        return (conn.ServerBaseUrl, tok);
    }

    /// <summary>
    /// Fire-and-forget recording of the current track play. Runs off the UI thread so a slow
    /// network call never blocks playback; failures are logged and ignored.
    /// </summary>
    private async void RecordPlayFireAndForget()
    {
        try
        {
            var track = _player.CurrentTrack;
            if (track is null)
                return;
            var (serverUrl, token) = await GetCredentialsAsync();
            if (serverUrl is null || token is null)
                return;
            await _music.RecordPlayAsync(serverUrl, token, track.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Music] RecordPlay failed: {ex.Message}");
        }
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

    // ── Whole-library queue prefetch state ────────────────────────────

    /// <summary>
    /// True while the current playback queue is backed by the paginated "All Tracks"
    /// list, so newly fetched pages are appended to the queue to keep playing through
    /// the whole library in display order.
    /// </summary>
    private bool _libraryQueueActive;

    /// <summary>Guards concurrent next-page fetches (user scroll vs playback prefetch).</summary>
    private bool _tracksPageFetchInProgress;

    /// <summary>Remaining queued tracks that trigger a prefetch of the next page.</summary>
    private const int QueuePrefetchThreshold = 4;

    // ── Filtered-mode state (scoped drill-down) ────────────────────

    /// <summary>When set, albums view is scoped to a single artist; infinite scroll is disabled.</summary>
    private Guid? _albumsFilteredByArtistId;

    /// <summary>When set, tracks view is scoped to a single album; infinite scroll is disabled.</summary>
    private Guid? _tracksFilteredByAlbumId;

    /// <summary>When set, tracks view is scoped to a single playlist; infinite scroll is disabled.</summary>
    private Guid? _tracksFilteredByPlaylistId;

    /// <summary>Tracks the last non-EQ view so the EQ button can toggle back to it.</summary>
    private MusicView _previousNonEqView = MusicView.Artists;

    // ── Search state ───────────────────────────────────────────────

    private CancellationTokenSource? _searchCts;

    /// <summary>Saved pre-search collections to restore on search close.</summary>
    private ObservableCollection<ArtistDto>? _preSearchArtists;

    /// <summary>Saved pre-search collections to restore on search close.</summary>
    private ObservableCollection<MusicAlbumDto>? _preSearchAlbums;

    /// <summary>Saved pre-search collections to restore on search close.</summary>
    private ObservableCollection<TrackDto>? _preSearchTracks;

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

    // ── Search observable properties ───────────────────────────────

    /// <summary>Whether the search panel is open.</summary>
    [ObservableProperty]
    private bool _isSearchOpen;

    /// <summary>Current search query text.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>Search result status text (e.g. &quot;12 results&quot; or &quot;No results&quot;).</summary>
    [ObservableProperty]
    private string? _searchResultText;

    /// <summary>Whether a server-side search is in flight.</summary>
    [ObservableProperty]
    private bool _isSearching;

    /// <summary>Placeholder text for the search Entry (varies by active tab).</summary>
    [ObservableProperty]
    private string _searchPlaceholderText = "Search…";

    partial void OnSearchQueryChanged(string value)
    {
        _ = SearchAsync(value);
    }

    partial void OnIsSearchOpenChanged(bool value)
    {
        if (value)
        {
            // Save pre-search collections when opening
            _preSearchArtists = Artists;
            _preSearchAlbums = Albums;
            _preSearchTracks = Tracks;
            SearchResultText = null;
            ErrorMessage = null;
        }
    }

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

    /// <summary>True when there are presets available (for toggling overwrite section visibility).</summary>
    public bool HasEqPresets => EqPresets.Count > 0;

    partial void OnEqPresetsChanged(ObservableCollection<EqPresetDto> value)
    {
        OnPropertyChanged(nameof(HasEqPresets));
    }

    [ObservableProperty]
    private ObservableCollection<string> _genres = [];

    // ── Save EQ Preset dialog state ──────────────────────────────

    /// <summary>Whether the "Save EQ Preset" dialog is visible.</summary>
    [ObservableProperty]
    private bool _showSavePresetDialog;

    /// <summary>Name entered by the user for the new/updated preset.</summary>
    [ObservableProperty]
    private string _newPresetName = string.Empty;

    /// <summary>Set when the user selects an existing preset to overwrite.</summary>
    private Guid? _selectedPresetId;

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

    /// <summary>Number of virtual bands (always 10, matching server/Blazor preset format).</summary>
    [ObservableProperty]
    private int _numberOfBands;

    /// <summary>Number of physical device bands (informational, typically 5–6).</summary>
    [ObservableProperty]
    private int _physicalBandCount;

    /// <summary>
    /// EQ band models for the 10 virtual server-standard bands (31 Hz – 16 kHz).
    /// Each <see cref="EqBandModel"/> has two-way bindable <see cref="EqBandModel.GainDb"/>
    /// that maps to the closest physical device band via <see cref="IEqualizerService.SetVirtualBandGain"/>.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<EqBandModel> _eqBands = [];

    // ── Seek state ─────────────────────────────────────────────────

    /// <summary>
    /// When true, the user is currently dragging the seek slider.
    /// Position updates from the playback timer are suppressed to avoid fighting the user's drag.
    /// </summary>
    [ObservableProperty]
    private bool _isSeeking;

    // ── Repeat state ────────────────────────────────────────────────

    /// <summary>Current repeat mode — synced from <see cref="IMusicPlayerService.RepeatMode"/>.</summary>
    [ObservableProperty]
    private RepeatMode _repeatMode;

    /// <summary>Repeat icon character: 🔁 for Off/All, 🔂 for One.</summary>
    [ObservableProperty]
    private string _repeatIcon = "🔁";

    /// <summary>Repeat label text: "Off", "One", or "All".</summary>
    [ObservableProperty]
    private string _repeatLabel = "Off";

    /// <summary>True when repeat mode is not Off (used for active styling).</summary>
    [ObservableProperty]
    private bool _isRepeatActive;

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
    /// Delegated to the code-behind so it can call <c>CollectionView.ScrollTo</c>.
    /// Invoked when the user taps a character in the alphabet index strip.
    /// </summary>
    public Action<object?, MusicView>? ScrollToRequested;

    // ── Search commands ────────────────────────────────────────────────

    /// <summary>Toggles the search panel open/close.</summary>
    [RelayCommand]
    private void ToggleSearch()
    {
        if (IsSearchOpen)
        {
            CloseSearch();
        }
        else
        {
            IsSearchOpen = true;
        }
    }

    /// <summary>Closes the search panel and restores original data.</summary>
    [RelayCommand]
    private void CloseSearch()
    {
        _searchCts?.Cancel();

        if (IsSearchOpen)
        {
            if (_preSearchArtists is not null)
                Artists = _preSearchArtists;
            if (_preSearchAlbums is not null)
                Albums = _preSearchAlbums;
            if (_preSearchTracks is not null)
                Tracks = _preSearchTracks;
        }

        _preSearchArtists = null;
        _preSearchAlbums = null;
        _preSearchTracks = null;
        IsSearchOpen = false;
        SearchQuery = string.Empty;
        SearchResultText = null;
        IsSearching = false;
    }

    /// <summary>
    /// Debounced server-side search. Called automatically when <see cref="SearchQuery"/> changes
    /// via the source-generated <c>OnSearchQueryChanged</c> partial method.
    /// Fans out to the correct search endpoint based on <see cref="CurrentView"/>.
    /// </summary>
    private async Task SearchAsync(string query)
    {
        // Cancel any in-flight search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        // If query is empty/whitespace, restore original collections
        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearching = false;
            SearchResultText = null;
            RestorePreSearchCollections();
            return;
        }

        // Debounce: wait 300ms before firing
        try
        {
            await Task.Delay(300, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
        {
            ErrorMessage = "Not connected to server";
            return;
        }

        IsSearching = true;
        ErrorMessage = null;

        try
        {
            int count;

            switch (CurrentView)
            {
                case MusicView.Artists:
                    var artists = await _music.SearchArtistsAsync(serverUrl, token, query, take: 50, ct: ct);
                    if (ct.IsCancellationRequested)
                        return;
                    count = artists.Count;
                    Dispatch(() =>
                    {
                        Artists = new ObservableCollection<ArtistDto>(artists);
                        ArtistAlphabet = ComputeAlphabetLocal(artists, a => a.Name);
                    });
                    break;

                case MusicView.Albums:
                    var albums = await _music.SearchAlbumsAsync(serverUrl, token, query, take: 50, ct: ct);
                    if (ct.IsCancellationRequested)
                        return;
                    count = albums.Count;
                    Dispatch(() =>
                    {
                        Albums = new ObservableCollection<MusicAlbumDto>(albums);
                        AlbumAlphabet = ComputeAlphabetLocal(albums, a => a.Title);
                    });
                    break;

                case MusicView.Tracks:
                    var tracks = await _music.SearchTracksAsync(serverUrl, token, query, take: 50, ct: ct);
                    if (ct.IsCancellationRequested)
                        return;
                    count = tracks.Count;
                    Dispatch(() =>
                    {
                        Tracks = new ObservableCollection<TrackDto>(tracks);
                        TrackAlphabet = ComputeAlphabetLocal(tracks, t => t.Title);
                    });
                    break;

                default:
                    // Playlists and EQ views — search not applicable
                    count = 0;
                    break;
            }

            if (!ct.IsCancellationRequested)
            {
                Dispatch(() =>
                {
                    SearchResultText = count == 0
                        ? $"No results for \"{query}\""
                        : $"{count} result{(count != 1 ? "s" : "")} for \"{query}\"";
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled — do nothing
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex));
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => IsSearching = false);
        }
    }

    /// <summary>
    /// Restores the pre-search collections when the user clears the search query.
    /// Only restores if pre-search collections were saved.
    /// </summary>
    private void RestorePreSearchCollections()
    {
        Dispatch(() =>
        {
            if (_preSearchArtists is not null && CurrentView == MusicView.Artists)
                Artists = _preSearchArtists;
            if (_preSearchAlbums is not null && CurrentView == MusicView.Albums)
                Albums = _preSearchAlbums;
            if (_preSearchTracks is not null && CurrentView == MusicView.Tracks)
                Tracks = _preSearchTracks;
        });
    }

    // ── Commands ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        // Close search if open — tab switch clears search state
        IsSearchOpen = false;
        SearchQuery = string.Empty;

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
            Dispatch(() => ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex));
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
        // Close search if open — tab switch clears search state
        IsSearchOpen = false;
        SearchQuery = string.Empty;

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

            // Replace queue with all album tracks and start playing from the first one
            if (items.Count > 0)
            {
                // An album is fully queued — never prefetch more whole-library pages.
                _libraryQueueActive = false;
                _player.ReplaceQueue(items, albumId: album.Id, playlistId: null);
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
        // Close search if open — tab switch clears search state
        IsSearchOpen = false;
        SearchQuery = string.Empty;

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
        if (IsSearchOpen || !_hasMoreArtists || IsLoading)
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
        if (IsSearchOpen || !_hasMoreAlbums || IsLoading)
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
        // Delegates to the shared next-page fetcher so user scrolls and playback
        // prefetches share one guarded code path (no duplicate page fetches).
        await FetchNextTracksPageAsync();
    }

    /// <summary>
    /// Fetches the next page of the "All Tracks" list and appends it to the displayed
    /// <see cref="Tracks"/> collection. When the current playback queue is backed by this
    /// same list (<see cref="_libraryQueueActive"/>), the page is also appended to the
    /// player queue so playback continues through the whole library in display order.
    /// </summary>
    private async Task FetchNextTracksPageAsync()
    {
        if (IsSearchOpen || !_hasMoreTracks || IsLoading || _tracksPageFetchInProgress)
            return;

        // When viewing tracks scoped to a specific album or playlist, there is no
        // further pagination — the whole scoped list is already loaded.
        if (_tracksFilteredByAlbumId is not null || _tracksFilteredByPlaylistId is not null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        _tracksPageFetchInProgress = true;
        ErrorMessage = null;
        _tracksLoadCts?.Cancel();
        _tracksLoadCts = new CancellationTokenSource();
        var ct = _tracksLoadCts.Token;

        // Snapshot the queue-extension intent at fetch time so a context change while
        // the fetch is in flight cannot append library tracks onto a newer queue.
        var shouldExtendQueue = _libraryQueueActive;

        try
        {
            var nextSkip = _tracksSkip + PageSize;
            var items = await _music.ListTracksAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
            if (ct.IsCancellationRequested)
                return;

            if (items.Count == 0)
            {
                _hasMoreTracks = false;
                return;
            }

            _tracksSkip = nextSkip;
            if (items.Count < PageSize)
                _hasMoreTracks = false;

            Dispatch(() =>
            {
                foreach (var track in items)
                    Tracks.Add(track);
            });

            if (shouldExtendQueue && _libraryQueueActive)
            {
                _player.Enqueue(items);
                System.Diagnostics.Debug.WriteLine($"[Music] FetchNextTracksPageAsync: enqueued {items.Count} more tracks (skip={_tracksSkip}, queue={_player.QueueLength})");
            }
        }
        catch (OperationCanceledException) { /* cancelled */ }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => ErrorMessage = $"Failed to load more tracks: {ex.Message}");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                Dispatch(() => IsLoading = false);
            _tracksPageFetchInProgress = false;
        }
    }

    /// <summary>
    /// Prefetches the next "All Tracks" page when playback is near the end of the
    /// currently queued (loaded) tracks, so the whole library keeps playing in the
    /// order it is displayed.
    /// </summary>
    private void MaybePrefetchQueue()
    {
        if (!_libraryQueueActive || !_hasMoreTracks || _player.QueueLength == 0)
            return;

        var remaining = _player.QueueLength - _player.QueuePosition - 1;
        System.Diagnostics.Debug.WriteLine($"[Music] MaybePrefetchQueue: remaining={remaining} threshold={QueuePrefetchThreshold} hasMore={_hasMoreTracks}");
        if (remaining <= QueuePrefetchThreshold)
        {
            System.Diagnostics.Debug.WriteLine("[Music] MaybePrefetchQueue: fetching next page to keep playing through the list");
            _ = FetchNextTracksPageAsync();
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

            // Replace queue with all playlist tracks and start playing from the first one
            if (items.Count > 0)
            {
                // A playlist is fully queued — never prefetch more whole-library pages.
                _libraryQueueActive = false;
                _player.ReplaceQueue(items, albumId: null, playlistId: playlist.Id);
                await _player.PlayAsync(items[0], serverUrl, token);
            }

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

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadEqPresetsAsync()
    {
        // Toggle: if EQ is already showing, go back to the previous non-EQ view.
        if (CurrentView == MusicView.Eq)
        {
            NavigateToView(_previousNonEqView);
            return;
        }

        // Save the current view so we can return to it when toggling EQ off.
        _previousNonEqView = CurrentView;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Show the EQ view immediately — don't wait for the server presets API.
        // The EQ tab must always be navigable regardless of server state.
        Dispatch(() =>
        {
            if (_eq.IsAvailable)
                InitEqFromDevice();
            else
            {
                EqAvailable = false;
                PhysicalBandCount = 0;
                NumberOfBands = _eq.VirtualBandCount;
                EqBands.Clear();
            }
            System.Diagnostics.Debug.WriteLine($"[EQ] IsAvailable={_eq.IsAvailable}, EqBands.Count={EqBands.Count}");
            CurrentView = MusicView.Eq;
            Title = "Equalizer";
        });

        // Load server presets asynchronously — failures only affect the preset list,
        // not the EQ view itself.
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListEqPresetsAsync(serverUrl, token, CancellationToken.None);
            Dispatch(() => EqPresets = new ObservableCollection<EqPresetDto>(items));
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
            // Queue up the currently displayed track list so playback auto-advances
            // to the next track in display order when one finishes. Membership is
            // matched by Id (not reference/value equality) so a tapped row always
            // repositions onto the visible list instead of collapsing to a standalone
            // single track.
            var inDisplayedList = Tracks.Count > 0 && Tracks.Any(t => t.Id == track.Id);
            if (inDisplayedList)
            {
                // Search results aren't a scoped album/playlist context, so
                // don't carry stale filter context while a search is active.
                var albumId = IsSearchOpen ? null : _tracksFilteredByAlbumId;
                var playlistId = IsSearchOpen ? null : _tracksFilteredByPlaylistId;

                // The queue is only "library-backed" (eligible for whole-list prefetch)
                // when built from the unbounded All Tracks list — not an album, a
                // playlist, search results, or a standalone track.
                _libraryQueueActive = !IsSearchOpen
                    && CurrentView == MusicView.Tracks
                    && albumId is null
                    && playlistId is null;

                _player.ReplaceQueue(Tracks.ToList(), albumId, playlistId);
                System.Diagnostics.Debug.WriteLine($"[Music] PlayTrackAsync: queued {Tracks.Count} tracks from list, albumId={albumId}, playlistId={playlistId}, library={_libraryQueueActive}");
            }
            else
            {
                _libraryQueueActive = false;
                System.Diagnostics.Debug.WriteLine($"[Music] PlayTrackAsync: track NOT in Tracks list (count={Tracks.Count}) — standalone play");
            }

            await _player.PlayAsync(track, serverUrl, token);

            // If the tapped track sits near the end of the loaded page(s), fetch the
            // next page right away so playback can continue past the loaded boundary.
            if (_libraryQueueActive)
                MaybePrefetchQueue();
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
    private void CycleRepeat() => _player.CycleRepeat();

    private void UpdateRepeatState()
    {
        var mode = _player.RepeatMode;
        RepeatMode = mode;
        RepeatIcon = mode == RepeatMode.One ? "🔂" : "🔁";
        RepeatLabel = mode switch
        {
            RepeatMode.Off => "Off",
            RepeatMode.One => "One",
            RepeatMode.All => "All",
            _ => "Off",
        };
        IsRepeatActive = mode != RepeatMode.Off;
    }

    [RelayCommand]
    private async Task NavigateToPlayingArtistAsync()
    {
        var track = _player.CurrentTrack;
        if (track is null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListAlbumsByArtistAsync(serverUrl, token, track.ArtistId, CancellationToken.None);
            _albumsFilteredByArtistId = track.ArtistId;
            Dispatch(() =>
            {
                Albums = new ObservableCollection<MusicAlbumDto>(items);
                CurrentView = MusicView.Albums;
                Title = track.ArtistName;
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
    private async Task NavigateToCurrentSourceAsync()
    {
        var track = _player.CurrentTrack;
        if (track is null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        // Prefer playlist context, then album context
        if (_player.PlayingPlaylistId is not null)
        {
            await NavigateToPlaylistTracksAsync(_player.PlayingPlaylistId.Value, serverUrl, token);
        }
        else if (_player.PlayingAlbumId is not null || track.AlbumId is not null)
        {
            var albumId = _player.PlayingAlbumId ?? track.AlbumId!.Value;
            await NavigateToAlbumTracksAsync(albumId, serverUrl, token);
        }
    }

    /// <summary>
    /// Loads tracks for the given playlist and switches to the tracks view,
    /// without restarting playback.
    /// </summary>
    private async Task NavigateToPlaylistTracksAsync(Guid playlistId, string serverUrl, string token)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.GetPlaylistTracksAsync(serverUrl, token, playlistId, CancellationToken.None);
            _tracksFilteredByPlaylistId = playlistId;
            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                TrackAlphabet = ComputeAlphabetLocal(items, t => t.Title);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = false;
                CanGoBackToPlaylist = true;
                // Set Title to the playlist name by loading playlists if needed
                _ = SetTitleFromPlaylistIdAsync(playlistId);
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

    /// <summary>
    /// Loads tracks for the given album and switches to the tracks view,
    /// without restarting playback.
    /// </summary>
    private async Task NavigateToAlbumTracksAsync(Guid albumId, string serverUrl, string token)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _music.ListTracksByAlbumAsync(serverUrl, token, albumId, CancellationToken.None);
            _tracksFilteredByAlbumId = albumId;
            Dispatch(() =>
            {
                Tracks = new ObservableCollection<TrackDto>(items);
                CurrentView = MusicView.Tracks;
                TrackAlphabet = ComputeAlphabetLocal(items, t => t.Title);
                CanGoBackToArtist = false;
                CanGoBackToAlbum = true;
                CanGoBackToPlaylist = false;
                _ = SetTitleFromAlbumIdAsync(albumId, serverUrl, token);
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to load album tracks: {ex.Message}");
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    /// <summary>Sets the Title from the album's title, loading album info if needed.</summary>
    private async Task SetTitleFromAlbumIdAsync(Guid albumId, string serverUrl, string token)
    {
        try
        {
            var album = await _music.GetAlbumAsync(serverUrl, token, albumId, CancellationToken.None);
            Dispatch(() => Title = album?.Title ?? "Unknown Album");
        }
        catch
        {
            // Fallback — already showing Tracks
        }
    }

    /// <summary>Sets the Title from the playlist's name, loading playlist info if needed.</summary>
    private async Task SetTitleFromPlaylistIdAsync(Guid playlistId)
    {
        var existing = Playlists.FirstOrDefault(p => p.Id == playlistId);
        if (existing is not null)
        {
            Dispatch(() => Title = existing.Name);
            return;
        }

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        try
        {
            var items = await _music.ListPlaylistsAsync(serverUrl, token, CancellationToken.None);
            Dispatch(() => Playlists = new ObservableCollection<PlaylistDto>(items));
            var match = items.FirstOrDefault(p => p.Id == playlistId);
            if (match is not null)
                Dispatch(() => Title = match.Name);
        }
        catch
        {
            // Fallback — already showing Tracks
        }
    }

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
            foreach (var band in EqBands)
                band.GainDb = 0f;
        });
    }

    [RelayCommand]
    private async Task ApplyEqPresetAsync(EqPresetDto preset)
    {
        // Apply to physical bands via the service
        _eq.ApplyPreset(preset);

        // Read back the virtual band gains to update the 10 sliders.
        // The preset's frequency→gain mapping is applied to physical bands,
        // and we read back through the virtual→physical mapping to show
        // what each virtual band ended up with.
        var virtualGains = _eq.GetVirtualBandGainsDb();
        Dispatch(() =>
        {
            for (int i = 0; i < virtualGains.Length && i < EqBands.Count; i++)
                EqBands[i].GainDb = virtualGains[i];
        });
    }

    // ── Save EQ Preset ───────────────────────────────────────────────

    /// <summary>Opens the save preset dialog.</summary>
    [RelayCommand]
    private void OpenSavePresetDialog()
    {
        _selectedPresetId = null;
        NewPresetName = string.Empty;
        ShowSavePresetDialog = true;
    }

    /// <summary>Closes the save preset dialog.</summary>
    [RelayCommand]
    private void CloseSavePresetDialog()
    {
        ShowSavePresetDialog = false;
    }

    /// <summary>Selects an existing preset to overwrite when saving.</summary>
    [RelayCommand]
    private void SelectEqPresetForOverwrite(EqPresetDto preset)
    {
        _selectedPresetId = preset.Id;
        NewPresetName = preset.Name;
    }

    /// <summary>Saves the current EQ band settings as a preset (new or overwrite).</summary>
    [RelayCommand]
    private async Task SaveEqPresetAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPresetName))
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return;

        try
        {
            // Build the bands dictionary in server format (10 standard frequencies)
            // directly from the virtual EQ sliders — they already use the same
            // 10-band format as Blazor/server presets.
            var bands = new Dictionary<string, double>();
            var virtualFreqs = _eq.GetVirtualBandFrequenciesHz();

            for (int i = 0; i < virtualFreqs.Length && i < EqBands.Count; i++)
            {
                var label = FormatFrequencyLabel(virtualFreqs[i]);
                bands[label] = EqBands[i].GainDb;
            }

            var dto = new SaveEqPresetDto
            {
                Name = NewPresetName.Trim(),
                Bands = bands
            };

            EqPresetDto result;
            if (_selectedPresetId.HasValue)
            {
                result = await _music.UpdateEqPresetAsync(serverUrl, token, _selectedPresetId.Value, dto, CancellationToken.None);
                // Update in local cache
                var idx = -1;
                Dispatch(() =>
                {
                    var existing = EqPresets.FirstOrDefault(p => p.Id == _selectedPresetId.Value);
                    if (existing is not null)
                    {
                        idx = EqPresets.IndexOf(existing);
                        EqPresets[idx] = result;
                    }
                    else
                    {
                        EqPresets.Add(result);
                    }
                });
            }
            else
            {
                result = await _music.CreateEqPresetAsync(serverUrl, token, dto, CancellationToken.None);
                Dispatch(() => EqPresets.Add(result));
            }

            Dispatch(() =>
            {
                ShowSavePresetDialog = false;
                NewPresetName = string.Empty;
                _selectedPresetId = null;
            });
        }
        catch (Exception ex)
        {
            Dispatch(() => ErrorMessage = $"Failed to save EQ preset: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats a frequency in Hz to a preset dictionary key (e.g. 1000 → "1K", 125 → "125").
    /// </summary>
    private static string FormatFrequencyLabel(int hz)
    {
        if (hz >= 1000)
        {
            var khz = hz / 1000.0;
            return khz % 1 == 0 ? $"{khz:F0}K" : $"{khz:F1}K";
        }
        return hz.ToString();
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        // If search is open, close it first — don't navigate back
        if (IsSearchOpen)
        {
            CloseSearch();
            return;
        }

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
            // From EQ back to the previous non-EQ view, falling back to Artists.
            NavigateToView(_previousNonEqView);
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

    /// <summary>Navigates to the specified non-EQ view, falling back to Artists for unknown values.</summary>
    private void NavigateToView(MusicView view)
    {
        switch (view)
        {
            case MusicView.Albums:
                if (Albums.Count == 0)
                    _ = LoadAlbumsCommand.ExecuteAsync(null);
                else
                    SwitchToAlbumsView();
                break;
            case MusicView.Tracks:
                if (Tracks.Count == 0)
                    _ = LoadTracksCommand.ExecuteAsync(null);
                else
                {
                    Dispatch(() =>
                    {
                        _tracksFilteredByAlbumId = null;
                        _tracksFilteredByPlaylistId = null;
                        CurrentView = MusicView.Tracks;
                        Title = "Tracks";
                        CanGoBackToArtist = false;
                        CanGoBackToAlbum = false;
                        CanGoBackToPlaylist = false;
                    });
                }
                break;
            case MusicView.Playlists:
                if (Playlists.Count == 0)
                    _ = LoadPlaylistsCommand.ExecuteAsync(null);
                else
                {
                    Dispatch(() =>
                    {
                        CurrentView = MusicView.Playlists;
                        Title = "Playlists";
                        CanGoBackToArtist = false;
                        CanGoBackToAlbum = false;
                        CanGoBackToPlaylist = false;
                    });
                }
                break;
            default:
                // Artists (fallback)
                if (Artists.Count == 0)
                    _ = LoadArtistsCommand.ExecuteAsync(null);
                else
                    SwitchToArtistsView();
                break;
        }
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

    // ── Equalizer ──────────────────────────────────────────────────

    /// <summary>
    /// Initializes the <see cref="EqBands"/> collection with 10 virtual server-standard
    /// bands. Each virtual band maps to the closest physical device band when gain is
    /// applied via <see cref="IEqualizerService.SetVirtualBandGain"/>.
    /// Skips recreation if the band count hasn't changed.
    /// </summary>
    private void InitEqFromDevice()
    {
        EqAvailable = _eq.IsAvailable;
        PhysicalBandCount = _eq.NumberOfBands;
        NumberOfBands = _eq.VirtualBandCount;

        System.Diagnostics.Debug.WriteLine(
            $"[EQ] InitEqFromDevice: IsAvailable={_eq.IsAvailable}, PhysicalBands={PhysicalBandCount}, VirtualBands={NumberOfBands}");

        if (!_eq.IsAvailable || _eq.VirtualBandCount == 0)
        {
            System.Diagnostics.Debug.WriteLine("[EQ] InitEqFromDevice: EQ not available, clearing");
            EqBands.Clear();
            return;
        }

        // Skip recreation if the virtual band count is unchanged
        if (EqBands.Count == _eq.VirtualBandCount)
        {
            System.Diagnostics.Debug.WriteLine("[EQ] InitEqFromDevice: band count unchanged, skipping recreation");
            return;
        }

        var virtualFreqsHz = _eq.GetVirtualBandFrequenciesHz();
        var virtualGainsDb = _eq.GetVirtualBandGainsDb();

        System.Diagnostics.Debug.WriteLine(
            $"[EQ] InitEqFromDevice: virtualFreqsHz.Length={virtualFreqsHz.Length}, virtualGainsDb.Length={virtualGainsDb.Length}");

        var bands = new EqBandModel[virtualFreqsHz.Length];
        for (int i = 0; i < virtualFreqsHz.Length; i++)
        {
            var gainDb = i < virtualGainsDb.Length ? virtualGainsDb[i] : 0f;
            bands[i] = new EqBandModel(i, virtualFreqsHz[i], gainDb);
            System.Diagnostics.Debug.WriteLine($"[EQ]   Band {i}: freq={virtualFreqsHz[i]}Hz, gain={gainDb:F1}dB");
        }

        EqBands = new ObservableCollection<EqBandModel>(bands);
        System.Diagnostics.Debug.WriteLine($"[EQ] InitEqFromDevice: EqBands set with {EqBands.Count} virtual bands");
    }

    /// <summary>
    /// Called when the user drags an EQ slider. Applies the gain change to the native EQ
    /// via the virtual band mapping (virtual band → closest physical band).
    /// </summary>
    public void OnEqBandChanged(int bandIndex, float gainDb)
    {
        _eq.SetVirtualBandGain(bandIndex, gainDb);
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
    private async Task ScrollToCharacter(string character)
    {
        if (string.IsNullOrEmpty(character))
            return;

        var c = char.ToUpperInvariant(character[0]);

        // Try exact match in already-loaded items first
        var target = FindExactMatch(c);
        if (target is not null)
        {
            ScrollToRequested?.Invoke(target, CurrentView);
            return;
        }

        // If in a scoped/filtered view, all data is already loaded — can't load more
        if (IsScopedView)
            return;

        // Load more pages until we find an item starting with this character
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            target = await LoadUntilCharacterFoundAsync(c);
            if (target is not null)
                ScrollToRequested?.Invoke(target, CurrentView);
        }
        finally
        {
            Dispatch(() => IsLoading = false);
        }
    }

    /// <summary>True when the current view is scoped to a parent item (all data already in memory).</summary>
    private bool IsScopedView => CurrentView switch
    {
        MusicView.Albums => _albumsFilteredByArtistId is not null,
        MusicView.Tracks => _tracksFilteredByAlbumId is not null || _tracksFilteredByPlaylistId is not null,
        _ => false
    };

    /// <summary>Finds the first loaded item whose name starts with the given character (exact match).</summary>
    private object? FindExactMatch(char c)
    {
        if (CurrentView == MusicView.Artists)
        {
            return Artists.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Name) && char.ToUpperInvariant(a.Name[0]) == c);
        }
        if (CurrentView == MusicView.Albums)
        {
            return Albums.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Title) && char.ToUpperInvariant(a.Title[0]) == c);
        }
        if (CurrentView == MusicView.Tracks)
        {
            return Tracks.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.Title) && char.ToUpperInvariant(t.Title[0]) == c);
        }
        return null;
    }

    /// <summary>
    /// Loads pages from the server until an item starting with <paramref name="c"/> is found
    /// or there is no more data. Returns the first matching item, or null.
    /// </summary>
    private async Task<object?> LoadUntilCharacterFoundAsync(char c)
    {
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || token is null)
            return null;

        const int maxPages = 200; // safety limit

        for (var page = 0; page < maxPages; page++)
        {
            // Re-check for exact match — new items may have been appended
            var existing = FindExactMatch(c);
            if (existing is not null)
                return existing;

            if (CurrentView == MusicView.Artists)
            {
                if (!_hasMoreArtists)
                    break;

                _artistsLoadCts?.Cancel();
                _artistsLoadCts = new CancellationTokenSource();
                var ct = _artistsLoadCts.Token;

                var nextSkip = _artistsSkip + PageSize;
                var items = await _music.ListArtistsAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
                if (ct.IsCancellationRequested)
                    return null;

                _artistsSkip = nextSkip;
                if (items.Count < PageSize)
                    _hasMoreArtists = false;

                Dispatch(() =>
                {
                    foreach (var item in items)
                        Artists.Add(item);
                });
            }
            else if (CurrentView == MusicView.Albums)
            {
                if (!_hasMoreAlbums)
                    break;

                _albumsLoadCts?.Cancel();
                _albumsLoadCts = new CancellationTokenSource();
                var ct = _albumsLoadCts.Token;

                var nextSkip = _albumsSkip + PageSize;
                var items = await _music.ListAlbumsAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
                if (ct.IsCancellationRequested)
                    return null;

                _albumsSkip = nextSkip;
                if (items.Count < PageSize)
                    _hasMoreAlbums = false;

                Dispatch(() =>
                {
                    foreach (var item in items)
                        Albums.Add(item);
                });
            }
            else if (CurrentView == MusicView.Tracks)
            {
                if (!_hasMoreTracks)
                    break;

                _tracksLoadCts?.Cancel();
                _tracksLoadCts = new CancellationTokenSource();
                var ct = _tracksLoadCts.Token;

                var nextSkip = _tracksSkip + PageSize;
                var items = await _music.ListTracksAsync(serverUrl, token, skip: nextSkip, take: PageSize, ct: ct);
                if (ct.IsCancellationRequested)
                    return null;

                _tracksSkip = nextSkip;
                if (items.Count < PageSize)
                    _hasMoreTracks = false;

                Dispatch(() =>
                {
                    foreach (var item in items)
                        Tracks.Add(item);
                });
            }
            else
            {
                break;
            }

            // Check if the newly loaded page contains a match
            var match = FindExactMatch(c);
            if (match is not null)
                return match;
        }

        return null;
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
