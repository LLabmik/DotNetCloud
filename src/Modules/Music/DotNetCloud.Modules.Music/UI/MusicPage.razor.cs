using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Music.UI;

// MusicPlaybackState is injected in the .razor file as 'Playback'

/// <summary>
/// Code-behind for the Music module Blazor page.
/// </summary>
public partial class MusicPage : IAsyncDisposable
{
    // ── Section / State ──
    private enum Section { Library, Artists, Albums, Genres, Playlists, Favorites, RecentlyPlayed, Settings }

    private Section _section = Section.Library;
    private bool _loading;
    private string? _errorMessage;
    private string _searchQuery = string.Empty;

    // ── Data collections ──
    private List<ArtistDto> _artists = [];
    private int _artistPage = 0;
    private int _artistPageSize = 50;
    private int _totalArtists;
    private bool _hasMoreArtists;
    private List<MusicAlbumDto> _albums = [];
    private int _albumPage = 0;
    private int _albumPageSize = 50;
    private int _totalAlbums;
    private bool _hasMoreAlbums;
    private List<MusicAlbumDto> _recentAlbums = [];
    private List<TrackDto> _newTracks = [];
    private List<TrackDto> _recommendations = [];
    private List<TrackDto> _tracks = [];
    private List<TrackDto> _albumTracks = [];
    private List<TrackDto> _starredTracks = [];
    private List<MusicAlbumDto> _starredAlbums = [];
    private List<TrackDto> _recentlyPlayed = [];
    private List<TrackDto> _genreTracks = [];
    private List<TrackDto>? _searchResults;
    private List<PlaylistDto> _playlists = [];
    private List<TrackDto> _playlistTracks = [];
    private List<string> _genres = [];

    // ── Selection state ──
    private ArtistDto? _selectedArtist;
    private MusicAlbumDto? _selectedAlbum;
    private PlaylistDto? _selectedPlaylist;
    private Guid? _selectedPlaylistId;
    private string? _selectedGenre;
    private List<MusicAlbumDto> _artistAlbums = [];

    // ── UI panels ──
    private bool _showCreatePlaylistDialog;
    private bool _showEditPlaylistDialog;
    private Guid? _trackMenuId;

    // ── Playlist form ──
    private string _playlistName = string.Empty;
    private string _playlistDescription = string.Empty;
    private bool _playlistPublic;

    // ── Visualizer ──
    private bool _visualizerEnabled;
    private bool _showVisualizer;
    private bool _visualizerStarted;
    private string[] _visualizerPresets = [];
    private string? _selectedVisualizerPreset;
    private bool _autoCyclePresets = true;
    private int _autoCycleInterval = 30;
    private double _visualizerBlendDuration = 2.0;
    private bool _allPresetsLoaded;
    private bool _loadingAllPresets;
    private bool _visualizerSupported = true; // assume yes until checked

    // ── Breadcrumbs ──
    private record BreadcrumbItem(string Label, Action Action);
    private List<BreadcrumbItem> _breadcrumb = [];

    // ── Auth ──
    private CallerContext? _caller;

    // ── Deep-link parameters (from search) ──
    [Parameter] public string? FileId { get; set; }
    [Parameter] public string? FileIdNav { get; set; }
    [Parameter] public string? ScrollToPlaying { get; set; }
    private string? _lastHandledNav;
    private bool _pendingScrollToPlaying;

    // ── Library Settings ──
    private List<MediaLibrarySource> _librarySources = [];
    private bool _settingsSaving;
    private bool _settingsScanning;
    private string? _settingsError;
    private string? _settingsSuccess;
    private MediaScanResult? _scanResult;
    private bool _showResetConfirm;
    private bool _settingsResetting;

    // Directory Browser
    private bool _showDirBrowser;
    private Guid? _dirBrowserFolderId;
    private List<(Guid Id, string Name)> _dirBrowserFolders = [];
    private List<(Guid Id, string Name)> _dirBrowserBreadcrumbs = [];
    private string? _dirBrowserError;

    // Enrichment state
    private bool _enrichingAlbum;
    private Guid? _enrichingAlbumId;
    private bool _enrichingArtist;
    private Guid? _enrichingArtistId;
    private ArtistBioDto? _artistBio;
    private string? _enrichmentToast;

    // Settings: enrichment toggles
    private bool _autoFetchMetadata = true;
    private bool _autoFetchAlbumArt = true;

    // Sidebar collapse state
    private bool _sidebarCollapsed;

    private async Task ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        await SaveSidebarCollapsedStateAsync();
        StateHasChanged();
    }

    private async Task SaveSidebarCollapsedStateAsync()
    {
        try
        {
            await Js.InvokeAsync<object?>("localStorage.setItem", new object?[] { "dotnetcloud.sidebar:music", _sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch { /* localStorage unavailable */ }
    }

    // ────────────────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var collapsed = await Js.InvokeAsync<string>("localStorage.getItem", new object?[] { "dotnetcloud.sidebar:music" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
            {
                _sidebarCollapsed = parsed;
            }
        }
        catch { /* localStorage unavailable */ }

        Playback.OnChange += OnPlaybackStateChanged;
        ScanProgress.OnProgressChanged += OnScanProgressChanged;

        try
        {
            _loading = true;
            var caller = await GetCallerAsync();
            _caller = caller;
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
            await LoadLibraryPathAsync();
            await LoadEnrichmentSettingsAsync();
            await LoadCurrentSectionAsync();

            // Deep-link: auto-play from search if fileId parameter was supplied on first load
            if (!string.IsNullOrEmpty(FileId) && Guid.TryParse(FileId, out var fileId))
            {
                _lastHandledNav = FileIdNav;
                await TryAutoPlayFileAsync(fileId, caller);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Music page");
            _errorMessage = "Failed to load music library.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // Handle fileId changes when already on the page (same-page navigation via search).
        // FileIdNav is a timestamp nonce that changes on every click, even for the same file.
        if (!string.IsNullOrEmpty(FileId) && FileIdNav != _lastHandledNav && Guid.TryParse(FileId, out var fileId))
        {
            _lastHandledNav = FileIdNav;
            var caller = _caller ?? await GetCallerAsync();
            await TryAutoPlayFileAsync(fileId, caller);
        }

        // Handle "scroll to playing track" when navigating from global playbar
        if (string.Equals(ScrollToPlaying, "true", StringComparison.OrdinalIgnoreCase) && Playback.NowPlaying is not null)
        {
            ScrollToPlaying = null; // consume once
            var nowPlaying = Playback.NowPlaying;

            // Navigate to the album containing the playing track
            if (nowPlaying.AlbumId.HasValue)
            {
                NavigateToAlbumAsync(nowPlaying.AlbumId);
            }

            _pendingScrollToPlaying = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Auto-play needs the global player's JS to be ready; trigger play if a file was queued during init
        if (_pendingAutoPlayTrack is not null)
        {
            var track = _pendingAutoPlayTrack;
            _pendingAutoPlayTrack = null;
            await PlayTrackAsync(track);
            StateHasChanged();
        }

        // Scroll the currently-playing track row into view
        if (_pendingScrollToPlaying)
        {
            _pendingScrollToPlaying = false;
            try
            {
                await Js.InvokeVoidAsync("eval",
                    "document.querySelector('.track-row.playing')?.scrollIntoView({behavior:'smooth',block:'center'})");
            }
            catch (JSDisconnectedException) { }
        }
    }

    private TrackDto? _pendingAutoPlayTrack;

    /// <summary>
    /// Looks up a track by its Files-module FileNodeId and queues it for auto-play.
    /// </summary>
    private async Task TryAutoPlayFileAsync(Guid fileNodeId, CallerContext caller)
    {
        try
        {
            var track = await TrackService.GetTrackByFileNodeIdAsync(fileNodeId, caller);
            if (track is not null)
            {
                _pendingAutoPlayTrack = track;
            }
            else
            {
                Logger.LogWarning("No music track found for FileNodeId {FileNodeId}", fileNodeId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to auto-play track for FileNodeId {FileNodeId}", fileNodeId);
        }
    }

    private void OnPlaybackStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnScanProgressChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private bool IsScanActive => _caller is not null && ScanProgress.IsScanning(_caller.UserId);

    private LibraryScanProgress? CurrentScanProgress => _caller is null ? null : ScanProgress.GetCurrentProgress(_caller.UserId);

    // ────────────────────────────────────────────────────────
    //  Section navigation
    // ────────────────────────────────────────────────────────

    private async Task SwitchSection(Section section)
    {
        _section = section;
        _selectedArtist = null;
        _selectedAlbum = null;
        _selectedPlaylist = null;
        _selectedPlaylistId = null;
        _selectedGenre = null;
        _searchResults = null;
        _breadcrumb.Clear();
        await LoadCurrentSectionAsync();
    }

    private async Task LoadCurrentSectionAsync()
    {
        try
        {
            _loading = true;
            _errorMessage = null;
            StateHasChanged();

            var caller = await GetCallerAsync();

            switch (_section)
            {
                case Section.Library:
                    _recentAlbums = (await AlbumService.ListAlbumsAsync(caller, 0, 8)).ToList();
                    _newTracks = (await TrackService.ListTracksAsync(caller, 0, 10)).ToList();
                    _recommendations = (await RecommendationService.GetRecentlyPlayedAsync(caller, 10)).ToList();
                    break;

                case Section.Artists:
                    _artistPage = 0;
                    await LoadArtistsAsync();
                    break;

                case Section.Albums:
                    _albumPage = 0;
                    await LoadAlbumsAsync();
                    break;

                case Section.Genres:
                    _genres = (await RecommendationService.GetGenresAsync(caller.UserId)).ToList();
                    break;

                case Section.Playlists:
                    _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
                    break;

                case Section.Favorites:
                    _starredTracks = (await TrackService.GetStarredTracksAsync(caller)).ToList();
                    _starredAlbums = (await AlbumService.GetStarredAlbumsAsync(caller)).ToList();
                    break;

                case Section.RecentlyPlayed:
                    _recentlyPlayed = (await RecommendationService.GetRecentlyPlayedAsync(caller, 50)).ToList();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading section {Section}", _section);
            _errorMessage = $"Failed to load {_section}.";
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    // ────────────────────────────────────────────────────────
    //  Artist drill-down
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Lists artists for the current page.
    /// </summary>
    private async Task LoadArtistsAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            var artists = (await ArtistService.ListArtistsAsync(caller, _artistPage * _artistPageSize, _artistPageSize)).ToList();
            _totalArtists = await ArtistService.GetCountAsync(caller.UserId);
            _artists = artists;
            _hasMoreArtists = (_artistPage + 1) * _artistPageSize < _totalArtists;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading artists");
        }
    }

    private async Task PrevArtistPageAsync()
    {
        if (_artistPage > 0)
        {
            _artistPage--;
            await LoadArtistsAsync();
        }
    }

    private async Task NextArtistPageAsync()
    {
        if (!_hasMoreArtists)
        {
            return;
        }

        try
        {
            _artistPage++;
            await LoadArtistsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching next page of artists");
        }
    }

    // ────────────────────────────────────────────────────────
    //  Album pagination
    // ────────────────────────────────────────────────────────

    private async Task LoadAlbumsAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            var albums = (await AlbumService.ListAlbumsAsync(caller, _albumPage * _albumPageSize, _albumPageSize + 1)).ToList();
            _hasMoreAlbums = albums.Count > _albumPageSize;
            if (_hasMoreAlbums)
            {
                albums.RemoveAt(albums.Count - 1);
            }
            _albums = albums;
            _totalAlbums = _albumPage * _albumPageSize + albums.Count + (_hasMoreAlbums ? 1 : 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading albums");
        }
    }

    private async Task PrevAlbumPageAsync()
    {
        if (_albumPage > 0)
        {
            _albumPage--;
            await LoadAlbumsAsync();
        }
    }

    private async Task NextAlbumPageAsync()
    {
        if (!_hasMoreAlbums)
        {
            return;
        }

        try
        {
            _albumPage++;
            await LoadAlbumsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching next page of albums");
        }
    }

    private async void OpenArtistDetail(ArtistDto artist)
    {
        _selectedArtist = artist;
        _artistBio = null;
        _enrichmentToast = null;
        _breadcrumb =
        [
            new BreadcrumbItem("Artists", () => { _selectedArtist = null; _artistBio = null; _breadcrumb.Clear(); StateHasChanged(); })
        ];

        try
        {
            var caller = await GetCallerAsync();
            _artistAlbums = (await AlbumService.ListAlbumsByArtistAsync(artist.Id, caller)).ToList();
            _artistBio = await ArtistService.GetArtistBioAsync(artist.Id, caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading artist detail");
        }
        StateHasChanged();
    }

    // ────────────────────────────────────────────────────────
    //  Album drill-down
    // ────────────────────────────────────────────────────────

    private async void OpenAlbumDetail(MusicAlbumDto album)
    {
        var sourceSection = _section;
        _selectedAlbum = album;
        _section = Section.Albums;

        // Build breadcrumbs that depend on how we got here
        if (_selectedArtist is not null)
        {
            var capturedArtist = _selectedArtist;
            _breadcrumb =
            [
                new BreadcrumbItem("Artists", () => { _section = Section.Artists; _selectedArtist = null; _selectedAlbum = null; _breadcrumb.Clear(); StateHasChanged(); }),
                new BreadcrumbItem(capturedArtist.Name, () => { _section = Section.Artists; _selectedAlbum = null; _breadcrumb.RemoveAt(_breadcrumb.Count - 1); StateHasChanged(); })
            ];
        }
        else if (sourceSection == Section.Library)
        {
            _breadcrumb =
            [
                new BreadcrumbItem("Library", () => { _section = Section.Library; _selectedAlbum = null; _breadcrumb.Clear(); StateHasChanged(); })
            ];
        }
        else
        {
            _breadcrumb =
            [
                new BreadcrumbItem("Albums", () => { _selectedAlbum = null; _breadcrumb.Clear(); StateHasChanged(); })
            ];
        }

        try
        {
            var caller = await GetCallerAsync();
            _albumTracks = (await TrackService.ListTracksByAlbumAsync(album.Id, caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading album tracks");
        }
        StateHasChanged();
    }

    private async void NavigateToAlbumAsync(Guid? albumId)
    {
        if (albumId is null) return;
        try
        {
            var caller = await GetCallerAsync();
            var album = await AlbumService.GetAlbumAsync(albumId.Value, caller);
            if (album is not null)
            {
                OpenAlbumDetail(album);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error navigating to album {AlbumId}", albumId);
        }
    }

    // ────────────────────────────────────────────────────────
    //  Genre selection
    // ────────────────────────────────────────────────────────

    private async Task SelectGenreAsync(string genre)
    {
        _selectedGenre = genre;
        try
        {
            var caller = await GetCallerAsync();
            _genreTracks = (await TrackService.GetRandomTracksAsync(caller, 100, genre)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading genre tracks for {Genre}", genre);
        }
    }

    // ────────────────────────────────────────────────────────
    //  Playlist
    // ────────────────────────────────────────────────────────

    private async Task SelectPlaylistAsync(Guid playlistId)
    {
        _section = Section.Playlists;
        _selectedPlaylistId = playlistId;
        _selectedPlaylist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        try
        {
            var caller = await GetCallerAsync();
            _playlistTracks = (await PlaylistService.GetPlaylistTracksAsync(playlistId, caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading playlist");
        }
        StateHasChanged();
    }

    private void BeginCreatePlaylist()
    {
        _playlistName = string.Empty;
        _playlistDescription = string.Empty;
        _playlistPublic = false;
        _showCreatePlaylistDialog = true;
    }

    private void ClosePlaylistDialog()
    {
        _showCreatePlaylistDialog = false;
        _showEditPlaylistDialog = false;
    }

    private async Task SavePlaylistAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            if (_showEditPlaylistDialog && _selectedPlaylist is not null)
            {
                await PlaylistService.UpdatePlaylistAsync(_selectedPlaylist.Id, new UpdatePlaylistDto { Name = _playlistName, Description = _playlistDescription, IsPublic = _playlistPublic }, caller);
            }
            else
            {
                await PlaylistService.CreatePlaylistAsync(new CreatePlaylistDto { Name = _playlistName, Description = _playlistDescription, IsPublic = _playlistPublic }, caller);
            }
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
            ClosePlaylistDialog();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving playlist");
        }
    }

    private async Task DeletePlaylistAsync(Guid playlistId)
    {
        try
        {
            var caller = await GetCallerAsync();
            await PlaylistService.DeletePlaylistAsync(playlistId, caller);
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
            _selectedPlaylist = null;
            _selectedPlaylistId = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting playlist");
        }
    }

    private async Task AddToPlaylistAsync(Guid playlistId, Guid trackId)
    {
        try
        {
            var caller = await GetCallerAsync();
            await PlaylistService.AddTrackAsync(playlistId, trackId, caller);
            _trackMenuId = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding track to playlist");
        }
    }

    private async Task RemoveFromPlaylistAsync(Guid playlistId, Guid trackId)
    {
        try
        {
            var caller = await GetCallerAsync();
            await PlaylistService.RemoveTrackAsync(playlistId, trackId, caller);
            _playlistTracks.RemoveAll(t => t.Id == trackId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing track from playlist");
        }
    }

    // ────────────────────────────────────────────────────────
    //  Playback
    // ────────────────────────────────────────────────────────

    private async Task PlayTrackAsync(TrackDto track)
    {
        Playback.PlayTrack(track);

        // Start visualizer render loop if enabled
        if (_visualizerEnabled && !_visualizerStarted)
        {
            _visualizerStarted = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.start");
            if (_visualizerStarted && _autoCyclePresets)
            {
                await Js.InvokeVoidAsync("dotnetcloudVisualizer.startAutoCycle", _autoCycleInterval, _visualizerBlendDuration);
            }
        }
    }

    private async Task PlayAlbumAsync(Guid albumId)
    {
        try
        {
            var caller = await GetCallerAsync();
            var tracks = (await TrackService.ListTracksByAlbumAsync(albumId, caller)).ToList();
            if (tracks.Count > 0)
            {
                Playback.PlayQueue(tracks);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error playing album");
        }
    }

    private async Task PlayPlaylistAsync(Guid playlistId)
    {
        try
        {
            var caller = await GetCallerAsync();
            var tracks = (await PlaylistService.GetPlaylistTracksAsync(playlistId, caller)).ToList();
            if (tracks.Count > 0)
            {
                Playback.PlayQueue(tracks);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error playing playlist");
        }
    }

    private void AddToQueue(TrackDto track)
    {
        Playback.AddToQueue(track);
        _trackMenuId = null;
    }

    // ────────────────────────────────────────────────────────
    //  Starring
    // ────────────────────────────────────────────────────────

    private async Task ToggleStarTrackAsync(TrackDto track)
    {
        try
        {
            var caller = await GetCallerAsync();
            await PlaybackService.ToggleStarAsync(track.Id, StarredItemType.Track, caller);
            var updated = track with { IsStarred = !track.IsStarred };
            ReplaceInList(_tracks, updated);
            ReplaceInList(_albumTracks, updated);
            ReplaceInList(_starredTracks, updated);
            ReplaceInList(_genreTracks, updated);
            ReplaceInList(_playlistTracks, updated);
            if (Playback.NowPlaying?.Id == track.Id)
                Playback.UpdateNowPlaying(updated);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling star");
        }
    }

    private async Task ToggleStarAlbumAsync(MusicAlbumDto album)
    {
        try
        {
            var caller = await GetCallerAsync();
            await PlaybackService.ToggleStarAsync(album.Id, StarredItemType.Album, caller);
            var updated = album with { IsStarred = !album.IsStarred };
            if (_selectedAlbum?.Id == album.Id)
                _selectedAlbum = updated;
            ReplaceInList(_albums, updated);
            ReplaceInList(_recentAlbums, updated);
            ReplaceInList(_artistAlbums, updated);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling album star");
        }
    }

    // ────────────────────────────────────────────────────────
    //  Search
    // ────────────────────────────────────────────────────────

    private async Task HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_searchQuery))
        {
            try
            {
                var caller = await GetCallerAsync();
                _searchResults = (await TrackService.SearchAsync(caller, _searchQuery, 50)).ToList();
                _breadcrumb.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Search error");
            }
        }
        else if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults = null;
        }
    }

    private void ClearSearch()
    {
        _searchQuery = string.Empty;
        _searchResults = null;
        _breadcrumb.Clear();
    }

    // ────────────────────────────────────────────────────────
    //  Track context menu
    // ────────────────────────────────────────────────────────

    private void ShowTrackMenu(TrackDto track)
    {
        _trackMenuId = _trackMenuId == track.Id ? null : track.Id;
    }

    // ────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────

    private string GetSectionTitle() => _searchResults is not null
        ? $"Search: {_searchQuery}"
        : _section switch
        {
            Section.Library => "Library",
            Section.Artists when _selectedArtist is not null => _selectedArtist.Name,
            Section.Artists => "Artists",
            Section.Albums when _selectedAlbum is not null => _selectedAlbum.Title,
            Section.Albums => "Albums",
            Section.Genres when _selectedGenre is not null => _selectedGenre,
            Section.Genres => "Genres",
            Section.Playlists when _selectedPlaylist is not null => _selectedPlaylist.Name,
            Section.Playlists => "Playlists",
            Section.Favorites => "Favorites",
            Section.RecentlyPlayed => "Recently Played",
            _ => "Music"
        };

    private static string FormatDuration(TimeSpan duration) => MusicPlaybackState.FormatDuration(duration);

    private static string GetAlbumArtUrl(Guid albumId) => MusicPlaybackState.GetAlbumArtUrl(albumId);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }

    private async Task<CallerContext> GetCallerAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? throw new UnauthorizedAccessException("Not authenticated.");
        var roles = authState.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(Guid.Parse(userId), roles, CallerType.User);
    }

    // ── Library Settings Methods ─────────────────────────────

    private async Task LoadLibraryPathAsync()
    {
        if (_caller is null) return;
        try
        {
            _librarySources = (await MediaLibrarySourceSettings.LoadSourcesAsync(UserSettingsService, _caller.UserId, "music")).ToList();
        }
        catch { /* ignore load failures */ }
    }

    private Task SaveLibraryPathAsync()
        => PersistLibrarySourcesAsync(showSuccessMessage: true);

    private async Task PersistLibrarySourcesAsync(bool showSuccessMessage)
    {
        if (_caller is null) return;
        _settingsSaving = true;
        _settingsError = null;
        if (showSuccessMessage)
        {
            _settingsSuccess = null;
        }

        try
        {
            _librarySources = MediaLibrarySourceSettings.Normalize(_librarySources).ToList();
            await MediaLibrarySourceSettings.SaveSourcesAsync(
                UserSettingsService,
                _caller.UserId,
                "music",
                _librarySources,
                "Music library scan sources");

            if (showSuccessMessage)
            {
                _settingsSuccess = "Sources saved.";
            }
        }
        catch (Exception ex)
        {
            _settingsError = $"Save failed: {ex.Message}";
        }
        finally
        {
            _settingsSaving = false;
        }
    }

    private async Task ScanLibraryAsync()
    {
        if (_caller is null || _librarySources.Count == 0) return;
        await PersistLibrarySourcesAsync(showSuccessMessage: false);
        if (_settingsError is not null) return;

        _settingsScanning = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        var scanStartedAt = DateTimeOffset.UtcNow;
        var scanCts = ScanProgress.StartScan(_caller.UserId);
        StateHasChanged();

        var scanStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Bridge MediaScanProgress → ScanProgressState (LibraryScanProgress)
            var progressBridge = new Progress<MediaScanProgress>(msp =>
            {
                ScanProgress.UpdateProgress(_caller.UserId, new LibraryScanProgress
                {
                    Phase = msp.Phase,
                    CurrentFile = msp.CurrentFile,
                    FilesProcessed = msp.FilesProcessed,
                    TotalFiles = msp.TotalFiles,
                    TracksAdded = msp.Imported,
                    TracksUpdated = 0,
                    TracksSkipped = 0,
                    TracksFailed = msp.Failed,
                    TracksRemoved = msp.Removed,
                    AlbumArtFetched = 0,
                    AlbumArtRemaining = 0,
                    PercentComplete = msp.PercentComplete,
                    ElapsedTime = scanStopwatch.Elapsed,
                });
            });

            _scanResult = await MediaLibraryScanner.ScanSourcesAsync(
                _librarySources, _caller.UserId, "Music", progressBridge, scanCts.Token);

            var successMsg = $"Scan complete: {_scanResult.Imported} imported, {_scanResult.Skipped} already up to date";
            if (_scanResult.Removed > 0)
                successMsg += $", {_scanResult.Removed} removed (files deleted)";

            var queuedBackgroundEnrichment = false;
            if (_autoFetchAlbumArt || _autoFetchMetadata)
            {
                queuedBackgroundEnrichment = await QueuePostScanEnrichmentAsync(scanStartedAt);
            }

            if (!queuedBackgroundEnrichment)
            {
                ScanProgress.CompleteScan(_caller.UserId);
            }

            if (queuedBackgroundEnrichment)
            {
                successMsg += ". MusicBrainz enrichment continues in the background";
            }

            _settingsSuccess = successMsg + ".";

            // Reload library data so navigating to Library shows freshly imported tracks
            var caller = await GetCallerAsync();
            _recentAlbums = (await AlbumService.ListAlbumsAsync(caller, 0, 8)).ToList();
            _newTracks = (await TrackService.ListTracksAsync(caller, 0, 10)).ToList();
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
        }
        catch (OperationCanceledException)
        {
            ScanProgress.CompleteScan(_caller.UserId);
            _settingsSuccess = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ScanProgress.CompleteScan(_caller.UserId);
            _settingsError = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _settingsScanning = false;
        }
    }

    private async Task<bool> QueuePostScanEnrichmentAsync(DateTimeOffset scanStartedAt)
    {
        if (_caller is null || _scanResult is null)
        {
            return false;
        }

        try
        {
            ScanProgress.UpdateProgress(_caller.UserId, new LibraryScanProgress
            {
                Phase = "Queued background enrichment",
                PercentComplete = 100,
                FilesProcessed = _scanResult.TotalFound,
                TotalFiles = _scanResult.TotalFound,
                TracksAdded = _scanResult.Imported,
                TracksUpdated = 0,
                TracksSkipped = _scanResult.Skipped,
                TracksFailed = _scanResult.Failed,
                TracksRemoved = _scanResult.Removed,
                AlbumArtFetched = 0,
                AlbumArtRemaining = 0,
                ElapsedTime = DateTimeOffset.UtcNow - scanStartedAt,
            });

            return await EnrichmentBackgroundQueue.EnqueueAsync(new MusicEnrichmentJob
            {
                OwnerId = _caller.UserId,
                FetchAlbumArt = _autoFetchAlbumArt,
                FetchMetadata = _autoFetchMetadata,
                StartedAtUtc = scanStartedAt,
                TotalFiles = _scanResult.TotalFound,
                TracksAdded = _scanResult.Imported,
                TracksUpdated = 0,
                TracksSkipped = _scanResult.Skipped,
                TracksFailed = _scanResult.Failed,
                TracksRemoved = _scanResult.Removed
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to queue post-scan enrichment, scan results preserved");
            return false;
        }
    }

    private void CancelScan()
    {
        if (_caller is null)
        {
            return;
        }

        ScanProgress.Cancel(_caller.UserId);
    }

    private async Task ResetCollectionAsync()
    {
        _settingsResetting = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        StateHasChanged();
        try
        {
            await MusicIndexingCallback.ResetCollectionAsync();
            _settingsSuccess = "Music collection reset. Click Scan Now to rebuild your library.";
            _showResetConfirm = false;

            // Clear displayed data
            _recentAlbums.Clear();
            _newTracks.Clear();
            _tracks.Clear();
            _albums.Clear();
            _artists.Clear();
            _playlists.Clear();
            _starredTracks.Clear();
            _recentlyPlayed.Clear();
            Playback.Stop();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reset music collection");
            _settingsError = $"Reset failed: {ex.Message}";
        }
        finally
        {
            _settingsResetting = false;
        }
    }

    // ── Directory Browser Methods ────────────────────────────

    private async Task OpenDirectoryBrowser()
    {
        _dirBrowserError = null;
        _dirBrowserFolderId = null;
        _dirBrowserBreadcrumbs.Clear();
        await LoadDirBrowserFoldersAsync();
        _showDirBrowser = true;
    }

    private void HideDirectoryBrowser() => _showDirBrowser = false;

    private async Task DirBrowserNavigateToRoot()
    {
        _dirBrowserFolderId = null;
        _dirBrowserBreadcrumbs.Clear();
        await LoadDirBrowserFoldersAsync();
    }

    private async Task LoadDirBrowserFoldersAsync()
    {
        _dirBrowserError = null;
        _dirBrowserFolders.Clear();
        try
        {
            if (_caller is null) return;
            var nodes = _dirBrowserFolderId.HasValue
                ? await FileService.ListChildrenAsync(_dirBrowserFolderId.Value, _caller)
                : await FileService.ListRootAsync(_caller);

            _dirBrowserFolders = nodes
                .Where(n => n.NodeType == "Folder")
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .Select(n => (n.Id, n.Name))
                .ToList();
        }
        catch (Exception ex)
        {
            _dirBrowserError = ex.Message;
        }
    }

    private async Task DirBrowserNavigate(Guid folderId, string folderName)
    {
        _dirBrowserBreadcrumbs.Add((folderId, folderName));
        _dirBrowserFolderId = folderId;
        await LoadDirBrowserFoldersAsync();
    }

    private async Task DirBrowserGoUp()
    {
        if (_dirBrowserBreadcrumbs.Count > 0)
        {
            _dirBrowserBreadcrumbs.RemoveAt(_dirBrowserBreadcrumbs.Count - 1);
            _dirBrowserFolderId = _dirBrowserBreadcrumbs.Count > 0
                ? _dirBrowserBreadcrumbs[^1].Id
                : null;
            await LoadDirBrowserFoldersAsync();
        }
    }

    private async Task DirBrowserNavigateToCrumb(int index)
    {
        if (index < _dirBrowserBreadcrumbs.Count - 1)
        {
            _dirBrowserBreadcrumbs.RemoveRange(index + 1, _dirBrowserBreadcrumbs.Count - index - 1);
        }
        _dirBrowserFolderId = _dirBrowserBreadcrumbs[index].Id;
        await LoadDirBrowserFoldersAsync();
    }

    private string GetDirBrowserPath()
    {
        if (_dirBrowserBreadcrumbs.Count == 0) return "/";
        return "/" + string.Join('/', _dirBrowserBreadcrumbs.Select(b => b.Name));
    }

    private async Task ConfirmDirectoryBrowserAsync()
    {
        _dirBrowserError = null;

        var source = await CreateLibrarySourceFromBrowserAsync();
        if (source is null)
        {
            return;
        }

        var sourceKey = MediaLibrarySourceSettings.GetSourceKey(source);
        if (_librarySources.Any(existing => string.Equals(MediaLibrarySourceSettings.GetSourceKey(existing), sourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            _dirBrowserError = "This folder is already selected.";
            return;
        }

        _librarySources.Add(source);
        _librarySources = MediaLibrarySourceSettings.Normalize(_librarySources).ToList();
        _settingsError = null;
        _settingsSuccess = "Source added. Save changes or scan now to persist it.";
        _showDirBrowser = false;
    }

    private void RemoveLibrarySource(MediaLibrarySource source)
    {
        var sourceKey = MediaLibrarySourceSettings.GetSourceKey(source);
        _librarySources = _librarySources
            .Where(existing => !string.Equals(MediaLibrarySourceSettings.GetSourceKey(existing), sourceKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _settingsError = null;
        _settingsSuccess = "Source removed. Save changes or scan now to persist it.";
    }

    private async Task<MediaLibrarySource?> CreateLibrarySourceFromBrowserAsync()
    {
        if (_caller is null)
        {
            return null;
        }

        var displayPath = GetDirBrowserPath();
        if (!_dirBrowserFolderId.HasValue)
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = null,
                DisplayPath = displayPath,
                DisplayName = "Home",
                Enabled = true,
            };
        }

        var node = await FileService.GetNodeAsync(_dirBrowserFolderId.Value, _caller);
        if (node is null)
        {
            _dirBrowserError = "The selected folder is no longer available.";
            return null;
        }

        if (!string.Equals(node.NodeType, "Folder", StringComparison.OrdinalIgnoreCase))
        {
            _dirBrowserError = "Select a folder source.";
            return null;
        }

        if (!node.IsVirtual)
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = node.Id,
                DisplayPath = displayPath,
                DisplayName = node.Name,
                Enabled = true,
            };
        }

        if (string.Equals(node.VirtualSourceKind, "AdminSharedFolder", StringComparison.OrdinalIgnoreCase) && node.VirtualSourceId.HasValue)
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.SharedMount,
                SharedFolderId = node.VirtualSourceId.Value,
                RelativePath = node.VirtualRelativePath,
                DisplayPath = displayPath,
                DisplayName = node.Name,
                Enabled = true,
            };
        }

        _dirBrowserError = "Only folders from your library or _DotNetCloud admin shared folders can be added.";
        return null;
    }

    private static string GetLibrarySourceKindLabel(MediaLibrarySource source)
        => source.SourceKind == MediaLibrarySourceKind.SharedMount ? "Shared" : "Owned";

    // ────────────────────────────────────────────────────────
    //  Visualizer
    // ────────────────────────────────────────────────────────

    private async Task ToggleVisualizerAsync()
    {
        _visualizerEnabled = !_visualizerEnabled;

        if (_visualizerEnabled)
        {
            _showVisualizer = true;
            await StartVisualizerAsync();
        }
        else
        {
            _showVisualizer = false;
            await StopVisualizerAsync();
        }
    }

    private async Task HideVisualizerOverlay()
    {
        _showVisualizer = false;
        try
        {
            await Js.InvokeVoidAsync("dotnetcloudVisualizer.exitFullscreen");
        }
        catch (JSDisconnectedException) { }
    }

    private async Task StartVisualizerAsync()
    {
        try
        {
            _visualizerSupported = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.isSupported");
            if (!_visualizerSupported)
            {
                _showVisualizer = false;
                _visualizerEnabled = false;
                return;
            }

            var initOk = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.init", "dnc-visualizer-canvas");
            if (!initOk) return;

            // Load preset names
            _visualizerPresets = await Js.InvokeAsync<string[]>("dotnetcloudVisualizer.getPresetNames");
            _allPresetsLoaded = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.isAllPresetsLoaded");

            if (Playback.IsPlaying)
            {
                _visualizerStarted = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.start");
                if (_visualizerStarted)
                {
                    if (_visualizerPresets.Length > 0)
                    {
                        _selectedVisualizerPreset = await Js.InvokeAsync<string?>("dotnetcloudVisualizer.getCurrentPresetName");
                    }

                    if (_autoCyclePresets)
                    {
                        await Js.InvokeVoidAsync("dotnetcloudVisualizer.startAutoCycle", _autoCycleInterval, _visualizerBlendDuration);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to start visualizer");
            _showVisualizer = false;
            _visualizerEnabled = false;
        }
    }

    private async Task StopVisualizerAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("dotnetcloudVisualizer.stop");
            _visualizerStarted = false;
        }
        catch (JSDisconnectedException) { }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to stop visualizer");
        }
    }

    private async Task ChangeVisualizerPresetAsync(string presetName)
    {
        if (!_visualizerStarted) return;
        await Js.InvokeAsync<bool>("dotnetcloudVisualizer.loadPreset", presetName, _visualizerBlendDuration);
        _selectedVisualizerPreset = presetName;
    }

    private async Task RandomVisualizerPresetAsync()
    {
        if (!_visualizerStarted) return;
        var name = await Js.InvokeAsync<string?>("dotnetcloudVisualizer.randomPreset", _visualizerBlendDuration);
        if (name is not null)
        {
            _selectedVisualizerPreset = name;
        }
    }

    private async Task LoadAllVisualizerPresetsAsync()
    {
        if (_allPresetsLoaded || _loadingAllPresets) return;
        _loadingAllPresets = true;
        StateHasChanged();
        try
        {
            _visualizerPresets = await Js.InvokeAsync<string[]>("dotnetcloudVisualizer.loadAllPresets");
            _allPresetsLoaded = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load all visualizer presets");
        }
        finally
        {
            _loadingAllPresets = false;
            StateHasChanged();
        }
    }

    private async Task ToggleVisualizerFullscreenAsync()
    {
        await Js.InvokeVoidAsync("dotnetcloudVisualizer.toggleFullscreen");
    }

    private async Task ToggleAutoCyclePresetsAsync()
    {
        _autoCyclePresets = !_autoCyclePresets;
        if (_autoCyclePresets)
        {
            await Js.InvokeVoidAsync("dotnetcloudVisualizer.startAutoCycle", _autoCycleInterval, _visualizerBlendDuration);
        }
        else
        {
            await Js.InvokeVoidAsync("dotnetcloudVisualizer.stopAutoCycle");
        }
    }

    public async ValueTask DisposeAsync()
    {
        Playback.OnChange -= OnPlaybackStateChanged;
        ScanProgress.OnProgressChanged -= OnScanProgressChanged;

        try
        {
            await Js.InvokeVoidAsync("dotnetcloudVisualizer.dispose");
        }
        catch (JSDisconnectedException) { }
    }

    // ────────────────────────────────────────────────────────
    //  Enrichment
    // ────────────────────────────────────────────────────────

    private async Task EnrichAlbumAsync(Guid albumId)
    {
        if (_caller is null) return;
        _enrichingAlbum = true;
        _enrichingAlbumId = albumId;
        _enrichmentToast = null;
        StateHasChanged();
        try
        {
            await EnrichmentService.EnrichAlbumAsync(albumId, _caller, force: false);
            // Reload album to get updated cover art status
            var updated = await AlbumService.GetAlbumAsync(albumId, _caller);
            if (updated is not null)
            {
                if (_selectedAlbum?.Id == albumId)
                    _selectedAlbum = updated;
                ReplaceInList(_albums, updated);
                ReplaceInList(_recentAlbums, updated);
                ReplaceInList(_artistAlbums, updated);
                _enrichmentToast = updated.HasCoverArt ? "Cover art fetched from MusicBrainz!" : "No cover art found on MusicBrainz.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error enriching album {AlbumId}", albumId);
            _enrichmentToast = "Failed to fetch cover art.";
        }
        finally
        {
            _enrichingAlbum = false;
            _enrichingAlbumId = null;
            StateHasChanged();
        }
    }

    private async Task EnrichArtistAsync(Guid artistId)
    {
        if (_caller is null) return;
        _enrichingArtist = true;
        _enrichingArtistId = artistId;
        _enrichmentToast = null;
        StateHasChanged();
        try
        {
            await EnrichmentService.EnrichArtistAsync(artistId, _caller, force: false);
            _artistBio = await ArtistService.GetArtistBioAsync(artistId, _caller);
            _enrichmentToast = _artistBio?.Biography is not null ? "Artist info fetched from MusicBrainz!" : "No additional info found on MusicBrainz.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error enriching artist {ArtistId}", artistId);
            _enrichmentToast = "Failed to fetch artist info.";
        }
        finally
        {
            _enrichingArtist = false;
            _enrichingArtistId = null;
            StateHasChanged();
        }
    }

    private void DismissEnrichmentToast()
    {
        _enrichmentToast = null;
    }

    private async Task SaveEnrichmentSettingsAsync()
    {
        if (_caller is null) return;
        try
        {
            await UserSettingsService.UpsertSettingAsync(_caller.UserId, "media-library", "music-auto-fetch-metadata",
                new UpsertUserSettingDto { Value = _autoFetchMetadata.ToString(), Description = "Auto-fetch metadata from MusicBrainz during scan" });
            await UserSettingsService.UpsertSettingAsync(_caller.UserId, "media-library", "music-auto-fetch-art",
                new UpsertUserSettingDto { Value = _autoFetchAlbumArt.ToString(), Description = "Auto-fetch missing album art from Cover Art Archive" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving enrichment settings");
        }
    }

    private async Task LoadEnrichmentSettingsAsync()
    {
        if (_caller is null) return;
        try
        {
            var metadataSetting = await UserSettingsService.GetSettingAsync(_caller.UserId, "media-library", "music-auto-fetch-metadata");
            if (metadataSetting?.Value is not null)
                _autoFetchMetadata = bool.TryParse(metadataSetting.Value, out var v) && v;
            var artSetting = await UserSettingsService.GetSettingAsync(_caller.UserId, "media-library", "music-auto-fetch-art");
            if (artSetting?.Value is not null)
                _autoFetchAlbumArt = bool.TryParse(artSetting.Value, out var v2) && v2;
        }
        catch { /* ignore load failures */ }
    }

    // ────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}"
            : $"{elapsed.Seconds}s";
    }

    private static string TruncateFileName(string path, int maxLength)
    {
        var name = Path.GetFileName(path);
        return name.Length <= maxLength ? name : string.Concat(name.AsSpan(0, maxLength - 3), "...");
    }

    private static void ReplaceInList<T>(List<T> list, T updated) where T : class
    {
        var id = updated switch
        {
            MusicAlbumDto a => a.Id,
            TrackDto t => t.Id,
            ArtistDto ar => ar.Id,
            _ => Guid.Empty
        };
        if (id == Guid.Empty) return;

        var index = list.FindIndex(item => item switch
        {
            MusicAlbumDto a => a.Id == id,
            TrackDto t => t.Id == id,
            ArtistDto ar => ar.Id == id,
            _ => false
        });
        if (index >= 0)
            list[index] = updated;
    }
}
