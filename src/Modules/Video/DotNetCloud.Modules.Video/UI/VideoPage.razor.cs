using System.Net.Http.Json;
using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Video.UI;

/// <summary>
/// Code-behind for the Video module Blazor page.
/// </summary>
public partial class VideoPage : IAsyncDisposable
{
    // ── Section / State ──
    private enum Section { Home, Library, Collections, Series, Favorites, Settings }

    private Section _section = Section.Home;
    private bool _sidebarCollapsed;
    private bool _loading;
    private string? _errorMessage;
    private string _searchQuery = string.Empty;

    // ── Data ──
    private VideoLibraryContentDto? _libraryContent;
    private List<VideoDto> _recentVideos = [];
    private List<VideoDto> _favoriteVideos = [];
    private VideoCollectionContentDto? _collectionContent;
    private VideoSearchResultDto? _searchResults;
    private List<VideoCollectionDto> _collections = [];

    // ── Series / Seasons ──
    private List<VideoSeriesDto> _seriesList = [];
    private VideoSeriesDto? _selectedSeries;
    private List<VideoSeasonDto> _seriesSeasons = [];
    private VideoSeasonDto? _selectedSeason;
    private List<VideoEpisodeDto> _seasonEpisodes = [];
    private List<VideoSeriesItemDto> _seriesVideos = [];

    // ── Paging ──
    private int _videoPage;
    private const int VideoPageSize = 50;
    private int _totalVideos;
    private bool _hasMoreVideos;

    private int _recentPage;
    private const int RecentPageSize = 12;
    private int _totalRecentVideos;
    private bool _hasMoreRecent;

    private int _seriesPage;
    private const int SeriesPageSize = 24;
    private int _totalSeries;
    private bool _hasMoreSeries;

    private int _collectionVideoPage;
    private const int CollectionVideoPageSize = 50;
    private int _totalCollectionVideos;
    private bool _hasMoreCollectionVideos;

    // ── First/Last item names for paging display ──
    private string FirstRecentVideoTitle => _recentVideos.Count > 0 ? _recentVideos[0].Title : string.Empty;
    private string LastRecentVideoTitle => _recentVideos.Count > 0 ? _recentVideos[^1].Title : string.Empty;
    private string FirstVideoTitle => _libraryContent?.StandaloneVideos.Count > 0 ? _libraryContent.StandaloneVideos[0].Title : string.Empty;
    private string LastVideoTitle => _libraryContent?.StandaloneVideos.Count > 0 ? _libraryContent.StandaloneVideos[^1].Title : string.Empty;
    private string FirstCollectionVideoTitle => _collectionContent?.StandaloneVideos.Count > 0 ? _collectionContent.StandaloneVideos[0].Title : string.Empty;
    private string LastCollectionVideoTitle => _collectionContent?.StandaloneVideos.Count > 0 ? _collectionContent.StandaloneVideos[^1].Title : string.Empty;
    private string FirstSeriesName => _seriesList.Count > 0 ? _seriesList[0].Name : string.Empty;
    private string LastSeriesName => _seriesList.Count > 0 ? _seriesList[^1].Name : string.Empty;


    // ── Selection ──
    private VideoCollectionDto? _selectedCollection;
    private Guid? _selectedCollectionId;

    // ── Player state ──
    private bool _playerOpen;
    private bool _videoPlayerInitialized;
    private VideoDto? _playerVideo;
    private VideoMetadataDto? _playerMetadata;
    private List<SubtitleDto> _playerSubtitles = [];
    private IReadOnlyList<VideoAudioStreamDto> _playerAudioStreams = [];
    private int _playerDefaultAudioIndex;
    private DotNetObjectReference<VideoPage>? _dotNetRef;
    private string? _streamStrategy; // "direct", "remux", or "transcode" (badge display only)

    private readonly SemaphoreSlim _pageLoadSemaphore = new(1, 1);

    // ── Library paging spinner ──
    private bool _libraryPaging;

    // ── Cached series list (avoids expensive ListSeriesAsync on every page turn) ──
    private List<VideoSeriesDto>? _librarySeriesCache;

    // ── Dialogs ──
    private bool _showCreateCollectionDialog;
    private bool _showEditCollectionDialog;
    private bool _showAddToCollection;
    private Guid? _editCollectionId;
    private string _collectionName = string.Empty;
    private string _collectionDescription = string.Empty;

    // ── Series context for player ──
    private sealed record PlayerSeriesContext(
        VideoSeriesDto? Series,
        VideoSeasonDto? Season,
        int? EpisodeNumber,
        int? SortOrder);
    private PlayerSeriesContext? _playerSeriesContext;

    // ── Breadcrumbs ──
    private record BreadcrumbItem(string Label, Func<Task> Action);
    private List<BreadcrumbItem> _breadcrumb = [];

    // ── Auth ──
    private CallerContext? _caller;

    // ── Library Settings ──
    private List<MediaLibrarySource> _librarySources = [];
    private bool _settingsSaving;
    private bool _settingsScanning;
    private string? _settingsError;
    private string? _settingsSuccess;
    private MediaScanResult? _scanResult;

    // Scan cancellation
    private CancellationTokenSource? _scanCts;

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

    // Manual metadata edit state
    private bool _showMetadataEditDialog;

    // Post-scan enrichment status
    private LibraryScanProgress? _lastEnrichmentResult;

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
        if (_playerOpen && !_videoPlayerInitialized)
        {
            _videoPlayerInitialized = true;
            try
            {
                _dotNetRef ??= DotNetObjectReference.Create(this);

                // Load hls.js → video-player.js (promise-chained onload), then init the player.
                await LoadPlayerScriptsAsync();

                await Js.InvokeVoidAsync("DotNetCloudVideoPlayer.init", BuildPlayerConfig());
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize video player");
                _videoPlayerInitialized = false;
            }
        }
    }

    /// <summary>
    /// Loads hls.min.js then video-player.js via promise-chained onload handlers.
    /// Script tags inside Blazor components don't execute; hls.js is loaded from the
    /// module static asset path, and video-player.js is served via the
    /// /api/v1/videos/video-player-js endpoint to work around the .NET 10
    /// static-web-assets bug. The eval returns a Promise that Blazor awaits.
    /// </summary>
    private async Task LoadPlayerScriptsAsync()
    {
        // Timestamp cache-buster (Date.now) so a freshly deployed video-player.js is
        // never served stale from the browser cache.
        await Js.InvokeVoidAsync("eval",
            "(function(){return new Promise(function(res){var h=document.createElement('script');h.src='/_content/DotNetCloud.Modules.Video/hls.min.js?v=1';h.onload=function(){var p=document.createElement('script');p.src='/api/v1/videos/video-player-js?_='+Date.now();p.onload=res;p.onerror=res;document.head.appendChild(p);};h.onerror=res;document.head.appendChild(h);});})()");
    }

    /// <summary>Called from JS when the stream strategy is determined. Drives the badge display only.</summary>
    [JSInvokable]
    public void OnStrategy(string strategy)
    {
        _streamStrategy = strategy;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called from JS when playback fails fatally. The JS player shows its own error overlay;
    /// this logs the diagnostic (with ffprobe metadata when available) server-side.</summary>
    [JSInvokable]
    public void OnError(int code, string message)
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

        // Append codec/container info from ffprobe metadata if available
        if (_playerMetadata is not null)
        {
            if (!string.IsNullOrWhiteSpace(_playerMetadata.ContainerFormat))
                diagnostic += $" | Container: {_playerMetadata.ContainerFormat}";
            if (!string.IsNullOrWhiteSpace(_playerMetadata.VideoCodec))
                diagnostic += $" | Video: {_playerMetadata.VideoCodec}";
            if (!string.IsNullOrWhiteSpace(_playerMetadata.AudioCodec))
                diagnostic += $" | Audio: {_playerMetadata.AudioCodec}";
        }

        Logger.LogWarning("Video playback error: {Diagnostic}", diagnostic);
    }

    /// <summary>Called from JS when playback ends naturally (for auto-advance to the next episode).</summary>
    [JSInvokable]
    public void OnEnded()
    {
        _ = AutoAdvanceEpisodeAsync();
    }

    /// <summary>Called from JS when the user clicks the prev (-1) / next (+1) episode button.</summary>
    [JSInvokable]
    public async Task OnNavigateEpisode(int delta)
    {
        await NavigateEpisodeAsync(delta);
    }

    /// <summary>
    /// Navigates to the next/previous episode in the current TV season or movie franchise.
    /// Stops at the boundaries of the season/franchise (returns without action).
    /// </summary>
    private async Task NavigateEpisodeAsync(int delta)
    {
        if (_playerSeriesContext is null || _playerVideo is null)
            return;

        // TV series with seasons
        if (_playerSeriesContext.Season is not null)
        {
            var episodes = _seasonEpisodes; // in order
            var idx = episodes.FindIndex(e => e.EpisodeNumber == _playerSeriesContext.EpisodeNumber);
            var next = ComputeNextEpisodeIndex(episodes.Count, idx, delta);
            if (next is not null)
            {
                _playerSeriesContext = new PlayerSeriesContext(
                    _playerSeriesContext.Series,
                    _playerSeriesContext.Season,
                    episodes[next.Value].EpisodeNumber,
                    null);
                await OpenEpisodeVideoAsync(episodes[next.Value]);
            }
            return;
        }

        // Movie franchise (no seasons)
        var items = _seriesVideos; // in order
        var i = items.FindIndex(x => x.SortOrder == _playerSeriesContext.SortOrder);
        var n = ComputeNextEpisodeIndex(items.Count, i, delta);
        if (n is not null)
        {
            _playerSeriesContext = new PlayerSeriesContext(
                _playerSeriesContext.Series,
                null,
                null,
                items[n.Value].SortOrder);
            await OpenSeriesVideoAsync(items[n.Value]);
        }
    }

    private async Task AutoAdvanceEpisodeAsync() => await NavigateEpisodeAsync(1);

    /// <summary>
    /// Pure helper computing the index reached by stepping <paramref name="delta"/> from
    /// <paramref name="currentIndex"/> within a list of <paramref name="count"/> items.
    /// Returns null when stepping past either bound. Extracted so it can be unit-tested.
    /// </summary>
    internal static int? ComputeNextEpisodeIndex(int count, int currentIndex, int delta)
    {
        if (count <= 0 || currentIndex < 0 || currentIndex >= count)
            return null;
        var next = currentIndex + delta;
        if (next < 0 || next >= count)
            return null;
        return next;
    }

    /// <summary>
    /// Builds the config object passed to the JS player's <c>init</c>. The JS player
    /// owns all player DOM; Blazor only supplies the metadata it needs.
    /// </summary>
    private object BuildPlayerConfig()
    {
        var video = _playerVideo!;
        var nav = GetPlayerNavigationState();
        return new
        {
            containerId = "video-player-root",
            videoId = video.Id,
            title = video.Title,
            posterUrl = video.HasExternalPoster ? GetThumbnailUrl(video.Id) : null,
            streamUrl = GetStreamUrl(video.Id),
            durationSeconds = video.Duration.TotalSeconds,
            resumeSeconds = video.WatchPositionTicks.HasValue && video.WatchPositionTicks.Value > 0
                ? TimeSpan.FromTicks(video.WatchPositionTicks.Value).TotalSeconds
                : 0,
            subtitles = _playerSubtitles.Select(s => new
            {
                id = s.Id,
                language = s.Language,
                label = s.Label ?? s.Language,
                isDefault = s.IsDefault
            }),
            audioStreams = _playerAudioStreams.Select(a => new
            {
                a.Index,
                a.Codec,
                a.Language,
                a.Title,
                a.Channels,
                a.IsDefault
            }),
            defaultAudioIndex = _playerDefaultAudioIndex,
            hasPrevious = nav.HasPrevious,
            hasNext = nav.HasNext,
            dotNetRef = _dotNetRef
        };
    }

    /// <summary>
    /// Determines whether prev/next episode navigation is available for the current
    /// player context (within the season or franchise bounds).
    /// </summary>
    private (bool HasPrevious, bool HasNext) GetPlayerNavigationState()
    {
        if (_playerSeriesContext is null || _playerVideo is null)
            return (false, false);

        if (_playerSeriesContext.Season is not null)
        {
            var idx = _seasonEpisodes.FindIndex(e => e.EpisodeNumber == _playerSeriesContext.EpisodeNumber);
            return (
                ComputeNextEpisodeIndex(_seasonEpisodes.Count, idx, -1) is not null,
                ComputeNextEpisodeIndex(_seasonEpisodes.Count, idx, 1) is not null);
        }

        if (_playerSeriesContext.SortOrder.HasValue)
        {
            var i = _seriesVideos.FindIndex(x => x.SortOrder == _playerSeriesContext.SortOrder);
            return (
                ComputeNextEpisodeIndex(_seriesVideos.Count, i, -1) is not null,
                ComputeNextEpisodeIndex(_seriesVideos.Count, i, 1) is not null);
        }

        return (false, false);
    }

    /// <summary>
    /// Fetches the audio streams for a video via GET /api/v1/videos/{id}/streams.
    /// Returns an empty list when the call fails (the player then has no audio menu).
    /// </summary>
    private async Task<IReadOnlyList<VideoAudioStreamDto>> GetAudioStreamsAsync(Guid videoId, CallerContext caller)
    {
        try
        {
            using var response = await Http.GetAsync($"/api/v1/videos/{videoId}/streams");
            if (!response.IsSuccessStatusCode)
                return [];

            var envelope = await response.Content.ReadFromJsonAsync<StreamsEnvelope>();
            return envelope?.Data?.AudioStreams ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load audio streams for video {VideoId}", videoId);
            return [];
        }
    }

    private sealed record StreamsEnvelope
    {
        public StreamsData? Data { get; init; }
    }

    private sealed record StreamsData
    {
        public Guid VideoId { get; init; }

        public IReadOnlyList<VideoAudioStreamDto>? AudioStreams { get; init; }
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

            ScanProgress.OnProgressChanged += OnScanProgressChanged;

            // Initialize TMDB availability from database settings (set via admin pages)
            await EnrichmentService.InitializeAsync();
            _tmdbAvailable = EnrichmentService.IsTmdbAvailable;

            // Deep-link: auto-open from Files module if fileId parameter was supplied on first load
            if (!string.IsNullOrEmpty(FileId) && Guid.TryParse(FileId, out var fileId))
            {
                _lastHandledNav = FileIdNav;
                await TryAutoPlayFileAsync(fileId, _caller);
            }

            // Deep-link: auto-open from search results — read videoId directly from query string
            Logger.LogInformation("VideoPage deep-link check: Uri={Uri}, FileId={FileId}, VideoId param unused (reading from query)",
                Navigation.Uri, FileId);
            await TryOpenVideoFromQueryAsync(_caller);

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

    // ── Scan Progress ──

    private bool _isScanActive => _caller is not null && ScanProgress.IsScanning(_caller.UserId);

    private LibraryScanProgress? _currentScanProgress => _caller is null ? null : ScanProgress.GetCurrentProgress(_caller.UserId);

    private void OnScanProgressChanged()
    {
        // Capture the final enrichment result when enrichment completes
        var progress = _currentScanProgress;
        if (progress is not null &&
            string.Equals(progress.Phase, "Enrichment complete", StringComparison.OrdinalIgnoreCase))
        {
            _lastEnrichmentResult = progress;
        }
        InvokeAsync(StateHasChanged);
    }

    private static string TruncateFileName(string fileName, int maxLength)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Length <= maxLength)
            return fileName ?? string.Empty;

        var half = (maxLength - 3) / 2;
        return $"{fileName[..half]}...{fileName[^half..]}";
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_caller is null)
            return;

        // Handle fileId changes when already on the page (same-page navigation via Files module).
        // FileIdNav is a timestamp nonce that changes on every click, even for the same file.
        if (!string.IsNullOrEmpty(FileId) && FileIdNav != _lastHandledNav && Guid.TryParse(FileId, out var fileId))
        {
            _lastHandledNav = FileIdNav;
            await TryAutoPlayFileAsync(fileId, _caller);
        }

        // Handle videoId changes when already on the page (same-page navigation via search results).
        // Read directly from query string to bypass ModulePageHost/DynamicComponent parameter chain.
        await TryOpenVideoFromQueryAsync(_caller);
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
        // Properly tear down player if open (cleans up JS, resets scriptsLoaded, etc.)
        if (_playerOpen)
            await ClosePlayer();

        _section = section;
        _selectedCollection = null;
        _selectedCollectionId = null;
        _selectedSeries = null;
        _selectedSeason = null;
        _seriesSeasons.Clear();
        _seasonEpisodes.Clear();
        _seriesVideos.Clear();
        _searchResults = null;
        _libraryContent = null;
        _collectionContent = null;
        _searchQuery = string.Empty;
        _playerOpen = false;
        _playerSeriesContext = null;
        _videoPage = 0;
        _recentPage = 0;
        _seriesPage = 0;
        _collectionVideoPage = 0;
        _librarySeriesCache = null; // series may have changed after a scan
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

        if (_caller is null)
            return;

        // Note: individual page load methods (LoadVideosPageAsync, etc.) handle
        // their own semaphore acquisition — do NOT acquire it here.
        try
        {
            _loading = true;
            _errorMessage = null;
            StateHasChanged();

            switch (_section)
            {
                case Section.Home:
                    await LoadRecentPageAsync();
                    break;

                case Section.Library:
                    await LoadVideosPageAsync();
                    break;

                case Section.Series:
                    _seriesPage = 0;
                    await LoadSeriesPageAsync();
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
        if (_caller is null)
            return;

        _libraryPaging = true;
        StateHasChanged();

        // Serialize DbContext access to prevent concurrency errors on rapid page clicks.
        await _pageLoadSemaphore.WaitAsync();
        try
        {
            // Cache series between page loads — ListSeriesAsync is expensive (4-5 queries).
            _librarySeriesCache ??= (await SeriesService.ListSeriesAsync(_caller)).ToList();

            _libraryContent = await VideoService.ListLibraryContentAsync(_caller, _videoPage * VideoPageSize, VideoPageSize, _librarySeriesCache);
            _totalVideos = _libraryContent.TotalSeries + _libraryContent.TotalStandaloneVideos;
            _hasMoreVideos = (_videoPage + 1) * VideoPageSize < _totalVideos;
        }
        finally
        {
            _pageLoadSemaphore.Release();
            _libraryPaging = false;
            StateHasChanged();
        }
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
        if (!_hasMoreVideos)
            return;

        _videoPage++;
        await LoadVideosPageAsync();
    }

    private async Task LoadRecentPageAsync()
    {
        if (_caller is null)
            return;

        await _pageLoadSemaphore.WaitAsync();
        try
        {
            _totalRecentVideos = await VideoService.GetVideoCountAsync(_caller.UserId);
            var videos = (await VideoService.GetRecentVideosAsync(_caller, _recentPage * RecentPageSize, RecentPageSize)).ToList();
            _hasMoreRecent = (_recentPage + 1) * RecentPageSize < _totalRecentVideos;
            _recentVideos = videos;
        }
        finally
        {
            _pageLoadSemaphore.Release();
        }
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
        if (!_hasMoreRecent)
            return;

        _recentPage++;
        await LoadRecentPageAsync();
    }

    // ── Series Paging ──

    private async Task LoadSeriesPageAsync()
    {
        if (_caller is null)
            return;

        await _pageLoadSemaphore.WaitAsync();
        try
        {
            var allSeries = (await SeriesService.ListSeriesAsync(_caller)).ToList();
            _totalSeries = allSeries.Count;
            _seriesList = allSeries
                .Skip(_seriesPage * SeriesPageSize)
                .Take(SeriesPageSize)
                .ToList();
            _hasMoreSeries = (_seriesPage + 1) * SeriesPageSize < _totalSeries;
        }
        finally
        {
            _pageLoadSemaphore.Release();
        }
    }

    private async Task PrevSeriesPageAsync()
    {
        if (_seriesPage > 0)
        {
            _seriesPage--;
            await LoadSeriesPageAsync();
        }
    }

    private async Task NextSeriesPageAsync()
    {
        if (!_hasMoreSeries)
            return;

        _seriesPage++;
        await LoadSeriesPageAsync();
    }

    // ── Collection Videos Paging ──

    private async Task LoadCollectionVideoPageAsync()
    {
        if (_caller is null || _selectedCollection is null)
            return;

        _collectionContent = await CollectionService.GetCollectionContentAsync(_selectedCollection.Id, _caller);
        _totalCollectionVideos = _collectionContent.TotalItems;
        _hasMoreCollectionVideos = (_collectionVideoPage + 1) * CollectionVideoPageSize < _totalCollectionVideos;
    }

    private async Task PrevCollectionVideoPageAsync()
    {
        if (_collectionVideoPage > 0)
        {
            _collectionVideoPage--;
            await LoadCollectionVideoPageAsync();
        }
    }

    private async Task NextCollectionVideoPageAsync()
    {
        if (!_hasMoreCollectionVideos)
            return;

        _collectionVideoPage++;
        await LoadCollectionVideoPageAsync();
    }

    // ────────────────────────────────────────────────────────
    //  Video Detail → Player
    // ────────────────────────────────────────────────────────

    private async Task OpenVideoDetailAsync(VideoDto video)
    {
        _searchResults = null;
        try
        {
            // Tear down previous player state (the JS player is destroyed/re-initialized
            // for the new video via _videoPlayerInitialized).
            _videoPlayerInitialized = false;
            _streamStrategy = null;
            _playerAudioStreams = [];
            _playerDefaultAudioIndex = 0;
            _playerOpen = true;

            var caller = await GetCallerAsync();
            _playerVideo = video;

            // Increment view count (fire-and-forget — best-effort, don't block player)
            _ = Task.Run(async () =>
            {
                try
                { await VideoService.IncrementViewCountAsync(video.Id); }
                catch (Exception ex) { Logger.LogWarning(ex, "Failed to increment view count for {VideoId}", video.Id); }
            });

            _playerSubtitles = (await SubtitleService.GetSubtitlesAsync(video.Id, caller)).ToList();
            _playerMetadata = await MetadataService.GetMetadataAsync(video.Id);

            // Load the audio streams for the audio-track selector (best-effort).
            _playerAudioStreams = await GetAudioStreamsAsync(video.Id, caller);
            _playerDefaultAudioIndex = _playerAudioStreams.FirstOrDefault(s => s.IsDefault)?.Index ?? 0;

            _breadcrumb =
            [
                new BreadcrumbItem(GetSectionLabel(), async () => { await ClosePlayer(); StateHasChanged(); })
            ];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening video player");
        }
    }

    private async Task ClosePlayer()
    {
        _playerOpen = false;
        _playerVideo = null;
        _playerMetadata = null;
        _playerSubtitles.Clear();
        _playerAudioStreams = [];
        _playerDefaultAudioIndex = 0;
        _playerSeriesContext = null;
        _streamStrategy = null;
        _videoPlayerInitialized = false;
        try
        {
            // The JS player tears down hls.js, subtitle blobs, DOM, and listeners.
            await Js.InvokeVoidAsync("DotNetCloudVideoPlayer.destroy");
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error tearing down video player JS");
        }

        _breadcrumb.Clear();
    }

    // ────────────────────────────────────────────────────────
    //  Favorites
    // ────────────────────────────────────────────────────────

    private async Task ToggleFavoriteAsync()
    {
        if (_playerVideo is null)
            return;
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
        _collectionVideoPage = 0;

        _breadcrumb =
        [
            new BreadcrumbItem("Collections", async () => { _selectedCollection = null; _selectedCollectionId = null; _breadcrumb.Clear(); StateHasChanged(); })
        ];

        try
        {
            var caller = await GetCallerAsync();
            await LoadCollectionVideoPageAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading collection videos");
        }
        StateHasChanged();
    }

    // ────────────────────────────────────────────────────────
    //  Series / Seasons
    // ────────────────────────────────────────────────────────

    private async Task OpenSeriesDetailAsync(VideoSeriesDto series)
    {
        _searchResults = null;
        _section = Section.Series;
        _selectedSeries = series;
        _selectedSeason = null;
        _seasonEpisodes.Clear();
        _seriesVideos.Clear();

        _breadcrumb =
        [
            new BreadcrumbItem("Series", async () => { _selectedSeries = null; _selectedSeason = null; _seriesSeasons.Clear(); _seasonEpisodes.Clear(); _seriesVideos.Clear(); _breadcrumb.Clear(); StateHasChanged(); })
        ];

        try
        {
            var caller = await GetCallerAsync();
            if (series.Type == "TvSeries")
            {
                _seriesSeasons = (await SeriesService.GetSeriesSeasonsAsync(series.Id, caller)).ToList();
            }
            else
            {
                _seriesVideos = (await SeriesService.GetSeriesVideosAsync(series.Id, caller)).ToList();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading series detail for {SeriesId}", series.Id);
        }
        StateHasChanged();
    }

    private async Task OpenSeasonDetailAsync(VideoSeasonDto season)
    {
        _selectedSeason = season;
        _seasonEpisodes.Clear();

        // Breadcrumb trail: Series › {SeriesName} — the current season is shown as
        // the section title (h3), so the full trail reads Series › American Gods › Season 1.
        var series = _selectedSeries;
        _breadcrumb =
        [
            new BreadcrumbItem("Series", async () =>
            {
                _selectedSeries = null;
                _selectedSeason = null;
                _seriesSeasons.Clear();
                _seasonEpisodes.Clear();
                _seriesVideos.Clear();
                _breadcrumb.Clear();
                StateHasChanged();
            }),
            new BreadcrumbItem(series?.Name ?? "Series", async () =>
            {
                if (series is null)
                    return;
                _selectedSeason = null;
                _seasonEpisodes.Clear();
                await OpenSeriesDetailAsync(series);
            })
        ];

        try
        {
            var caller = await GetCallerAsync();
            _seasonEpisodes = (await SeriesService.GetSeasonEpisodesAsync(season.Id, caller)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading episodes for season {SeasonId}", season.Id);
        }
        StateHasChanged();
    }

    private async Task OpenSeriesVideoAsync(VideoSeriesItemDto item)
    {
        if (item.Video is not null)
        {
            // Build series context for the player
            _playerSeriesContext = new PlayerSeriesContext(
                Series: _selectedSeries,
                Season: null,
                EpisodeNumber: null,
                SortOrder: item.SortOrder);
            await OpenVideoDetailAsync(item.Video);
        }
    }

    private async Task OpenEpisodeVideoAsync(VideoEpisodeDto episode)
    {
        if (episode.Video is not null)
        {
            // Build series/season context for the player
            _playerSeriesContext = new PlayerSeriesContext(
                Series: _selectedSeries,
                Season: _selectedSeason,
                EpisodeNumber: episode.EpisodeNumber,
                SortOrder: null);
            await OpenVideoDetailAsync(episode.Video);
        }
    }

    private async Task NavigateToSeriesFromPlayer()
    {
        if (_playerSeriesContext?.Series is null)
            return;

        _playerOpen = false;
        _playerVideo = null;
        _section = Section.Series;
        _selectedSeason = null;
        _seasonEpisodes.Clear();
        _seriesVideos.Clear();
        _breadcrumb.Clear();

        // Re-fetch series data
        var caller = await GetCallerAsync();
        _selectedSeries = await SeriesService.GetSeriesAsync(_playerSeriesContext.Series.Id, caller);
        if (_selectedSeries is not null)
        {
            if (_selectedSeries.Type == "TvSeries")
            {
                _seriesSeasons = (await SeriesService.GetSeriesSeasonsAsync(_selectedSeries.Id, caller)).ToList();
            }
            else
            {
                _seriesVideos = (await SeriesService.GetSeriesVideosAsync(_selectedSeries.Id, caller)).ToList();
            }
        }
        _playerSeriesContext = null;
        StateHasChanged();
    }

    private async Task NavigateToSeasonFromPlayer()
    {
        if (_playerSeriesContext?.Season is null || _playerSeriesContext?.Series is null)
            return;

        _playerOpen = false;
        _playerVideo = null;
        _section = Section.Series;
        _breadcrumb.Clear();

        var caller = await GetCallerAsync();
        _selectedSeries = await SeriesService.GetSeriesAsync(_playerSeriesContext.Series.Id, caller);
        if (_selectedSeries is not null)
        {
            _seriesSeasons = (await SeriesService.GetSeriesSeasonsAsync(_selectedSeries.Id, caller)).ToList();
            var season = _seriesSeasons.FirstOrDefault(s => s.Id == _playerSeriesContext.Season.Id);
            if (season is not null)
            {
                await OpenSeasonDetailAsync(season);
            }
        }
        _playerSeriesContext = null;
        StateHasChanged();
    }

    private static string GetSeriesThumbnailUrl(Guid seriesId) => $"/api/v1/series/{seriesId}/thumbnail";

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
        if (_playerVideo is null)
            return;
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
    //  Manual Metadata Edit
    // ────────────────────────────────────────────────────────

    private void OpenMetadataEditDialog()
    {
        if (_playerVideo is null)
            return;
        _showMetadataEditDialog = true;
    }

    private void CloseMetadataEditDialog()
    {
        _showMetadataEditDialog = false;
    }

    private async Task OnMetadataSavedAsync(VideoDto? updated)
    {
        _showMetadataEditDialog = false;

        if (updated is null)
            return;

        // Refresh the player's displayed video and any lists that show it.
        if (_playerVideo?.Id == updated.Id)
        {
            _playerVideo = updated;
        }

        ReplaceInLibraryContent(_libraryContent, updated);
        ReplaceInList(_recentVideos, updated);
        ReplaceInList(_favoriteVideos, updated);
        ReplaceInCollectionContent(_collectionContent, updated);
        _enrichmentToast = "Metadata saved.";

        StateHasChanged();
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
                _searchResults = await VideoService.SearchAsync(caller, _searchQuery, 50);
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
        Section.Series when _selectedSeason is not null => _selectedSeason.Name ?? $"Season {_selectedSeason.SeasonNumber}",
        Section.Series when _selectedSeries is not null => _selectedSeries.Name,
        Section.Series => "Series",
        Section.Collections when _selectedCollection is not null => _selectedCollection.Name,
        Section.Collections => "Collections",
        Section.Favorites => "Favorites",
        _ => "Video"
    };

    private string GetSectionLabel() => _section switch
    {
        Section.Home => "Home",
        Section.Library => "Library",
        Section.Series => "Series",
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
        if (width is null || height is null)
            return "Unknown";
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

    /// <summary>
    /// Converts a TMDB language code to a human-readable label with a flag emoji where possible.
    /// </summary>
    private static string GetLanguageLabel(string? langCode)
    {
        return langCode?.ToLowerInvariant() switch
        {
            "en" => "English",
            "ja" => "🇯🇵 Japanese",
            "ko" => "🇰🇷 Korean",
            "zh" => "🇨🇳 Chinese",
            "fr" => "🇫🇷 French",
            "de" => "🇩🇪 German",
            "es" => "🇪🇸 Spanish",
            "it" => "🇮🇹 Italian",
            "pt" => "🇵🇹 Portuguese",
            "ru" => "🇷🇺 Russian",
            "ar" => "🇸🇦 Arabic",
            "hi" => "🇮🇳 Hindi",
            "sv" => "🇸🇪 Swedish",
            "nl" => "🇳🇱 Dutch",
            "pl" => "🇵🇱 Polish",
            "tr" => "🇹🇷 Turkish",
            "th" => "🇹🇭 Thai",
            "vi" => "🇻🇳 Vietnamese",
            _ => langCode ?? string.Empty
        };
    }

    private static string GetStreamUrl(Guid videoId) =>
        $"/api/v1/videos/{videoId}/stream";

    private static string GetDownloadUrl(Guid videoId) =>
        $"/api/v1/videos/{videoId}/download";

    private static double GetWatchPercent(VideoDto video)
    {
        if (video.WatchPositionTicks is null || video.Duration.Ticks < 1)
            return 0;
        return (double)video.WatchPositionTicks.Value / video.Duration.Ticks * 100;
    }

    /// <summary>
    /// Looks up a video by its Files-module FileNodeId and opens it in the player.
    /// Falls back to direct video ID lookup (for search result deep-links).
    /// </summary>
    private async Task TryAutoPlayFileAsync(Guid id, CallerContext caller)
    {
        try
        {
            // First try file node ID lookup (deep-link from Files module)
            var video = await VideoService.GetVideoByFileNodeIdAsync(id, caller);
            if (video is null)
            {
                // Fallback: try direct video ID lookup (deep-link from search results)
                video = await VideoService.GetVideoAsync(id, caller);
            }

            if (video is not null)
            {
                await OpenVideoDetailAsync(video);
            }
            else
            {
                Logger.LogWarning("No video found for Id {Id}", id);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to auto-open video for Id {Id}", id);
        }
    }

    /// <summary>
    /// Reads the videoId query parameter directly (bypasses the ModulePageHost/DynamicComponent chain)
    /// and opens the video if found.
    /// </summary>
    private async Task TryOpenVideoFromQueryAsync(CallerContext caller)
    {
        try
        {
            var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
            if (!QueryHelpers.ParseQuery(uri.Query).TryGetValue("videoId", out var videoIdValue))
                return;
            if (!Guid.TryParse(videoIdValue, out var videoId))
                return;

            var video = await VideoService.GetVideoAsync(videoId, caller);
            if (video is not null)
            {
                await OpenVideoDetailAsync(video);
            }
            else
            {
                Logger.LogWarning("No video found for VideoId {VideoId} from query string", videoId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to auto-open video from query string");
        }
    }

    /// <summary>
    /// Pauses playback (if playing) and triggers a download of the current video.
    /// Uses JS interop to create a temporary anchor element with the download attribute,
    /// which prompts the browser to save the file rather than navigate to it.
    /// </summary>
    private async Task DownloadVideoAsync()
    {
        if (_playerVideo is null)
            return;

        // Pause playback only if the video is currently playing
        // (no-op if already paused). This avoids competing bandwidth usage
        // between the stream and the download, while preserving the
        // playback position so the user can resume after download completes.
        await Js.InvokeVoidAsync("DotNetCloudVideoPlayer.pauseIfPlaying");

        // Trigger the download with the original filename so the saved file
        // has a meaningful name, not just the video ID.
        await Js.InvokeVoidAsync(
            "DotNetCloudVideoPlayer.triggerDownload",
            GetDownloadUrl(_playerVideo!.Id),
            _playerVideo!.FileName);
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

    // ── Library Settings Methods ─────────────────────────────

    private async Task LoadLibraryPathAsync()
    {
        if (_caller is null)
            return;
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
        if (_caller is null)
            return;
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
        if (_caller is null || _librarySources.Count == 0)
            return;
        await PersistLibrarySourcesAsync(showSuccessMessage: false);
        if (_settingsError is not null)
            return;

        _settingsScanning = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        _lastEnrichmentResult = null;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        StateHasChanged();

        var userId = _caller.UserId;
        _scanCts = ScanProgress.StartScan(userId);
        var scanCts = _scanCts;

        // Bridge MediaScanProgress → LibraryScanProgress
        var elapsedStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var progressBridge = new Progress<MediaScanProgress>(msp =>
        {
            ScanProgress.UpdateProgress(userId, new LibraryScanProgress
            {
                Phase = msp.Phase,
                CurrentFile = msp.CurrentFile,
                FilesDiscovered = msp.FilesDiscovered,
                FilesProcessed = msp.FilesProcessed,
                TotalFiles = msp.TotalFiles,
                TracksAdded = msp.Imported,
                TracksFailed = msp.Failed,
                TracksRemoved = msp.Removed,
                PercentComplete = msp.PercentComplete,
                ElapsedTime = elapsedStopwatch.Elapsed
            });
        });

        try
        {
            _scanResult = await MediaLibraryScanner.ScanSourcesAsync(_librarySources, userId, "Video", progressBridge, scanCts.Token);
            ScanProgress.CompleteScan(userId);
            _settingsSuccess = $"Scan complete: {_scanResult.Imported} imported, {_scanResult.Skipped} already up to date.";

            // Fire-and-forget background enrichment
            _ = QueuePostScanEnrichmentAsync(DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            ScanProgress.CompleteScan(userId);
            _settingsError = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ScanProgress.CompleteScan(userId);
            _settingsError = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _settingsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void StopScan()
    {
        _scanCts?.Cancel();
        if (_caller is not null)
            ScanProgress.Cancel(_caller.UserId);
    }

    private async Task ResetCollectionAsync()
    {
        if (_caller is null)
            return;
        _settingsResetting = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        StateHasChanged();
        try
        {
            await VideoIndexingCallback.ResetCollectionAsync(_caller.UserId);
            _settingsSuccess = "Video collection reset. Click Scan Now to rebuild your library.";
            _showResetConfirm = false;

            // Clear displayed data
            _libraryContent = null;
            _librarySeriesCache = null;
            _recentVideos.Clear();
            _favoriteVideos.Clear();
            _collectionContent = null;
            _collections.Clear();
            _seriesList.Clear();
            _selectedSeries = null;
            _selectedSeason = null;
            _seriesSeasons.Clear();
            _seasonEpisodes.Clear();
            _seriesVideos.Clear();
            _searchResults = null;
            _selectedCollection = null;
            _selectedCollectionId = null;
            _playerOpen = false;
            _playerVideo = null;
            _playerSeriesContext = null;
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
            if (_caller is null)
                return;
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
        if (_dirBrowserBreadcrumbs.Count == 0)
            return "/";
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
        if (_caller is null)
            return;
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
                ReplaceInLibraryContent(_libraryContent, updated);
                ReplaceInList(_recentVideos, updated);
                ReplaceInList(_favoriteVideos, updated);
                ReplaceInCollectionContent(_collectionContent, updated);
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
        if (_caller is null)
            return;
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
        if (_caller is null)
            return;
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
        if (_caller is null)
            return;
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

    private static void ReplaceInLibraryContent(VideoLibraryContentDto? content, VideoDto updated)
    {
        if (content is null)
            return;
        var list = content.StandaloneVideos.ToList();
        ReplaceInList(list, updated);
    }

    private static void ReplaceInCollectionContent(VideoCollectionContentDto? content, VideoDto updated)
    {
        if (content is null)
            return;
        var list = content.StandaloneVideos.ToList();
        ReplaceInList(list, updated);
    }

    public async ValueTask DisposeAsync()
    {
        ScanProgress.OnProgressChanged -= OnScanProgressChanged;
        _dotNetRef?.Dispose();
        _dotNetRef = null;

        try
        {
            await Js.InvokeVoidAsync("DotNetCloudVideoPlayer.destroy");
        }
        catch { /* circuit may be gone */ }

        _pageLoadSemaphore.Dispose();
    }
}
