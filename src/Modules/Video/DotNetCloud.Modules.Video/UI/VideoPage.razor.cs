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
    private VideoDto? _playerVideo;
    private VideoMetadataDto? _playerMetadata;
    private List<SubtitleDto> _playerSubtitles = [];
    private string? _codecErrorMessage;
    private string? _codecErrorGuidance;
    private bool _noAudioDetected;
    private DotNetObjectReference<VideoPage>? _dotNetRef;
    private bool _videoErrorListenerAttached;
    private bool _scriptsLoaded;
    private string? _streamStrategy; // "direct", "remux", or "transcode"

    // ── Transcode seek bar ──
    private double _seekBarPosition;
    private bool _seekInProgress;

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

    /// <summary>
    /// Toggles fullscreen mode for the video player container via JS Fullscreen API.
    /// </summary>
    private async Task ToggleFullscreenAsync()
    {
        if (_playerVideo is null)
            return;
        await Js.InvokeVoidAsync("DotNetCloudVideo.toggleFullscreen", "player-container");
    }

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
                _dotNetRef ??= DotNetObjectReference.Create(this);

                // Load scripts via JS interop (script tags in Blazor components don't execute).
                // Dynamic script tags load in parallel, so chain hls.js → video-player.js via onload.
                if (!_scriptsLoaded)
                {
                    // Load hls.js normally (not affected by the static assets bug)
                    await Js.InvokeVoidAsync("eval",
                        "(function(){var e=document.getElementById('dnc-hls');if(e)e.remove();e=document.getElementById('dnc-vp');if(e)e.remove();var s=document.createElement('script');s.src='/_content/DotNetCloud.Modules.Video/hls.min.js';s.id='dnc-hls';s.onload=function(){var s2=document.createElement('script');s2.src='/api/v1/videos/video-player-js?_='+Date.now();s2.id='dnc-vp';document.head.appendChild(s2);};document.head.appendChild(s);})();");
                    await Task.Delay(500);
                    _scriptsLoaded = true;
                }

                // Start the stream pipeline first (triggers chunk reconstruction on server),
                // then show progress overlay and attach the player once ready.
                var streamUrl = GetStreamUrl(_playerVideo!.Id);

                // attachHlsPlayer with 5 args: elementId, streamUrl, videoId, dotNetRef, fullDuration
                await Js.InvokeVoidAsync("DotNetCloudVideo.attachHlsPlayer",
                    "video-player", streamUrl, _playerVideo!.Id.ToString(), _dotNetRef,
                    _playerVideo.Duration.TotalSeconds);

                await Js.InvokeVoidAsync("DotNetCloudVideo.attachVideoErrorListener", "video-player", _dotNetRef);
                await Js.InvokeVoidAsync("DotNetCloudVideo.attachIdleAutoHide", "player-container", 3000);
                await Js.InvokeVoidAsync("DotNetCloudVideo.attachKeyboardShortcuts", "video-player");

                // Attach watch progress tracking (reports position every ~60s while playing)
                await Js.InvokeVoidAsync("DotNetCloudVideo.attachProgressTracking",
                    "video-player", _playerVideo!.Id.ToString());

                // Resume playback at previous position if available
                if (_playerVideo!.WatchPositionTicks.HasValue && _playerVideo.WatchPositionTicks.Value > 0)
                {
                    var resumeSeconds = TimeSpan.FromTicks(_playerVideo.WatchPositionTicks.Value).TotalSeconds;
                    await Js.InvokeVoidAsync("DotNetCloudVideo.setInitialPosition",
                        "video-player", resumeSeconds);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize video player");
            }
        }
    }

    /// <summary>Called from JS when the stream strategy is determined (via X-Stream-Strategy header).</summary>
    [JSInvokable]
    public void OnStreamStrategy(string strategy)
    {
        _streamStrategy = strategy;
        _seekBarPosition = 0;
        // StateHasChanged needed here so the seek bar DOM renders (gated on _streamStrategy).
        // The video element src is unchanged, so Blazor's diff should leave it alone.
        InvokeAsync(StateHasChanged);

        // Initialize the custom seek slider for non-direct strategies.
        // Fire-and-forget: initSeekSlider retries for up to 3s waiting for
        // the Blazor-rendered DOM elements to appear.
        if (!string.Equals(strategy, "direct", StringComparison.OrdinalIgnoreCase)
            && _playerVideo is not null)
        {
            _ = InvokeAsync(async () =>
            {
                await Js.InvokeVoidAsync("DotNetCloudVideo.initSeekSlider",
                    _dotNetRef,
                    _playerVideo.Id.ToString(),
                    _playerVideo.Duration.TotalSeconds);
            });
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
        _codecErrorMessage = diagnostic;
        _codecErrorGuidance = BuildCodecGuidance();
        InvokeAsync(StateHasChanged);
    }

    private string BuildCodecGuidance()
    {
        var container = _playerMetadata?.ContainerFormat?.ToLowerInvariant();
        var videoCodec = _playerMetadata?.VideoCodec?.ToLowerInvariant();
        var audioCodec = _playerMetadata?.AudioCodec?.ToLowerInvariant();

        // MKV container — Edge doesn't support it at all
        if (container == "mkv" || container == "matroska" || container == "webm")
        {
            if (container == "webm" && videoCodec is "vp8" or "vp9" or "av1")
                return "This video uses a WebM container with a supported codec. This format should play in most modern browsers, including Edge.";

            if (container == "mkv" || container == "matroska")
            {
                return "This video uses an <strong>MKV (Matroska)</strong> container, which Microsoft Edge does not support natively — even with HEVC extensions installed. " +
                       "To play this video, use <strong>Google Chrome</strong> or <strong>Mozilla Firefox</strong>, both of which support MKV playback. " +
                       "Alternatively, you can remux the file to MP4 using ffmpeg: <code>ffmpeg -i input.mkv -c copy output.mp4</code>.";
            }
        }

        // Known problematic codecs in MP4 — HEVC needs Microsoft's extension
        if (videoCodec is "hevc" or "h265" && (container == "mp4" || container is null))
        {
            return "This video uses <strong>H.265 / HEVC</strong> video. Install the free " +
                   "<a href=\"ms-windows-store://pdp/?productid=9n4wgh0z6vhq\" target=\"_blank\" rel=\"noopener\">HEVC Video Extensions</a> " +
                   "from the Microsoft Store, then refresh the page.";
        }

        if (videoCodec is "av1" && (container == "mp4" || container is null))
        {
            return "This video uses the <strong>AV1</strong> video codec. Install the " +
                   "<a href=\"ms-windows-store://pdp/?productid=9mvzqvxjbq9v\" target=\"_blank\" rel=\"noopener\">AV1 Video Extension</a> " +
                   "from the Microsoft Store, then refresh the page.";
        }

        if (videoCodec is "mpeg2video" or "mpeg1video")
        {
            return "This video uses <strong>MPEG-2</strong> video, which is not supported for playback in any modern browser. " +
                   "Re-encode the file to H.264 using ffmpeg: <code>ffmpeg -i input -c:v libx264 -c:a aac output.mp4</code>.";
        }

        if (container == "avi" || videoCodec is "msmpeg4v3" or "wmv3" or "wmv2" or "wmv1")
        {
            return "This video uses a legacy Windows Media or AVI format. These formats have limited browser support. " +
                   "Try a different browser, or re-encode to H.264 MP4.";
        }

        // Audio-only issues
        if (audioCodec is "ac3" or "eac3" or "dts" or "truehd")
        {
            return "This video has a <strong>" + (audioCodec ?? "surround") + "</strong> audio track that most browsers cannot decode. " +
                   "For full audio support, use <strong>Microsoft Edge</strong> or re-encode the audio to AAC.";
        }

        // Generic guidance when we have metadata but no known bad combo
        if (_playerMetadata is not null)
        {
            return "The file uses a <strong>" + (container ?? "unknown") + "</strong> container with <strong>" +
                   (videoCodec ?? "unknown") + "</strong> video. This combination may not be supported by your browser. " +
                   "Try <strong>Google Chrome</strong> or <strong>Mozilla Firefox</strong> for the widest format compatibility.";
        }

        // No metadata — generic guidance
        return "Your browser was unable to play this video. This can happen if the video uses an unsupported codec or if the file failed to load.";
    }

    private void DismissCodecError()
    {
        _codecErrorMessage = null;
        _codecErrorGuidance = null;
    }

    /// <summary>Called from JS when the video plays but no audio track is decoded (e.g. Firefox + Dolby Digital).</summary>
    [JSInvokable]
    public void OnNoAudio()
    {
        _noAudioDetected = true;
        InvokeAsync(StateHasChanged);
    }

    // ────────────────────────────────────────────────────────
    //  Transcode Seek Bar
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on every input event while the user drags the transcode seek slider.
    /// Updates the displayed position without triggering a seek-transcode.
    /// </summary>
    private void OnSeekBarInput(ChangeEventArgs e)
    {
        if (e.Value is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pos))
        {
            _seekBarPosition = pos;
        }
    }

    /// <summary>
    /// Called when the user releases the transcode seek slider (onchange).
    /// Triggers the seek-transcode flow if the target is beyond buffered range.
    /// </summary>
    private async Task OnSeekBarChanged(ChangeEventArgs e)
    {
        if (_seekInProgress)
            return;
        if (e.Value is not string s || !double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var targetSeconds))
            return;

        _seekBarPosition = targetSeconds;
        _seekInProgress = true;
        StateHasChanged();

        try
        {
            await Js.InvokeVoidAsync("DotNetCloudVideo.seekTranscode",
                "video-player",
                targetSeconds,
                _playerVideo!.Id.ToString(),
                _dotNetRef);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Seek-transcode failed for video {VideoId}", _playerVideo?.Id);
        }
        finally
        {
            _seekInProgress = false;
        }
    }

    /// <summary>
    /// Called from JS when the transcode seek completes and the stream is ready.
    /// Updates the seek bar position to match the new playback position.
    /// </summary>
    [JSInvokable]
    public void OnTranscodeSeekComplete(double positionSeconds)
    {
        _seekBarPosition = positionSeconds;
        // NOTE: Do NOT call StateHasChanged() here.
        // _seekBarPosition is not rendered in Razor — the JS manages the
        // slider fill/thumb position directly. A Blazor re-render would
        // re-apply the <video> src attribute and restart playback from 0.
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
            // Tear down previous player state (without JS interop — old <video>
            // element is removed from DOM by Blazor re-render, JS GC handles the rest)
            _codecErrorMessage = null;
            _codecErrorGuidance = null;
            _noAudioDetected = false;
            _videoErrorListenerAttached = false;
            _scriptsLoaded = false; // force fresh script load with DOM-settle delay
            _streamStrategy = null;
            _seekBarPosition = 0;
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
        var closingVideoId = _playerVideo?.Id;
        _playerOpen = false;
        _playerVideo = null;
        _playerMetadata = null;
        _playerSubtitles.Clear();
        _codecErrorMessage = null;
        _codecErrorGuidance = null;
        _noAudioDetected = false;
        _videoErrorListenerAttached = false;
        _playerSeriesContext = null;
        _streamStrategy = null;
        _seekBarPosition = 0;
        _seekInProgress = false;
        try
        {
            await Js.InvokeVoidAsync("DotNetCloudVideo.disposeProgressTracking", "video-player");
            await Js.InvokeVoidAsync("DotNetCloudVideo.cancelStreamProgress", closingVideoId?.ToString());
            await Js.InvokeVoidAsync("DotNetCloudVideo.destroyHlsPlayer", "video-player");
            await Js.InvokeVoidAsync("DotNetCloudVideo.disposeIdleAutoHide", "player-container");
            await Js.InvokeVoidAsync("DotNetCloudVideo.disposeKeyboardShortcuts", "video-player");

            // Remove cached script tags so next open loads fresh versions
            await Js.InvokeVoidAsync("eval",
                "(function(){var e=document.getElementById('dnc-hls');if(e)e.remove();e=document.getElementById('dnc-vp');if(e)e.remove();})();");
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error tearing down video player JS");
        }

        _scriptsLoaded = false;

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

    private static string GetSubtitleUrl(Guid subtitleId) => $"/api/v1/videos/subtitles/{subtitleId}";

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
        await Js.InvokeVoidAsync("DotNetCloudVideo.pauseIfPlaying", "video-player");

        // Trigger the download with the original filename so the saved file
        // has a meaningful name, not just the video ID.
        await Js.InvokeVoidAsync(
            "DotNetCloudVideo.triggerDownload",
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

        try
        {
            await Js.InvokeVoidAsync("DotNetCloudVideo.disposeProgressTracking", "video-player");
            await Js.InvokeVoidAsync("DotNetCloudVideo.cancelStreamProgress", _playerVideo?.Id.ToString());
            await Js.InvokeVoidAsync("DotNetCloudVideo.destroyHlsPlayer", "video-player");
            await Js.InvokeVoidAsync("DotNetCloudVideo.disposeIdleAutoHide", "player-container");
            await Js.InvokeVoidAsync("DotNetCloudVideo.disposeKeyboardShortcuts", "video-player");
        }
        catch { /* circuit may be gone */ }

        _pageLoadSemaphore.Dispose();
    }
}
