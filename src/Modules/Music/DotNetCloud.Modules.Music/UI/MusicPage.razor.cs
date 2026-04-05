using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
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
    private enum Section { Library, Artists, Albums, Genres, Playlists, Favorites, RecentlyPlayed }
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

    // ── Breadcrumbs ──
    private record BreadcrumbItem(string Label, Action Action);
    private List<BreadcrumbItem> _breadcrumb = [];

    // ── Timer ──
    private PeriodicTimer? _playbackTimer;
    private CancellationTokenSource? _timerCts;

    // ────────────────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _loading = true;
            var caller = await GetCallerAsync();
            _playlists = (await PlaylistService.ListPlaylistsAsync(caller)).ToList();
            await LoadCurrentSectionAsync();
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
                    _starredTracks = (await RecommendationService.GetMostPlayedAsync(caller)).ToList();
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
        _selectedAlbum = album;

        // Build breadcrumbs that depend on how we got here
        if (_selectedArtist is not null)
        {
            var capturedArtist = _selectedArtist;
            _breadcrumb =
            [
                new BreadcrumbItem("Artists", () => { _selectedArtist = null; _selectedAlbum = null; _breadcrumb.Clear(); StateHasChanged(); }),
                new BreadcrumbItem(capturedArtist.Name, () => { _selectedAlbum = null; _breadcrumb.RemoveAt(_breadcrumb.Count - 1); StateHasChanged(); })
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

    private void TogglePlayPause()
    {
        _isPlaying = !_isPlaying;
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

    private void ToggleMute() => _muted = !_muted;

    private void SeekAsync(MouseEventArgs e)
    {
        // Approximate seek from click position - real implementation would use JS interop
        if (_nowPlaying is not null)
        {
            _playbackPosition = TimeSpan.FromTicks(_nowPlaying.Duration.Ticks / 2);
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
            // Optimistic UI update via a simple property swap
            var index = _tracks.FindIndex(t => t.Id == track.Id);
            // Starred state toggled server-side; reload would be cleanest
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

    private async Task ApplyPresetAsync(EqPresetDto preset)
    {
        _activePresetId = preset.Id;
        if (preset.Bands is not null && preset.Bands.Count >= 10)
        {
            var bandValues = preset.Bands.Values.Take(10).ToArray();
            for (int i = 0; i < bandValues.Length; i++)
                _eqBands[i] = bandValues[i];
        }
        await Task.CompletedTask;
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

    private static string GetAlbumArtUrl(Guid albumId) => $"/api/music/albums/{albumId}/cover";

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

    public async ValueTask DisposeAsync()
    {
        await StopPlaybackTimerAsync();
    }
}
