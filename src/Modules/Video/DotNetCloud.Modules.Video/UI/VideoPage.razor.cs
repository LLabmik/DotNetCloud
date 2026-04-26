using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
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
    private enum Section { Home, Library, Collections, Favorites, Settings }

    private Section _section = Section.Home;
    private bool _sidebarCollapsed;
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

    // ── Paging ──
    private int _videoPage;
    private const int VideoPageSize = 50;
    private int _totalVideos;
    private bool _hasMoreVideos;

    private int _recentPage;
    private const int RecentPageSize = 12;
    private int _totalRecentVideos;
    private bool _hasMoreRecent;

    // ── Selection ──
    private VideoCollectionDto? _selectedCollection;
    private Guid? _selectedCollectionId;

    // ── Player state ──
    private bool _playerOpen;
    private VideoDto? _playerVideo;
    private VideoMetadataDto? _playerMetadata;
    private List<SubtitleDto> _playerSubtitles = [];
    private string? _streamToken;
    private string? _codecErrorMessage;
    private bool _noAudioDetected;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _idleAutoHideHandle;
    private IJSObjectReference? _keyboardShortcutsHandle;
    private DotNetObjectReference<VideoPage>? _dotNetRef;
    private bool _videoErrorListenerAttached;

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

    // ── Auth ──
    private CallerContext? _caller;

    // ── Library Settings ──
    private List<MediaLibrarySource> _librarySources = [];
    private bool _settingsSaving;
    private bool _settingsScanning;
    private string? _settingsError;
    private string? _settingsSuccess;
    private MediaScanResult? _scanResult;

    // Reset Collection
    private bool _showResetConfirm;
    private bool _settingsResetting;

    // Enrichment state
    private bool _tmdbAvailable;
    private bool _enrichingVideo;
    private Guid? _enrichingVideoId;
    private string? _enrichmentToast;
    private bool _autoFetchMetadata = true;
    private bool _autoFetchPosters = true;
    private bool _settingsEnriching;

    // Directory Browser
    private bool _showDirBrowser;
    private Guid? _dirBrowserFolderId;
    private List<(Guid Id, string Name)> _dirBrowserFolders = [];
    private List<(Guid Id, string Name)> _dirBrowserBreadcrumbs = [];
    private string? _dirBrowserError;

    // Deep-link from Files module
    private string? _lastHandledNav;

    /// <summary>Optional file ID to auto-open on load (deep-link from Files module).</summary>
    [Parameter] public string? FileId { get; set; }

    /// <summary>Navigation nonce — changes each time a file is clicked, even for the same file.</summary>
    [Parameter] public string? FileIdNav { get; set; }

    // ────────────────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_playerOpen && !_videoErrorListenerAttached)
        {
            _videoErrorListenerAttached = true;
            try
            {
                _jsModule ??= await Js.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/DotNetCloud.Modules.Video/video-player.js");
                _dotNetRef ??= DotNetObjectReference.Create(this);
                await _jsModule.InvokeVoidAsync("attachVideoErrorListener", "video-player", _dotNetRef);
                _idleAutoHideHandle = await _jsModule.InvokeAsync<IJSObjectReference>("attachIdleAutoHide", "player-container", 3000);
                _keyboardShortcutsHandle = await _jsModule.InvokeAsync<IJSObjectReference>("attachKeyboardShortcuts", "video-player");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to attach video error listener");
            }
        }
    }

    /// <summary>Called from JS when the video element fires an error event.</summary>
    [JSInvokable]
    public void OnVideoError(int code, string message, int? httpStatus, string? httpStatusText)
    {
        var codeLabel = code switch
        {
            1 => "MEDIA_ERR_ABORTED",
            2 => "MEDIA_ERR_NETWORK",
            3 => "MEDIA_ERR_DECODE",
            4 => "MEDIA_ERR_SRC_NOT_SUPPORTED",
            _ => $"Unknown ({code})"
        };

        var diagnostic = $"Code: {codeLabel}";
        if (!string.IsNullOrWhiteSpace(message))
            diagnostic += $" — {message}";
        if (httpStatus.HasValue)
            diagnostic += $" | HTTP {httpStatus}";
        if (!string.IsNullOrWhiteSpace(httpStatusText))
            diagnostic += $" ({httpStatusText})";

        Logger.LogWarning("Video playback error: {Diagnostic}", diagnostic);
        _codecErrorMessage = diagnostic;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called from JS when the video plays but no audio track is decoded (e.g. Firefox + Dolby Digital).</summary>
    [JSInvokable]
    public void OnNoAudio()
    {
        _noAudioDetected = true;
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            try
            {
                var collapsed = await Js.InvokeAsync<string>("localStorage.getItem", new object?[] { "dotnetcloud.sidebar:video" });
                if (bool.TryParse(collapsed ?? "false", out var parsed))
                {
                    _sidebarCollapsed = parsed;
                }
            }
            catch
            {
                // localStorage unavailable
            }

            _loading = true;
            _caller = await GetCallerAsync();

            _tmdbAvailable = EnrichmentService.IsTmdbAvailable;

            // Deep-link: auto-open from Files module if fileId parameter was supplied on first load
            if (!string.IsNullOrEmpty(FileId) && Guid.TryParse(FileId, out var fileId))
            {
                _lastHandledNav = FileIdNav;
                await TryAutoPlayFileAsync(fileId, _caller);
            }

            _collections = (await CollectionService.ListCollectionsAsync(_caller)).ToList();
            await LoadLibraryPathAsync();
            await LoadEnrichmentSettingsAsync();
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

    protected override async Task OnParametersSetAsync()
    {
        if (_caller is null) return;

        // Handle fileId changes when already on the page (same-page navigation via Files module).
        // FileIdNav is a timestamp nonce that changes on every click, even for the same file.
        if (!string.IsNullOrEmpty(FileId) && FileIdNav != _lastHandledNav && Guid.TryParse(FileId, out var fileId))
        {
            _lastHandledNav = FileIdNav;
            await TryAutoPlayFileAsync(fileId, _caller);
        }
    }

    private async Task ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        try
        {
            await Js.InvokeAsync<object?>("localStorage.setItem", new object?[] { "dotnetcloud.sidebar:video", _sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch
        {
            // localStorage unavailable
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
        _searchQuery = string.Empty;
        _playerOpen = false;
        _videoPage = 0;
        _recentPage = 0;
        _breadcrumb.Clear();
        await LoadCurrentSectionAsync();
    }

    private async Task LoadCurrentSectionAsync()
    {
        if (_section == Section.Settings)
        {
            _loading = false;
            _errorMessage = null;
            StateHasChanged();
            return;
        }

        if (_caller is null) return;

        try
        {
            _loading = true;
            _errorMessage = null;
            StateHasChanged();

            switch (_section)
            {
                case Section.Home:
                    _continueWatching = (await WatchProgressService.GetContinueWatchingAsync(_caller, 10)).ToList();
                    await LoadRecentPageAsync();
                    break;

                case Section.Library:
                    await LoadVideosPageAsync();
                    break;

                case Section.Collections:
                    _collections = (await CollectionService.ListCollectionsAsync(_caller)).ToList();
                    break;

                case Section.Favorites:
                    _favoriteVideos = (await VideoService.GetFavoritesAsync(_caller)).ToList();
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

    // ── Paging ──

    private async Task LoadVideosPageAsync()
    {
        if (_caller is null) return;

        _totalVideos = await VideoService.GetVideoCountAsync(_caller.UserId);
        var videos = (await VideoService.ListVideosAsync(_caller, _videoPage * VideoPageSize, VideoPageSize)).ToList();
        _hasMoreVideos = (_videoPage + 1) * VideoPageSize < _totalVideos;
        _videos = videos;
    }

    private async Task PrevVideoPageAsync()
    {
        if (_videoPage > 0)
        {
            _videoPage--;
            await LoadVideosPageAsync();
        }
    }

    private async Task NextVideoPageAsync()
    {
        if (!_hasMoreVideos) return;

        _videoPage++;
        await LoadVideosPageAsync();
    }

    private async Task LoadRecentPageAsync()
    {
        if (_caller is null) return;

        _totalRecentVideos = await VideoService.GetVideoCountAsync(_caller.UserId);
        var videos = (await VideoService.GetRecentVideosAsync(_caller, _recentPage * RecentPageSize, RecentPageSize)).ToList();
        _hasMoreRecent = (_recentPage + 1) * RecentPageSize < _totalRecentVideos;
        _recentVideos = videos;
    }

    private async Task PrevRecentPageAsync()
    {
        if (_recentPage > 0)
        {
            _recentPage--;
            await LoadRecentPageAsync();
        }
    }

    private async Task NextRecentPageAsync()
    {
        if (!_hasMoreRecent) return;

        _recentPage++;
        await LoadRecentPageAsync();
    }

    // ────────────────────────────────────────────────────────
    //  Video Detail → Player
    // ────────────────────────────────────────────────────────

    private async Task OpenVideoDetailAsync(VideoDto video)
    {
        try
        {
            var caller = await GetCallerAsync();
            _playerVideo = video;
            _playerSubtitles = (await SubtitleService.GetSubtitlesAsync(video.Id, caller)).ToList();
            _playerMetadata = await MetadataService.GetMetadataAsync(video.Id);

            _streamToken = StreamingService.GenerateStreamToken(video.Id, caller.UserId);
            _codecErrorMessage = null;
            _noAudioDetected = false;
            _videoErrorListenerAttached = false;
            _playerOpen = true;
            await WatchProgressService.RecordViewAsync(video.Id, caller);
            await StartProgressTimerAsync(video.Id);

            _breadcrumb =
            [
                new BreadcrumbItem(GetSectionLabel(), () => { ClosePlayer(); StateHasChanged(); })
            ];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening video player");
        }
    }

    private async Task OpenPlayerForContinueAsync(WatchProgressDto wp)
    {
        try
        {
            var caller = await GetCallerAsync();
            var video = await VideoService.GetVideoAsync(wp.VideoId, caller);
            if (video is not null)
            {
                await OpenVideoDetailAsync(video);
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
        _codecErrorMessage = null;
        _noAudioDetected = false;
        _videoErrorListenerAttached = false;
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

    private static string GetThumbnailUrl(Guid videoId) => $"/api/v1/videos/{videoId}/thumbnail";

    private string GetStreamUrl(Guid videoId) =>
        _streamToken is not null
            ? $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(_streamToken)}"
            : $"/api/v1/videos/{videoId}/stream";

    private static string GetSubtitleUrl(Guid subtitleId) => $"/api/v1/videos/subtitles/{subtitleId}";

    private static double GetWatchPercent(VideoDto video)
    {
        if (video.WatchPositionTicks is null || video.Duration.Ticks < 1) return 0;
        return (double)video.WatchPositionTicks.Value / video.Duration.Ticks * 100;
    }

    /// <summary>
    /// Looks up a video by its Files-module FileNodeId and opens it in the player.
    /// </summary>
    private async Task TryAutoPlayFileAsync(Guid fileNodeId, CallerContext caller)
    {
        try
        {
            var video = await VideoService.GetVideoByFileNodeIdAsync(fileNodeId, caller);
            if (video is not null)
            {
                await OpenVideoDetailAsync(video);
            }
            else
            {
                Logger.LogWarning("No video found for FileNodeId {FileNodeId}", fileNodeId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to auto-open video for FileNodeId {FileNodeId}", fileNodeId);
        }
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

    // ── Library Settings Methods ─────────────────────────────

    private async Task LoadLibraryPathAsync()
    {
        if (_caller is null) return;
        try
        {
            _librarySources = (await MediaLibrarySourceSettings.LoadSourcesAsync(UserSettingsService, _caller.UserId, "video")).ToList();
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
                "video",
                _librarySources,
                "Video library scan sources");

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
        StateHasChanged();
        try
        {
            _scanResult = await MediaLibraryScanner.ScanSourcesAsync(_librarySources, _caller.UserId, "Video");
            _settingsSuccess = $"Scan complete: {_scanResult.Imported} imported, {_scanResult.Skipped} already up to date.";

            // Fire-and-forget background enrichment
            _ = QueuePostScanEnrichmentAsync(DateTimeOffset.UtcNow);
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
            await VideoIndexingCallback.ResetCollectionAsync();
            _settingsSuccess = "Video collection reset. Click Scan Now to rebuild your library.";
            _showResetConfirm = false;

            // Clear displayed data
            _videos.Clear();
            _recentVideos.Clear();
            _favoriteVideos.Clear();
            _collectionVideos.Clear();
            _continueWatching.Clear();
            _collections.Clear();
            _searchResults = null;
            _selectedCollection = null;
            _selectedCollectionId = null;
            _playerOpen = false;
            _playerVideo = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reset video collection");
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
    //  TMDB Enrichment
    // ────────────────────────────────────────────────────────

    private async Task EnrichVideoAsync(Guid videoId)
    {
        if (_caller is null) return;
        _enrichingVideo = true;
        _enrichingVideoId = videoId;
        _enrichmentToast = null;
        StateHasChanged();
        try
        {
            await EnrichmentService.EnrichVideoAsync(videoId, _caller, force: false);
            var updated = await VideoService.GetVideoAsync(videoId, _caller);
            if (updated is not null)
            {
                if (_playerVideo?.Id == videoId)
                    _playerVideo = updated;
                _enrichmentToast = updated.HasExternalPoster
                    ? "Movie poster fetched from TMDB!"
                    : "No poster found on TMDB.";
                ReplaceInList(_videos, updated);
                ReplaceInList(_recentVideos, updated);
                ReplaceInList(_favoriteVideos, updated);
                ReplaceInList(_collectionVideos, updated);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error enriching video {VideoId}", videoId);
            _enrichmentToast = "Failed to fetch movie poster.";
        }
        finally
        {
            _enrichingVideo = false;
            _enrichingVideoId = null;
            StateHasChanged();
        }
    }

    private async Task EnrichLibraryAsync()
    {
        if (_caller is null) return;
        _settingsEnriching = true;
        _enrichmentToast = null;
        StateHasChanged();
        try
        {
            var queued = await EnrichmentBackgroundQueue.EnqueueAsync(new VideoEnrichmentJob
            {
                OwnerId = _caller.UserId,
                FetchPosters = true,
                FetchMetadata = true,
                StartedAtUtc = DateTimeOffset.UtcNow
            });
            _enrichmentToast = queued
                ? "Library enrichment queued. It will run in the background."
                : "An enrichment job is already running for your library.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error queueing library enrichment");
            _enrichmentToast = "Failed to queue enrichment.";
        }
        finally
        {
            _settingsEnriching = false;
            StateHasChanged();
        }
    }

    private async Task<bool> QueuePostScanEnrichmentAsync(DateTimeOffset scanStartedAt)
    {
        if (_caller is null || _scanResult is null)
            return false;

        if (!_autoFetchMetadata && !_autoFetchPosters)
            return false;

        try
        {
            return await EnrichmentBackgroundQueue.EnqueueAsync(new VideoEnrichmentJob
            {
                OwnerId = _caller.UserId,
                FetchPosters = _autoFetchPosters,
                FetchMetadata = _autoFetchMetadata,
                StartedAtUtc = scanStartedAt,
                TotalFiles = _scanResult.TotalFound,
                VideosAdded = _scanResult.Imported,
                VideosSkipped = _scanResult.Skipped,
                VideosFailed = _scanResult.Failed,
                VideosRemoved = _scanResult.Removed
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to queue post-scan video enrichment");
            return false;
        }
    }

    private async Task LoadEnrichmentSettingsAsync()
    {
        if (_caller is null) return;
        try
        {
            var posterSetting = await UserSettingsService.GetSettingAsync(
                _caller.UserId, "media-library", "video-auto-fetch-posters");
            if (posterSetting?.Value is not null)
                _autoFetchPosters = bool.TryParse(posterSetting.Value, out var v) && v;
            var metadataSetting = await UserSettingsService.GetSettingAsync(
                _caller.UserId, "media-library", "video-auto-fetch-metadata");
            if (metadataSetting?.Value is not null)
                _autoFetchMetadata = bool.TryParse(metadataSetting.Value, out var v2) && v2;
        }
        catch { /* ignore load failures */ }
    }

    private async Task SaveEnrichmentSettingsAsync()
    {
        if (_caller is null) return;
        try
        {
            await UserSettingsService.UpsertSettingAsync(_caller.UserId, "media-library",
                "video-auto-fetch-posters",
                new UpsertUserSettingDto { Value = _autoFetchPosters.ToString(), Description = "Auto-fetch movie posters from TMDB during video library scan" });
            await UserSettingsService.UpsertSettingAsync(_caller.UserId, "media-library",
                "video-auto-fetch-metadata",
                new UpsertUserSettingDto { Value = _autoFetchMetadata.ToString(), Description = "Auto-fetch movie metadata from TMDB during video library scan" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving video enrichment settings");
        }
    }

    private async Task OnAutoFetchPostersChanged(ChangeEventArgs e)
    {
        _autoFetchPosters = e.Value is bool b ? b : false;
        await SaveEnrichmentSettingsAsync();
    }

    private async Task OnAutoFetchMetadataChanged(ChangeEventArgs e)
    {
        _autoFetchMetadata = e.Value is bool b ? b : false;
        await SaveEnrichmentSettingsAsync();
    }

    private static void ReplaceInList(List<VideoDto> list, VideoDto updated)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == updated.Id)
            {
                list[i] = updated;
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopProgressTimer();
        _dotNetRef?.Dispose();
        if (_idleAutoHideHandle is not null)
        {
            try { await _idleAutoHideHandle.InvokeVoidAsync("dispose"); await _idleAutoHideHandle.DisposeAsync(); } catch { /* circuit may be gone */ }
        }
        if (_keyboardShortcutsHandle is not null)
        {
            try { await _keyboardShortcutsHandle.InvokeVoidAsync("dispose"); await _keyboardShortcutsHandle.DisposeAsync(); } catch { /* circuit may be gone */ }
        }
        if (_jsModule is not null)
        {
            try { await _jsModule.DisposeAsync(); } catch { /* circuit may be gone */ }
        }
    }
}
