using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Video.UI;

/// <summary>
/// Code-behind for the Video module Blazor page.
/// </summary>
public partial class VideoPage : IAsyncDisposable
{
    // ── Section / State ──
    private enum Section { Home, Library, Collections, Favorites }

    private Section _section = Section.Home;
    private bool _loading;
    private string? _errorMessage;
    private string _searchQuery = string.Empty;

    // ── Data ──
    private List<VideoDto> _videos = [];
    private List<VideoDto> _recentVideos = [];
    private List<VideoDto> _favoriteVideos = [];
    private List<VideoDto> _collectionVideos = [];
    private List<VideoDto>? _searchResults;
    private List<WatchProgressDto> _continueWatching = [];
    private List<VideoCollectionDto> _collections = [];

    // ── Selection ──
    private VideoCollectionDto? _selectedCollection;
    private Guid? _selectedCollectionId;

    // ── Player state ──
    private bool _playerOpen;
    private VideoDto? _playerVideo;
    private VideoMetadataDto? _playerMetadata;
    private List<SubtitleDto> _playerSubtitles = [];
    private string? _streamToken;

    // ── Dialogs ──
    private bool _showCreateCollectionDialog;
    private bool _showEditCollectionDialog;
    private bool _showAddToCollection;
    private Guid? _editCollectionId;
    private string _collectionName = string.Empty;
    private string _collectionDescription = string.Empty;

    // ── Breadcrumbs ──
    private record BreadcrumbItem(string Label, Action Action);
    private List<BreadcrumbItem> _breadcrumb = [];

    // ── Timer for progress saving ──
    private PeriodicTimer? _progressTimer;
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
            _collections = (await CollectionService.ListCollectionsAsync(caller)).ToList();
            await LoadCurrentSectionAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Video page");
            _errorMessage = "Failed to load video library.";
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
        _selectedCollection = null;
        _selectedCollectionId = null;
        _searchResults = null;
        _playerOpen = false;
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
                case Section.Home:
                    _continueWatching = (await WatchProgressService.GetContinueWatchingAsync(caller, 10)).ToList();
                    _recentVideos = (await VideoService.GetRecentVideosAsync(caller, 12)).ToList();
                    break;

                case Section.Library:
                    _videos = (await VideoService.ListVideosAsync(caller, 0, 200)).ToList();
                    break;

                case Section.Collections:
                    _collections = (await CollectionService.ListCollectionsAsync(caller)).ToList();
                    break;

                case Section.Favorites:
                    _favoriteVideos = (await VideoService.GetFavoritesAsync(caller)).ToList();
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
    //  Video Detail → Player
    // ────────────────────────────────────────────────────────

    private async void OpenVideoDetail(VideoDto video)
    {
        try
        {
            var caller = await GetCallerAsync();
            _playerVideo = video;
            _playerOpen = true;
            _playerSubtitles = (await SubtitleService.GetSubtitlesAsync(video.Id, caller)).ToList();
            _playerMetadata = await MetadataService.GetMetadataAsync(video.Id);

            // Generate streaming token
            _streamToken = StreamingService.GenerateStreamToken(video.Id, caller.UserId);

            // Record view
            await WatchProgressService.RecordViewAsync(video.Id, caller);

            // Start progress saving timer
            await StartProgressTimerAsync(video.Id);

            _breadcrumb =
            [
                new BreadcrumbItem(GetSectionLabel(), () => { ClosePlayer(); StateHasChanged(); })
            ];

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening video player");
        }
    }

    private async void OpenPlayerForContinue(WatchProgressDto wp)
    {
        try
        {
            var caller = await GetCallerAsync();
            var video = await VideoService.GetVideoAsync(wp.VideoId, caller);
            if (video is not null)
            {
                OpenVideoDetail(video);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resuming video");
        }
    }

    private void ClosePlayer()
    {
        _playerOpen = false;
        _playerVideo = null;
        _playerMetadata = null;
        _playerSubtitles.Clear();
        _streamToken = null;
        _breadcrumb.Clear();
        StopProgressTimer();
    }

    // ────────────────────────────────────────────────────────
    //  Favorites
    // ────────────────────────────────────────────────────────

    private async Task ToggleFavoriteAsync()
    {
        if (_playerVideo is null) return;
        try
        {
            var caller = await GetCallerAsync();
            await VideoService.ToggleFavoriteAsync(_playerVideo.Id, caller);
            // Refresh the player video to reflect updated state
            _playerVideo = await VideoService.GetVideoAsync(_playerVideo.Id, caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling favorite");
        }
    }

    // ────────────────────────────────────────────────────────
    //  Collections
    // ────────────────────────────────────────────────────────

    private async Task SelectCollectionAsync(Guid collectionId)
    {
        _section = Section.Collections;
        _selectedCollectionId = collectionId;
        _selectedCollection = _collections.FirstOrDefault(c => c.Id == collectionId);

        _breadcrumb =
        [
            new BreadcrumbItem("Collections", () => { _selectedCollection = null; _selectedCollectionId = null; _breadcrumb.Clear(); StateHasChanged(); })
        ];

        try
        {
            var caller = await GetCallerAsync();
            _collectionVideos = (await CollectionService.GetCollectionVideosAsync(collectionId, caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading collection videos");
        }
        StateHasChanged();
    }

    private void BeginCreateCollection()
    {
        _collectionName = string.Empty;
        _collectionDescription = string.Empty;
        _editCollectionId = null;
        _showCreateCollectionDialog = true;
    }

    private void BeginEditCollection(VideoCollectionDto coll)
    {
        _collectionName = coll.Name;
        _collectionDescription = coll.Description ?? string.Empty;
        _editCollectionId = coll.Id;
        _showEditCollectionDialog = true;
    }

    private void CloseCollectionDialog()
    {
        _showCreateCollectionDialog = false;
        _showEditCollectionDialog = false;
    }

    private async Task SaveCollectionAsync()
    {
        try
        {
            var caller = await GetCallerAsync();
            if (_showEditCollectionDialog && _editCollectionId.HasValue)
            {
                await CollectionService.UpdateCollectionAsync(_editCollectionId.Value,
                    new UpdateVideoCollectionDto { Name = _collectionName, Description = _collectionDescription }, caller);
            }
            else
            {
                await CollectionService.CreateCollectionAsync(
                    new CreateVideoCollectionDto { Name = _collectionName, Description = _collectionDescription }, caller);
            }
            _collections = (await CollectionService.ListCollectionsAsync(caller)).ToList();
            CloseCollectionDialog();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving collection");
        }
    }

    private async Task DeleteCollectionAsync(Guid collectionId)
    {
        try
        {
            var caller = await GetCallerAsync();
            await CollectionService.DeleteCollectionAsync(collectionId, caller);
            _collections = (await CollectionService.ListCollectionsAsync(caller)).ToList();
            if (_selectedCollectionId == collectionId)
            {
                _selectedCollection = null;
                _selectedCollectionId = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting collection");
        }
    }

    private async Task AddToCollectionAsync(Guid collectionId)
    {
        if (_playerVideo is null) return;
        try
        {
            var caller = await GetCallerAsync();
            await CollectionService.AddVideoAsync(collectionId, _playerVideo.Id, caller);
            _showAddToCollection = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding video to collection");
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
                _searchResults = (await VideoService.SearchAsync(caller, _searchQuery, 50)).ToList();
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
    //  Helpers
    // ────────────────────────────────────────────────────────

    private string GetSectionTitle() => _section switch
    {
        Section.Home => "Home",
        Section.Library => "Library",
        Section.Collections when _selectedCollection is not null => _selectedCollection.Name,
        Section.Collections => "Collections",
        Section.Favorites => "Favorites",
        _ => "Video"
    };

    private string GetSectionLabel() => _section switch
    {
        Section.Home => "Home",
        Section.Library => "Library",
        Section.Collections => "Collections",
        Section.Favorites => "Favorites",
        _ => "Video"
    };

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";
    }

    private static string FormatFileSize(long bytes)
    {
        const double gb = 1024 * 1024 * 1024;
        const double mb = 1024 * 1024;
        return bytes >= gb
            ? $"{bytes / gb:F1} GB"
            : $"{bytes / mb:F1} MB";
    }

    private static string FormatResolution(int? width, int? height)
    {
        if (width is null || height is null) return "Unknown";
        return height switch
        {
            >= 2160 => "4K",
            >= 1080 => "1080p",
            >= 720 => "720p",
            >= 480 => "480p",
            _ => $"{width}×{height}"
        };
    }

    private static string FormatBitrate(long bitrate)
    {
        return bitrate >= 1_000_000
            ? $"{bitrate / 1_000_000.0:F1} Mbps"
            : $"{bitrate / 1_000.0:F0} kbps";
    }

    private static string GetThumbnailUrl(Guid videoId) => $"/api/video/{videoId}/thumbnail";

    private string GetStreamUrl(Guid videoId) =>
        _streamToken is not null
            ? $"/api/video/{videoId}/stream?token={Uri.EscapeDataString(_streamToken)}"
            : $"/api/video/{videoId}/stream";

    private static string GetSubtitleUrl(Guid subtitleId) => $"/api/video/subtitles/{subtitleId}";

    private static double GetWatchPercent(VideoDto video)
    {
        if (video.WatchPositionTicks is null || video.Duration.Ticks < 1) return 0;
        return (double)video.WatchPositionTicks.Value / video.Duration.Ticks * 100;
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
    //  Progress saving timer
    // ────────────────────────────────────────────────────────

    private async Task StartProgressTimerAsync(Guid videoId)
    {
        StopProgressTimer();
        _timerCts = new CancellationTokenSource();
        _progressTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        _ = Task.Run(async () =>
        {
            try
            {
                while (await _progressTimer.WaitForNextTickAsync(_timerCts.Token))
                {
                    // In a real implementation, JS interop would report currentTime
                    // For now, progress is saved on player events
                }
            }
            catch (OperationCanceledException) { }
        });

        await Task.CompletedTask;
    }

    private void StopProgressTimer()
    {
        _timerCts?.Cancel();
        _progressTimer?.Dispose();
        _progressTimer = null;
        _timerCts?.Dispose();
        _timerCts = null;
    }

    public ValueTask DisposeAsync()
    {
        StopProgressTimer();
        return ValueTask.CompletedTask;
    }
}
