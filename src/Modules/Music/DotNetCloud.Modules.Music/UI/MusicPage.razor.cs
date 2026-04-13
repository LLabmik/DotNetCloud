using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Music.UI;

/// <summary>
/// Code-behind for the Music module Blazor page.
/// </summary>
public partial class MusicPage : IAsyncDisposable
{
    // ── Section / State ──
    private enum Section { Library, Artists, Albums, Genres, Playlists, Favorites, RecentlyPlayed, Settings }
    private enum RepeatMode { Off, All, One }

    private Section _section = Section.Library;
    private bool _loading;
    private string? _errorMessage;
    private string _searchQuery = string.Empty;

    // ── Data collections ──
    private List<ArtistDto> _artists = [];
    private List<MusicAlbumDto> _albums = [];
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
    private List<EqPresetDto> _eqPresets = [];

    // ── Selection state ──
    private ArtistDto? _selectedArtist;
    private MusicAlbumDto? _selectedAlbum;
    private PlaylistDto? _selectedPlaylist;
    private Guid? _selectedPlaylistId;
    private string? _selectedGenre;
    private List<MusicAlbumDto> _artistAlbums = [];

    // ── Playback state ──
    private TrackDto? _nowPlaying;
    private bool _isPlaying;
    private bool _shuffle;
    private RepeatMode _repeat = RepeatMode.Off;
    private int _volume = 80;
    private bool _muted;
    private TimeSpan _playbackPosition = TimeSpan.Zero;
    private List<TrackDto> _queue = [];
    private int _queueIndex = -1;

    // ── UI panels ──
    private bool _showQueue;
    private bool _showEqualizer;
    private bool _showCreatePlaylistDialog;
    private bool _showEditPlaylistDialog;
    private Guid? _trackMenuId;

    // ── Playlist form ──
    private string _playlistName = string.Empty;
    private string _playlistDescription = string.Empty;
    private bool _playlistPublic;

    // ── Equalizer ──
    private double[] _eqBands = new double[10];
    private Guid? _activePresetId;
    private static readonly string[] _bandLabels = ["31", "63", "125", "250", "500", "1K", "2K", "4K", "8K", "16K"];
    private bool _showSavePresetDialog;
    private string _newPresetName = string.Empty;

    // ── Visualizer ──
    private bool _showVisualizer;
    private bool _visualizerStarted;
    private string[] _visualizerPresets = [];
    private string? _selectedVisualizerPreset;
    private bool _autoCyclePresets;
    private int _autoCycleInterval = 30;
    private double _visualizerBlendDuration = 2.0;
    private bool _allPresetsLoaded;
    private bool _loadingAllPresets;
    private bool _visualizerSupported = true; // assume yes until checked

    // ── Breadcrumbs ──
    private record BreadcrumbItem(string Label, Action Action);
    private List<BreadcrumbItem> _breadcrumb = [];

    // ── Timer ──
    private PeriodicTimer? _playbackTimer;
    private CancellationTokenSource? _timerCts;

    // ── JS Audio Interop ──
    private DotNetObjectReference<MusicPage>? _dotNetRef;
    private bool _jsInitialized;

    // ── Auth ──
    private CallerContext? _caller;

    // ── Deep-link parameters (from search) ──
    [Parameter] public string? FileId { get; set; }
    [Parameter] public string? FileIdNav { get; set; }
    private string? _lastHandledNav;

    // ── Library Settings ──
    private string _libraryPath = string.Empty;
    private Guid? _libraryFolderId;
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

    // ────────────────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _loading = true;
            var caller = await GetCallerAsync();
            _caller = caller;
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
            await LoadEqPresetsAsync(caller);
            await LoadLibraryPathAsync();
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
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_jsInitialized)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.init", _dotNetRef);
                _jsInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize music player JS interop");
            }
        }

        // Auto-play needs JS audio to be ready; trigger play if a file was queued during init
        if (_jsInitialized && _pendingAutoPlayTrack is not null)
        {
            var track = _pendingAutoPlayTrack;
            _pendingAutoPlayTrack = null;
            await PlayTrackAsync(track);
            StateHasChanged();
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
                    _artists = (await ArtistService.ListArtistsAsync(caller, 0, 200)).ToList();
                    break;

                case Section.Albums:
                    _albums = (await AlbumService.ListAlbumsAsync(caller, 0, 200)).ToList();
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

    private async void OpenArtistDetail(ArtistDto artist)
    {
        _selectedArtist = artist;
        _breadcrumb =
        [
            new BreadcrumbItem("Artists", () => { _selectedArtist = null; _breadcrumb.Clear(); StateHasChanged(); })
        ];

        try
        {
            var caller = await GetCallerAsync();
            _artistAlbums = (await AlbumService.ListAlbumsByArtistAsync(artist.Id, caller)).ToList();
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
        _nowPlaying = track;
        _isPlaying = true;
        _playbackPosition = TimeSpan.Zero;

        // Insert into queue if not already
        if (!_queue.Any(t => t.Id == track.Id))
        {
            _queue.Add(track);
            _queueIndex = _queue.Count - 1;
        }
        else
        {
            _queueIndex = _queue.FindIndex(t => t.Id == track.Id);
        }

        // Start real audio playback via JS interop using the Files content endpoint
        if (_jsInitialized && track.FileNodeId != Guid.Empty)
        {
            var audioUrl = $"/api/v1/files/{track.FileNodeId}/content";
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.play", audioUrl);
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setVolume", _muted ? 0 : _volume);
            // Re-apply current EQ band settings to the new track
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", _eqBands);

            // Start visualizer render loop if toggled on
            if (_showVisualizer && !_visualizerStarted)
            {
                _visualizerStarted = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.start");
            }
        }

        await StartPlaybackTimerAsync();

        try
        {
            var caller = await GetCallerAsync();
            await PlaybackService.RecordPlayAsync(track.Id, (int)track.Duration.TotalSeconds, caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error recording play");
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
                _queue = tracks;
                _queueIndex = 0;
                await PlayTrackAsync(tracks[0]);
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
                _queue = tracks;
                _queueIndex = 0;
                await PlayTrackAsync(tracks[0]);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error playing playlist");
        }
    }

    private async Task TogglePlayPause()
    {
        _isPlaying = !_isPlaying;
        if (_jsInitialized)
        {
            if (_isPlaying)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.resume");
                // Resume visualizer render loop if active
                if (_showVisualizer && !_visualizerStarted)
                {
                    _visualizerStarted = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.start");
                }
            }
            else
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.pause");
                // Pause visualizer render loop to save GPU
                if (_visualizerStarted)
                {
                    await Js.InvokeVoidAsync("dotnetcloudVisualizer.stop");
                    _visualizerStarted = false;
                }
            }
        }
    }

    private async Task PlayNextAsync()
    {
        if (_queue.Count == 0) return;

        if (_shuffle)
        {
            _queueIndex = Random.Shared.Next(_queue.Count);
        }
        else
        {
            _queueIndex++;
            if (_queueIndex >= _queue.Count)
            {
                _queueIndex = _repeat == RepeatMode.All ? 0 : _queue.Count - 1;
                if (_repeat == RepeatMode.Off)
                {
                    _isPlaying = false;
                    return;
                }
            }
        }

        await PlayTrackAsync(_queue[_queueIndex]);
    }

    private async Task PlayPreviousAsync()
    {
        if (_queue.Count == 0) return;

        // If more than 3 seconds in, restart current track
        if (_playbackPosition.TotalSeconds > 3)
        {
            _playbackPosition = TimeSpan.Zero;
            return;
        }

        _queueIndex = Math.Max(0, _queueIndex - 1);
        await PlayTrackAsync(_queue[_queueIndex]);
    }

    private Task AddToQueueAsync(TrackDto track)
    {
        _queue.Add(track);
        _trackMenuId = null;
        return Task.CompletedTask;
    }

    private Task RemoveFromQueueAsync(int index)
    {
        if (index >= 0 && index < _queue.Count)
        {
            _queue.RemoveAt(index);
            if (index < _queueIndex) _queueIndex--;
        }
        return Task.CompletedTask;
    }

    private void ToggleShuffle() => _shuffle = !_shuffle;

    private void CycleRepeat()
    {
        _repeat = _repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.Off
        };
    }

    private async Task ToggleMute()
    {
        _muted = !_muted;
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setMuted", _muted);
        }
    }

    private async Task OnVolumeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var vol))
        {
            _volume = vol;
            if (_jsInitialized)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setVolume", _muted ? 0 : _volume);
            }
        }
    }

    private async Task SeekAsync(MouseEventArgs e)
    {
        if (_nowPlaying is null) return;
        // Approximate seek from click position within the progress bar (assumed ~400px wide)
        // The progress bar fraction is estimated from OffsetX vs element width.
        // A more precise approach would use JS interop to get the exact element width.
        var fraction = Math.Clamp(e.OffsetX / 400.0, 0, 1);
        _playbackPosition = TimeSpan.FromTicks((long)(_nowPlaying.Duration.Ticks * fraction));
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.seek", _playbackPosition.TotalSeconds);
        }
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
            if (_nowPlaying?.Id == track.Id)
                _nowPlaying = updated;
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

    // ────────────────────────────────────────────────────────
    //  Equalizer
    // ────────────────────────────────────────────────────────

    private async Task LoadEqPresetsAsync(CallerContext caller)
    {
        try
        {
            _eqPresets = (await EqPresetService.ListPresetsAsync(caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load EQ presets");
        }
    }

    private async Task ApplyPresetAsync(EqPresetDto preset)
    {
        _activePresetId = preset.Id;
        if (preset.Bands is not null && preset.Bands.Count >= 10)
        {
            var bandValues = preset.Bands.Values.Take(10).ToArray();
            for (int i = 0; i < bandValues.Length; i++)
                _eqBands[i] = bandValues[i];
        }

        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", _eqBands);
        }
    }

    private async Task OnEqBandChanged(int bandIndex, ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var gain))
        {
            _eqBands[bandIndex] = gain;
            if (_jsInitialized)
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBand", bandIndex, gain);
            }
        }
    }

    private async Task ResetEqBands()
    {
        _activePresetId = null;
        Array.Clear(_eqBands);
        if (_jsInitialized)
        {
            await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.setEqBands", _eqBands);
        }
    }

    private async Task SaveCurrentAsPresetAsync()
    {
        if (string.IsNullOrWhiteSpace(_newPresetName)) return;

        try
        {
            var caller = await GetCallerAsync();
            var bands = new Dictionary<string, double>();
            for (int i = 0; i < _eqBands.Length && i < _bandLabels.Length; i++)
            {
                bands[_bandLabels[i]] = _eqBands[i];
            }

            var dto = new SaveEqPresetDto
            {
                Name = _newPresetName.Trim(),
                Bands = bands
            };

            var created = await EqPresetService.CreatePresetAsync(dto, caller);
            _eqPresets.Add(created);
            _activePresetId = created.Id;
            _showSavePresetDialog = false;
            _newPresetName = string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save EQ preset");
        }
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

    private string GetSectionTitle() => _section switch
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

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";
    }

    private static string GetAlbumArtUrl(Guid albumId) => $"/api/v1/music/albums/{albumId}/cover";

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }

    private double GetProgressPercent()
    {
        if (_nowPlaying is null || _nowPlaying.Duration.TotalSeconds < 1) return 0;
        return _playbackPosition.TotalSeconds / _nowPlaying.Duration.TotalSeconds * 100;
    }

    private async Task<CallerContext> GetCallerAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? throw new UnauthorizedAccessException("Not authenticated.");
        var roles = authState.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(Guid.Parse(userId), roles, CallerType.User);
    }

    // ────────────────────────────────────────────────────────
    //  Playback timer (simulated progress)
    // ────────────────────────────────────────────────────────

    private async Task StartPlaybackTimerAsync()
    {
        await StopPlaybackTimerAsync();
        _timerCts = new CancellationTokenSource();
        _playbackTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _ = Task.Run(async () =>
        {
            try
            {
                while (await _playbackTimer.WaitForNextTickAsync(_timerCts.Token))
                {
                    if (!_isPlaying || _nowPlaying is null) continue;
                    _playbackPosition += TimeSpan.FromSeconds(1);
                    if (_playbackPosition >= _nowPlaying.Duration)
                    {
                        await InvokeAsync(async () => await PlayNextAsync());
                    }
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private Task StopPlaybackTimerAsync()
    {
        _timerCts?.Cancel();
        _playbackTimer?.Dispose();
        _playbackTimer = null;
        _timerCts?.Dispose();
        _timerCts = null;
        return Task.CompletedTask;
    }

    // ── Library Settings Methods ─────────────────────────────

    private async Task LoadLibraryPathAsync()
    {
        if (_caller is null) return;
        try
        {
            var setting = await UserSettingsService.GetSettingAsync(_caller.UserId, "media-library", "music-path");
            _libraryPath = setting?.Value ?? string.Empty;
            var folderIdSetting = await UserSettingsService.GetSettingAsync(_caller.UserId, "media-library", "music-folder-id");
            _libraryFolderId = Guid.TryParse(folderIdSetting?.Value, out var fid) ? fid : null;
        }
        catch { /* ignore load failures */ }
    }

    private async Task SaveLibraryPathAsync()
    {
        if (_caller is null) return;
        _settingsSaving = true;
        _settingsError = null;
        _settingsSuccess = null;
        try
        {
            await UserSettingsService.UpsertSettingAsync(_caller.UserId, "media-library", "music-path",
                new UpsertUserSettingDto { Value = _libraryPath.Trim(), Description = "Music library folder path" });
            await UserSettingsService.UpsertSettingAsync(_caller.UserId, "media-library", "music-folder-id",
                new UpsertUserSettingDto { Value = _libraryFolderId?.ToString() ?? string.Empty, Description = "Music library folder ID" });
            _settingsSuccess = "Path saved.";
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
        if (_caller is null || string.IsNullOrWhiteSpace(_libraryPath)) return;
        await SaveLibraryPathAsync();
        if (_settingsError is not null) return;

        _settingsScanning = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        StateHasChanged();
        try
        {
            _scanResult = await MediaLibraryScanner.ScanFolderAsync(_libraryFolderId, _caller.UserId, "Music");
            _settingsSuccess = $"Scan complete: {_scanResult.Imported} imported, {_scanResult.Skipped} already up to date.";

            // Reload library data so navigating to Library shows freshly imported tracks
            var caller = await GetCallerAsync();
            _recentAlbums = (await AlbumService.ListAlbumsAsync(caller, 0, 8)).ToList();
            _newTracks = (await TrackService.ListTracksAsync(caller, 0, 10)).ToList();
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
        }
        catch (Exception ex)
        {
            _settingsError = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _settingsScanning = false;
        }
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
            _queue.Clear();
            _nowPlaying = null;
            _isPlaying = false;
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

    private void ConfirmDirectoryBrowser()
    {
        _libraryPath = GetDirBrowserPath();
        _libraryFolderId = _dirBrowserFolderId;
        _showDirBrowser = false;
    }

    // ────────────────────────────────────────────────────────
    //  JS Interop callbacks (invoked from music-player.js)
    // ────────────────────────────────────────────────────────

    /// <summary>Called by JS when audio currentTime changes.</summary>
    [JSInvokable]
    public void OnJsTimeUpdate(double currentTime, double duration)
    {
        _playbackPosition = TimeSpan.FromSeconds(currentTime);
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS when the current track finishes playing.</summary>
    [JSInvokable]
    public async Task OnJsTrackEnded()
    {
        await InvokeAsync(async () => await PlayNextAsync());
    }

    /// <summary>Called by JS when an audio error occurs.</summary>
    [JSInvokable]
    public void OnJsPlaybackError(string message)
    {
        Logger.LogWarning("Audio playback error: {Message}", message);
        _isPlaying = false;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS when audio metadata (duration) is loaded from the file.</summary>
    [JSInvokable]
    public void OnJsMetadataLoaded(double duration)
    {
        // If the track has no duration stored, use the one from the audio element
        if (_nowPlaying is not null && _nowPlaying.Duration.TotalSeconds < 1 && duration > 0)
        {
            _nowPlaying = _nowPlaying with { Duration = TimeSpan.FromSeconds(duration) };
            InvokeAsync(StateHasChanged);
        }
    }

    // ────────────────────────────────────────────────────────
    //  Visualizer
    // ────────────────────────────────────────────────────────

    private async Task ToggleVisualizerAsync()
    {
        _showVisualizer = !_showVisualizer;

        if (_showVisualizer)
        {
            await StartVisualizerAsync();
        }
        else
        {
            await StopVisualizerAsync();
        }
    }

    private async Task StartVisualizerAsync()
    {
        if (!_jsInitialized) return;
        try
        {
            _visualizerSupported = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.isSupported");
            if (!_visualizerSupported)
            {
                _showVisualizer = false;
                return;
            }

            var initOk = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.init", "dnc-visualizer-canvas");
            if (!initOk) return;

            await Js.InvokeVoidAsync("dotnetcloudVisualizer.initMiniCanvas", "dnc-mini-visualizer");

            // Load curated preset names
            _visualizerPresets = await Js.InvokeAsync<string[]>("dotnetcloudVisualizer.getPresetNames");

            if (_isPlaying)
            {
                _visualizerStarted = await Js.InvokeAsync<bool>("dotnetcloudVisualizer.start");
                if (_visualizerStarted && _visualizerPresets.Length > 0)
                {
                    _selectedVisualizerPreset = await Js.InvokeAsync<string?>("dotnetcloudVisualizer.getCurrentPresetName");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to start visualizer");
            _showVisualizer = false;
        }
    }

    private async Task StopVisualizerAsync()
    {
        if (!_jsInitialized) return;
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
        if (!_jsInitialized || !_visualizerStarted) return;
        await Js.InvokeAsync<bool>("dotnetcloudVisualizer.loadPreset", presetName, _visualizerBlendDuration);
        _selectedVisualizerPreset = presetName;
    }

    private async Task RandomVisualizerPresetAsync()
    {
        if (!_jsInitialized || !_visualizerStarted) return;
        var name = await Js.InvokeAsync<string?>("dotnetcloudVisualizer.randomPreset", _visualizerBlendDuration);
        if (name is not null)
        {
            _selectedVisualizerPreset = name;
        }
    }

    private async Task LoadAllVisualizerPresetsAsync()
    {
        if (_allPresetsLoaded || _loadingAllPresets || !_jsInitialized) return;
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
        if (!_jsInitialized) return;
        await Js.InvokeVoidAsync("dotnetcloudVisualizer.enterFullscreen");
    }

    private async Task ToggleAutoCyclePresetsAsync()
    {
        _autoCyclePresets = !_autoCyclePresets;
        if (!_jsInitialized) return;
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
        await StopPlaybackTimerAsync();
        if (_jsInitialized)
        {
            try
            {
                await Js.InvokeVoidAsync("dotnetcloudVisualizer.dispose");
            }
            catch (JSDisconnectedException) { }
            try
            {
                await Js.InvokeVoidAsync("dotnetcloudMusicPlayer.dispose");
            }
            catch (JSDisconnectedException) { }
        }
        _dotNetRef?.Dispose();
    }

    // ────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────

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
