using Android.Content;
using Android.Provider;
using AndroidX.Core.App;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using AndroidUri = global::Android.Net.Uri;
using AndroidConnectivityManager = global::Android.Net.ConnectivityManager;
using AndroidTransportType = global::Android.Net.TransportType;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Channels;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Scans the device's MediaStore (read-only) for photos and videos and uploads each new
/// item to the active DotNetCloud server using the chunked upload protocol via
/// <see cref="IFileRestClient"/>. Uploads are organised into an <c>AutoUpload/YYYY/MM</c>
/// folder hierarchy by default and tracked in a persistent <see cref="MediaUploadIndex"/>
/// so nothing is skipped or double-uploaded. Respects WiFi-only / charging-only / battery
/// preferences and requires the Android 13+ media-library read permission (see
/// <see cref="IMediaPermissionService"/>).
/// </summary>
internal sealed class MediaAutoUploadService : IMediaAutoUploadService
{
    private const string PrefEnabled = "media_upload_enabled";
    private const string PrefWifiOnly = "media_upload_wifi_only";
    private const string PrefOrganizeByDate = "media_upload_organize_by_date";
    private const string PrefUploadFolderName = "media_upload_folder_name";
    private const int NotificationId = 3001;
    private const int QuotaNotificationId = 3003;
    private const string DefaultUploadFolderName = "AutoUpload";
    private const string PrefChargingOnly = "media_upload_charging_only";
    private const string PrefBatteryThreshold = "media_upload_battery_threshold";
    private const string PrefLastSuccessTs = "media_upload_last_success";
    private const string PendingUploadsDirName = "PendingUploads";

    /// <summary>Maximum number of items uploaded in a single scan pass (keeps memory + notification churn bounded during a first-run backfill).</summary>
    private const int MaxItemsPerPass = 40;

    /// <summary>
    /// Largest single item the watcher will upload; anything bigger is skipped entirely.
    /// </summary>
    /// <remarks>
    /// Candidates are drained smallest-first and a row is only recorded in the index after a fully
    /// successful upload. A multi-gigabyte file therefore used to sit at the head of the queue and
    /// block all progress: at the sequential chunk cadence (~4 MB chunks, <c>MaxConcurrency = 1</c>
    /// to avoid HTTP 429) it took long enough that a process kill mid-upload meant the next attempt
    /// restarted from the same file and recorded nothing, so the backup never advanced. Oversized
    /// items are reported in the log and left on the device rather than silently dropped.
    /// </remarks>
    private const long MaxSingleItemBytes = 500L * 1024 * 1024;

    private readonly IServerConnectionStore _connectionStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IFileRestClient _fileApi;
    private readonly ILogger<MediaAutoUploadService> _logger;
    private readonly IAppForegroundService _foregroundService;
    private readonly IMediaPermissionService _permissionService;
    private readonly TimeSpan _foregroundScanInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _backgroundScanInterval = TimeSpan.FromMinutes(60);

    // Persistent index of already-uploaded media (single source of truth for dedup).
    private readonly MediaUploadIndex _index = new();

    // Guards scans so the periodic loop, the observer and a manual "Sync now" never overlap.
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    // Wakes the periodic loop early (from a manual sync, post-permission-grant scan, or the
    // observer) so a large backlog drains on a ~1-minute cadence instead of waiting out the
    // loop's normal 15/60-minute sleep before the next scan.
    private readonly Channel<bool> _wakeChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = true,
        SingleReader = true
    });

    // Cached folder IDs so we don't re-create folders on every upload.
    private Guid? _rootFolderId;
    private (int Year, int Month, Guid Id)? _cachedMonthFolder;

    // Names/sizes already present under the server AutoUpload tree (loaded once per process,
    // so a first-run backfill doesn't re-upload items that were synced in an earlier session).
    private bool _serverSeedLoaded;
    private HashSet<(string Name, long Size)>? _serverUploaded;

    // Set when a scan found more items than it could upload in one pass, so the loop
    // shortens its next delay to chew through the backlog.
    private bool _hasBacklog;

    // True when the last pass left media queued — either a backlog beyond the per-pass cap, or
    // items this pass selected but could not finish. Drives the background job's adaptive cadence
    // (short poll while work remains, idle interval once the queue is empty).
    private bool _hasPendingWork;

    // Storage-quota state: remaining bytes (long.MaxValue when unlimited) plus a flag so the
    // "storage full" notification is raised once per transition rather than on every scan.
    private long _quotaRemainingBytes = long.MaxValue;
    private bool _quotaBlocked;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    // Real-time MediaStore observer — signals when new photos/videos appear.
    private MediaStoreContentObserver? _contentObserver;
    private Task? _observerListenerTask;

    /// <inheritdoc />
    public bool IsRunning => _loopCts is not null && !_loopCts.IsCancellationRequested;

    /// <inheritdoc />
    public bool HasPendingWork => _hasPendingWork;

    /// <summary>Initializes a new <see cref="MediaAutoUploadService"/>.</summary>
    public MediaAutoUploadService(
        IServerConnectionStore connectionStore,
        ISecureTokenStore tokenStore,
        IFileRestClient fileApi,
        IAppForegroundService foregroundService,
        IMediaPermissionService permissionService,
        ILogger<MediaAutoUploadService> logger)
    {
        _connectionStore = connectionStore;
        _tokenStore = tokenStore;
        _fileApi = fileApi;
        _foregroundService = foregroundService;
        _permissionService = permissionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        _loopCts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCts.Token);

        // Register real-time MediaStore observer for instant photo/video detection.
        if (Platform.AppContext?.ContentResolver is { } resolver)
        {
            _contentObserver = new MediaStoreContentObserver();
            _contentObserver.Register(resolver);
            _observerListenerTask = ObserveMediaStoreChangesAsync(_loopCts.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_loopCts is null)
            return;

        await _loopCts.CancelAsync().ConfigureAwait(false);
        try
        { await (_loopTask ?? Task.CompletedTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _loopCts.Dispose();
        _loopCts = null;

        // Unregister MediaStore observer.
        if (_contentObserver is not null && Platform.AppContext?.ContentResolver is { } resolver)
        {
            _contentObserver.Unregister(resolver);
            _contentObserver = null;
        }
    }

    /// <inheritdoc />
    public Task ScanAndUploadNowAsync(CancellationToken cancellationToken = default)
        => RunScanAsync(cancellationToken);

    /// <summary>
    /// Runs one scan/upload pass and, if it left a backlog, wakes the periodic loop so it
    /// continues draining on the short backlog cadence rather than the long idle interval.
    /// </summary>
    private async Task RunScanAsync(CancellationToken ct)
    {
        await UploadNewMediaAsync(ct).ConfigureAwait(false);
        if (_hasBacklog)
            SignalWake();
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveUploadTargetFolderAsync(
        string serverBaseUrl, string accessToken,
        DateTime? timestamp = null, CancellationToken ct = default)
    {
        var dt = timestamp ?? DateTime.UtcNow;
        return await EnsureUploadFolderAsync(serverBaseUrl, accessToken, dt.Year, dt.Month, ct)
            .ConfigureAwait(false);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Listens for MediaStore change signals from the <see cref="MediaStoreContentObserver"/>
    /// and triggers a scan-and-upload whenever new media content is detected.
    /// </summary>
    private async Task ObserveMediaStoreChangesAsync(CancellationToken ct)
    {
        if (_contentObserver is null)
            return;

        var reader = _contentObserver.Reader;
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out _))
                {
                    // Drain any buffered signals — we only need one scan.
                }

                _logger.LogDebug("MediaStore change detected; triggering immediate scan.");
                _ = RunScanAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested.
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UploadNewMediaAsync(ct).ConfigureAwait(false);

                // While a first-run backfill (or a large backlog) is draining, scan every
                // minute; otherwise use a battery-friendly interval. When foregrounded the
                // MediaStoreContentObserver already handles real-time detection. A manual
                // "Sync now" or permission grant can also wake us early (see SignalWake).
                var delay = _hasBacklog
                    ? TimeSpan.FromMinutes(1)
                    : _foregroundService.IsInForeground
                        ? _foregroundScanInterval
                        : _backgroundScanInterval;
                await WaitForWakeOrDelayAsync(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media auto-upload scan failed.");
                await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Signals the periodic loop to wake early (e.g. a manual scan left a backlog).</summary>
    private void SignalWake() => _wakeChannel.Writer.TryWrite(true);

    /// <summary>
    /// Waits either for the given delay or until <see cref="SignalWake"/> is called, whichever
    /// comes first. Returns early on a wake so the caller re-scans immediately.
    /// </summary>
    private async Task WaitForWakeOrDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timer = Task.Delay(delay, delayCts.Token);
        var wake = _wakeChannel.Reader.WaitToReadAsync(ct).AsTask();
        var winner = await Task.WhenAny(timer, wake).ConfigureAwait(false);

        delayCts.Cancel();
        _wakeChannel.Reader.TryRead(out _);
        _ = winner; // if wake won, we return immediately and re-scan
    }

    private async Task UploadNewMediaAsync(CancellationToken ct)
    {
        if (!Preferences.Default.Get(PrefEnabled, false))
            return;

        if (Preferences.Default.Get(PrefWifiOnly, true) && !IsOnWifi())
        {
            _logger.LogDebug("Media auto-upload skipped — not on WiFi.");
            return;
        }

        // Charging-only mode: skip if not plugged in.
        if (Preferences.Default.Get(PrefChargingOnly, false) && !IsCharging())
        {
            _logger.LogDebug("Media auto-upload skipped — charging-only mode active and device is not charging.");
            return;
        }

        // Battery threshold: skip if battery is below the minimum percentage.
        var minBatteryPct = Preferences.Default.Get(PrefBatteryThreshold, 20);
        if (minBatteryPct > 0 && GetBatteryPercentage() is { } pct && pct < minBatteryPct)
        {
            _logger.LogDebug("Media auto-upload skipped — battery ({BatteryPct}%) below threshold ({Threshold}%).", pct, minBatteryPct);
            return;
        }

        var connection = _connectionStore.GetActive();
        if (connection is null)
            return;

        var accessToken = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            .ConfigureAwait(false);
        if (accessToken is null)
            return;

        _hasBacklog = false;
        _hasPendingWork = false;

        // Serialise scans — the periodic loop, the observer and a manual "Sync now" can all
        // arrive here concurrently and must not race (which previously caused duplicates).
        await _scanLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Storage-quota gate: if the account has a finite quota and it is exhausted, stop and
            // notify rather than letting every upload fail with 409 FILES_QUOTA_EXCEEDED.
            var (unlimited, remaining, usedBytes, totalBytes) = await QueryQuotaAsync(
                connection.ServerBaseUrl, accessToken, ct).ConfigureAwait(false);
            _quotaRemainingBytes = remaining;
            if (!unlimited && remaining == 0)
            {
                NotifyQuotaFull(usedBytes, totalBytes);
                CancelUploadNotification();
                return;
            }

            if (_quotaBlocked)
            {
                // Room freed up (or the limit was lifted) — clear the notice and resume.
                _quotaBlocked = false;
                CancelQuotaNotification();
            }

            // App-private captures made while auto-upload was off don't need the media-library
            // permission, so flush them before the gallery scan.
            await UploadPendingFilesAsync(connection.ServerBaseUrl, accessToken, ct).ConfigureAwait(false);

            if (!_permissionService.HasMediaReadPermission())
            {
                _logger.LogWarning(
                    "Media auto-upload skipped — no media-library read permission. Grant 'Photos access' in Settings to back up the camera roll.");
                return;
            }

            var candidates = QueryMediaCandidates();
            if (candidates.Count == 0)
            {
                CancelUploadNotification();
                return;
            }

            // Load (once per process) the file names/sizes already under the server AutoUpload
            // tree so a first-run backfill doesn't re-upload items synced in an earlier session.
            await EnsureServerSeedLoadedAsync(connection.ServerBaseUrl, accessToken, ct).ConfigureAwait(false);

            var pending = new List<MediaCandidate>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (await _index.ContainsAsync(ComputeMediaKey(candidate), ct).ConfigureAwait(false))
                    continue;
                if (IsAlreadyOnServer(candidate))
                    continue;
                pending.Add(candidate);
            }

            if (pending.Count == 0)
            {
                CancelUploadNotification();
                return;
            }

            // Respect the (possibly finite) quota: never pick more bytes than remain, and skip
            // individual items larger than the remaining space — they stay pending until room frees.
            var toUpload = new List<MediaCandidate>();
            var budget = _quotaRemainingBytes;
            foreach (var candidate in pending)
            {
                if (candidate.Size > budget)
                    continue;
                toUpload.Add(candidate);
                budget -= candidate.Size;
                if (toUpload.Count >= MaxItemsPerPass)
                    break;
            }

            if (toUpload.Count == 0)
            {
                // Nothing fits in the remaining quota right now — wait for space instead of spamming.
                _hasBacklog = false;
                CancelUploadNotification();
                return;
            }
            _hasBacklog = pending.Count > toUpload.Count;

            _logger.LogInformation(
                "Found {PendingCount} new photo(s)/video(s); uploading {BatchCount} this pass.",
                pending.Count, toUpload.Count);

            if (Platform.AppContext is not { } appContext)
            {
                _logger.LogWarning("Platform.AppContext is null; cannot show upload notifications.");
                return;
            }

            var nm = NotificationManagerCompat.From(appContext);
            if (nm is null)
            {
                _logger.LogWarning("NotificationManagerCompat unavailable; skipping upload notifications.");
                return;
            }

            var uploaded = 0;
            foreach (var candidate in toUpload)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var mimeType = GuessMimeType(candidate.FileName, candidate.IsVideo ? "video/mp4" : "image/jpeg");
                    await UploadMediaItemAsync(
                        connection.ServerBaseUrl, accessToken, candidate, mimeType, ct)
                        .ConfigureAwait(false);

                    // Only record AFTER a successful upload so failures are retried next scan.
                    await _index.RecordAsync(ToIndexRow(candidate), ct).ConfigureAwait(false);
                    _serverUploaded?.Add((candidate.FileName, candidate.Size));
                    uploaded++;
                    ShowProgress(nm, appContext, "Uploading media", uploaded, toUpload.Count);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // Server rejected with 409 — quota exhausted mid-batch (or a name clash).
                    // Stop the pass; the quota gate re-checks next scan and raises the full notice.
                    _logger.LogWarning(ex, "Upload rejected with 409 for {FileName}; stopping pass (likely quota).", candidate.FileName);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upload media {FileName}; will retry next scan.", candidate.FileName);
                }
            }

            if (uploaded > 0)
                Preferences.Default.Set(PrefLastSuccessTs, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Anything this pass could not finish — a backlog beyond the per-pass cap, or items
            // that failed and so were not recorded — keeps the background job on its short cadence.
            _hasPendingWork = _hasBacklog || uploaded < toUpload.Count;

            nm.Cancel(NotificationId);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>
    /// Uploads all files from the local pending uploads queue directory. These are photos/videos
    /// captured while auto-upload was off (the app-private spool). A file is only removed from the
    /// queue after a successful upload; each success is recorded in the index so a partially-failed
    /// pass never re-uploads an item that did make it.
    /// </summary>
    private async Task UploadPendingFilesAsync(string serverBaseUrl, string accessToken, CancellationToken ct)
    {
        var pendingDir = System.IO.Path.Combine(
            Microsoft.Maui.Storage.FileSystem.AppDataDirectory,
            PendingUploadsDirName);

        if (!Directory.Exists(pendingDir))
            return;

        var files = Directory.GetFiles(pendingDir);
        if (files.Length == 0)
            return;

        _logger.LogInformation("Found {Count} pending file(s) in local upload queue.", files.Length);

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = System.IO.Path.GetFileName(filePath);
            var mimeType = GuessMimeType(fileName, "application/octet-stream");

            try
            {
                // Skip anything already recorded (e.g. an upload that succeeded but whose local
                // deletion was interrupted) — delete the leftover file rather than re-uploading.
                if (await _index.ContainsAsync(filePath, ct).ConfigureAwait(false))
                {
                    SafeDeleteFile(filePath);
                    continue;
                }

                await using var fileStream = File.OpenRead(filePath);
                using var ms = new MemoryStream();
                await fileStream.CopyToAsync(ms, ct).ConfigureAwait(false);
                ms.Position = 0;

                // Resolve the AutoUpload/YYYY/MM folder.
                Guid? parentId = null;
                if (Preferences.Default.Get(PrefOrganizeByDate, true))
                {
                    parentId = await EnsureUploadFolderAsync(
                        serverBaseUrl, accessToken, DateTime.UtcNow.Year, DateTime.UtcNow.Month, ct)
                        .ConfigureAwait(false);
                }

                await _fileApi.UploadFileAsync(
                    serverBaseUrl, accessToken,
                    fileName, parentId,
                    ms, ms.Length, mimeType,
                    progress: null, ct).ConfigureAwait(false);

                await _index.RecordAsync(new UploadedMediaRow
                {
                    MediaKey = filePath,
                    SourceUri = filePath,
                    DisplayName = fileName,
                    FileSize = ms.Length,
                    DateAddedUtcTicks = DateTime.UtcNow.Ticks,
                    ServerFolder = GetFolderLabel(DateTime.UtcNow),
                    UploadedAtUtcTicks = DateTime.UtcNow.Ticks
                }, ct).ConfigureAwait(false);

                _logger.LogInformation("Uploaded pending file {FileName}.", fileName);
                SafeDeleteFile(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload pending file {FileName}; keeping in queue.", fileName);
            }
        }
    }

    /// <summary>Deletes a file, swallowing any I/O errors.</summary>
    private static void SafeDeleteFile(string path)
    {
        try
        { File.Delete(path); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete pending file {path}: {ex.Message}");
        }
    }

    /// <summary>Removes any stale "Uploading media" progress notification (id <see cref="NotificationId"/>).</summary>
    private static void CancelUploadNotification()
    {
        if (Platform.AppContext is not { } context)
            return;
        try
        {
            NotificationManagerCompat.From(context)?.Cancel(NotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cancel upload notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Queries the server storage quota. Returns <c>Unlimited=true</c> (remaining =
    /// <see cref="long.MaxValue"/>) when the account has no finite quota or the query fails; the
    /// server still rejects truly over-quota uploads with 409 FILES_QUOTA_EXCEEDED.
    /// </summary>
    private async Task<(bool Unlimited, long Remaining, long UsedBytes, long TotalBytes)> QueryQuotaAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct)
    {
        try
        {
            var quota = await _fileApi.GetQuotaAsync(serverBaseUrl, accessToken, ct).ConfigureAwait(false);
            var remaining = QuotaGate.RemainingBytes(quota.TotalBytes, quota.UsedBytes);
            _logger.LogDebug("Storage quota: used={Used} total={Total} remaining={Remaining}.",
                quota.UsedBytes, quota.TotalBytes,
                remaining == long.MaxValue ? "unlimited" : remaining.ToString());
            return (
                Unlimited: QuotaGate.HasFiniteQuota(quota.TotalBytes) is false,
                Remaining: remaining,
                UsedBytes: quota.UsedBytes,
                TotalBytes: quota.TotalBytes);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Quota check failed; assuming no quota limit for this pass.");
            return (Unlimited: true, Remaining: long.MaxValue, UsedBytes: 0, TotalBytes: 0);
        }
    }

    /// <summary>
    /// Posts a "storage full — auto-upload paused" notification. Raised once per blocked
    /// transition so a full account doesn't re-notify on every scan.
    /// </summary>
    private void NotifyQuotaFull(long usedBytes, long totalBytes)
    {
        if (_quotaBlocked)
            return;
        _quotaBlocked = true;

        if (Platform.AppContext is not { } context)
            return;
        try
        {
            var nm = NotificationManagerCompat.From(context);
            if (nm is null)
                return;

            var message = $"You've used {FormatBytes(usedBytes)} of {FormatBytes(totalBytes)}. Free up space in DotNetCloud to resume auto-upload.";
            var openIntent = new Intent(context, typeof(MainActivity));
            openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pendingIntent = global::Android.App.PendingIntent.GetActivity(
                context, 1, openIntent,
                global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);

#pragma warning disable CS8602 // AndroidX Builder fluent setters are annotated nullable
            var notification = new NotificationCompat.Builder(context, MainApplication.ChannelIdMediaUpload)
                .SetSmallIcon(global::Android.Resource.Drawable.IcMenuUpload)
                .SetContentTitle("Auto-upload paused — storage full")
                .SetContentText(message)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
                .SetContentIntent(pendingIntent)
                .Build()!;
#pragma warning restore CS8602
            nm.Notify(QuotaNotificationId, notification);
            _logger.LogWarning("Media auto-upload paused: storage quota full ({Used}/{Total}).", usedBytes, totalBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show quota-full notification: {ex.Message}");
        }
    }

    /// <summary>Removes the "storage full" notification (id <see cref="QuotaNotificationId"/>).</summary>
    private static void CancelQuotaNotification()
    {
        if (Platform.AppContext is not { } context)
            return;
        try
        {
            NotificationManagerCompat.From(context)?.Cancel(QuotaNotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cancel quota notification: {ex.Message}");
        }
    }

    /// <summary>Formats a byte count as a human-readable string (B/KB/MB/GB/TB).</summary>
    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    private async Task UploadMediaItemAsync(
        string serverBaseUrl,
        string accessToken,
        MediaCandidate candidate,
        string mimeType,
        CancellationToken ct)
    {
        var resolver = Platform.AppContext?.ContentResolver;
        if (resolver is null)
        {
            _logger.LogWarning("ContentResolver is null; cannot upload media.");
            return;
        }

        var uri = AndroidUri.Parse(candidate.ContentUri);
        if (uri is null)
        {
            _logger.LogWarning("Failed to parse content URI: {Uri}", candidate.ContentUri);
            return;
        }

        // Determine parent folder based on date-organization preference.
        Guid? parentId = null;
        if (Preferences.Default.Get(PrefOrganizeByDate, true))
        {
            var mediaDt = DateTimeOffset.FromUnixTimeSeconds(candidate.DateAddedSeconds).LocalDateTime;
            parentId = await EnsureUploadFolderAsync(
                serverBaseUrl, accessToken, mediaDt.Year, mediaDt.Month, ct)
                .ConfigureAwait(false);
        }

        using var inputStream = resolver.OpenInputStream(uri);
        if (inputStream is null)
        {
            _logger.LogWarning("Failed to open input stream for URI: {Uri}", candidate.ContentUri);
            return;
        }

        using var ms = new MemoryStream();
        await inputStream.CopyToAsync(ms, ct).ConfigureAwait(false);
        ms.Position = 0;

        await _fileApi.UploadFileAsync(
            serverBaseUrl, accessToken,
            candidate.FileName, parentId,
            ms, ms.Length, mimeType,
            progress: null, ct).ConfigureAwait(false);

        _logger.LogInformation("Uploaded {FileName} ({Bytes} bytes) to {FolderName}.",
            candidate.FileName, ms.Length, parentId.HasValue ? "date folder" : "root");
    }

    /// <summary>
    /// Ensures the <c>AutoUpload/YYYY/MM</c> folder chain exists on the server and
    /// returns the month-level folder ID. Results are cached to avoid repeated API calls.
    /// </summary>
    private async Task<Guid?> EnsureUploadFolderAsync(
        string serverBaseUrl, string accessToken,
        int year, int month, CancellationToken ct)
    {
        // 1. Ensure the root upload folder (e.g. "InstantUpload") exists
        if (_rootFolderId is null)
        {
            var folderName = Preferences.Default.Get(PrefUploadFolderName, DefaultUploadFolderName);
            _rootFolderId = await FindOrCreateFolderAsync(
                serverBaseUrl, accessToken, folderName, parentId: null, ct).ConfigureAwait(false);
        }

        // 2. Check month-folder cache
        if (_cachedMonthFolder is { } cached && cached.Year == year && cached.Month == month)
            return cached.Id;

        // 3. Ensure year folder (e.g. "2026")
        var yearFolderId = await FindOrCreateFolderAsync(
            serverBaseUrl, accessToken, year.ToString(), _rootFolderId, ct).ConfigureAwait(false);

        // 4. Ensure month folder (e.g. "03")
        var monthName = month.ToString("D2");
        var monthFolderId = await FindOrCreateFolderAsync(
            serverBaseUrl, accessToken, monthName, yearFolderId, ct).ConfigureAwait(false);

        _cachedMonthFolder = (year, month, monthFolderId);
        return monthFolderId;
    }

    /// <summary>Finds a child folder by name, creating it if it doesn't exist.</summary>
    private async Task<Guid> FindOrCreateFolderAsync(
        string serverBaseUrl, string accessToken,
        string folderName, Guid? parentId, CancellationToken ct)
    {
        var children = await _fileApi.ListChildrenAsync(serverBaseUrl, accessToken, parentId, ct)
            .ConfigureAwait(false);

        var existing = children.FirstOrDefault(c =>
            string.Equals(c.Name, folderName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.NodeType, "Folder", StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing.Id;

        var created = await _fileApi.CreateFolderAsync(
            serverBaseUrl, accessToken, folderName, parentId, ct).ConfigureAwait(false);

        _logger.LogInformation("Created upload folder '{FolderName}' (parent={ParentId}).", folderName, parentId);
        return created.Id;
    }

    /// <summary>A single photo/video row discovered in MediaStore (read-only).</summary>
    private sealed record MediaCandidate(
        string ContentUri,
        string FileName,
        long Size,
        long DateAddedSeconds,
        bool IsVideo);

    /// <summary>
    /// Enumerates (read-only) every photo and video currently visible to the app in MediaStore,
    /// smallest first and skipping anything above <see cref="MaxSingleItemBytes"/>. On Android 13+
    /// this requires READ_MEDIA_IMAGES/VIDEO to see other apps' media; without the permission the
    /// query returns at most the app's own rows.
    /// </summary>
    private List<MediaCandidate> QueryMediaCandidates()
    {
        var result = new List<MediaCandidate>();
        var oversizedCount = 0;
        var resolver = Platform.AppContext?.ContentResolver;
        if (resolver is null)
            return result;

        Collect(MediaStore.Images.Media.ExternalContentUri, isVideo: false);
        Collect(MediaStore.Video.Media.ExternalContentUri, isVideo: true);

        if (oversizedCount > 0)
        {
            _logger.LogInformation(
                "Skipped {SkippedCount} media item(s) larger than {MaxMb} MB.",
                oversizedCount, MaxSingleItemBytes / (1024 * 1024));
        }

        // Smallest first: the index records an item only once its upload fully succeeds, so
        // draining quick wins first keeps the queue advancing even if the process is killed
        // mid-pass. Ordering by date previously parked a 1.7 GB video at the head and blocked
        // every subsequent pass from making any progress.
        return result.OrderBy(c => c.Size).ToList();

        void Collect(AndroidUri? collectionUri, bool isVideo)
        {
            if (collectionUri is null)
                return;

            var projection = new[] { "_id", "_display_name", "_size", "date_added" };
            try
            {
                using var cursor = resolver.Query(
                    collectionUri, projection,
                    selection: null,
                    selectionArgs: null,
                    sortOrder: "date_added ASC");
                if (cursor is null)
                    return;

                var idIdx = cursor.GetColumnIndex("_id");
                var nameIdx = cursor.GetColumnIndex("_display_name");
                var sizeIdx = cursor.GetColumnIndex("_size");
                var dateIdx = cursor.GetColumnIndex("date_added");
                if (idIdx < 0 || nameIdx < 0 || dateIdx < 0)
                    return;

                while (cursor.MoveToNext())
                {
                    var id = cursor.GetLong(idIdx);
                    var name = cursor.GetString(nameIdx) ?? $"media_{id}";
                    var size = sizeIdx >= 0 ? cursor.GetLong(sizeIdx) : 0L;
                    var date = cursor.GetLong(dateIdx);

                    // Leave oversized items untouched on the device: uploading one can outlast a
                    // process lifetime, and a partial attempt records no progress at all.
                    if (size > MaxSingleItemBytes)
                    {
                        oversizedCount++;
                        continue;
                    }

                    result.Add(new MediaCandidate(
                        ContentUris.WithAppendedId(collectionUri, id).ToString() ?? $"media_{id}",
                        name, size, date, isVideo));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MediaStore query failed for {collectionUri}: {ex.Message}");
            }
        }
    }

    /// <summary>Stable identity for dedup across scans: name + size + dateAdded second.</summary>
    private static string ComputeMediaKey(MediaCandidate candidate)
        => $"{candidate.FileName}|{candidate.Size}|{candidate.DateAddedSeconds}";

    /// <summary>
    /// Loads (once per process) the names/sizes already present under the server's AutoUpload tree
    /// so a first-run backfill skips items synced in an earlier session instead of creating server
    /// duplicates. Cached for the lifetime of the service and extended as new uploads succeed.
    /// </summary>
    private async Task EnsureServerSeedLoadedAsync(string serverBaseUrl, string accessToken, CancellationToken ct)
    {
        if (_serverSeedLoaded)
            return;

        _serverSeedLoaded = true;
        var names = new HashSet<(string Name, long Size)>();
        try
        {
            var folderName = Preferences.Default.Get(PrefUploadFolderName, DefaultUploadFolderName);
            var rootChildren = await _fileApi.ListChildrenAsync(serverBaseUrl, accessToken, folderId: null, ct)
                .ConfigureAwait(false);
            var root = rootChildren.FirstOrDefault(f =>
                string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.NodeType, "Folder", StringComparison.OrdinalIgnoreCase));
            if (root is null)
                return;

            var yearFolders = await _fileApi.ListChildrenAsync(serverBaseUrl, accessToken, root.Id, ct)
                .ConfigureAwait(false);
            foreach (var year in yearFolders.Where(f =>
                         string.Equals(f.NodeType, "Folder", StringComparison.OrdinalIgnoreCase)))
            {
                var monthFolders = await _fileApi.ListChildrenAsync(serverBaseUrl, accessToken, year.Id, ct)
                    .ConfigureAwait(false);
                foreach (var month in monthFolders.Where(f =>
                             string.Equals(f.NodeType, "Folder", StringComparison.OrdinalIgnoreCase)))
                {
                    var files = await _fileApi.ListChildrenAsync(serverBaseUrl, accessToken, month.Id, ct)
                        .ConfigureAwait(false);
                    foreach (var file in files.Where(f =>
                                 string.Equals(f.NodeType, "File", StringComparison.OrdinalIgnoreCase)))
                    {
                        names.Add((file.Name, file.Size));
                    }
                }
            }

            _logger.LogInformation("Seeded server AutoUpload index with {Count} existing file(s).", names.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seed server AutoUpload names; backfill may re-upload existing files.");
        }
        finally
        {
            _serverUploaded = names;
        }
    }

    /// <summary>True when a candidate already exists on the server under the AutoUpload tree (name + size match).</summary>
    private bool IsAlreadyOnServer(MediaCandidate candidate)
        => _serverUploaded?.Contains((candidate.FileName, candidate.Size)) ?? false;

    /// <summary>Builds the index row recorded after a successful gallery upload.</summary>
    private UploadedMediaRow ToIndexRow(MediaCandidate candidate)
        => new()
        {
            MediaKey = ComputeMediaKey(candidate),
            SourceUri = candidate.ContentUri,
            DisplayName = candidate.FileName,
            FileSize = candidate.Size,
            DateAddedUtcTicks = DateTimeOffset.FromUnixTimeSeconds(candidate.DateAddedSeconds).UtcTicks,
            ServerFolder = GetFolderLabel(DateTimeOffset.FromUnixTimeSeconds(candidate.DateAddedSeconds).LocalDateTime),
            UploadedAtUtcTicks = DateTime.UtcNow.Ticks
        };

    /// <summary>Formats a local date-time as the <c>YYYY/MM</c> server folder label.</summary>
    private static string GetFolderLabel(DateTime local)
        => $"{local.Year:D4}/{local.Month:D2}";

    private static void ShowProgress(
        NotificationManagerCompat nm, global::Android.Content.Context context,
        string title, int current, int total)
    {
        // NotificationCompat.Builder fluent setters return Builder? in the AndroidX binding
        // even though the real Java API is @NonNull. The chain is always safe here.
#pragma warning disable CS8602
        var notification = new NotificationCompat.Builder(context, MainApplication.ChannelIdMediaUpload)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuUpload)
            .SetContentTitle(title)
            .SetContentText($"{current} of {total} uploaded")
            .SetProgress(total, current, false)
            .SetOngoing(true)
            .Build()!;
#pragma warning restore CS8602
        nm.Notify(NotificationId, notification);
    }

    private static string GuessMimeType(string fileName, string fallback)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" or ".heif" => "image/heif",
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".3gp" => "video/3gpp",
            _ => fallback
        };
    }

    private static bool IsOnWifi()
    {
        var cm = Platform.AppContext.GetSystemService(Context.ConnectivityService)
            as AndroidConnectivityManager;
        if (cm is null)
            return false;
        var caps = cm.ActiveNetwork is { } net ? cm.GetNetworkCapabilities(net) : null;
        return caps?.HasTransport(AndroidTransportType.Wifi) ?? false;
    }

    /// <summary>Returns <c>true</c> if the device is plugged in (AC or USB).</summary>
    private static bool IsCharging()
    {
        var context = Platform.AppContext;
        if (context is null)
            return false;

        var batteryManager = context.GetSystemService(Context.BatteryService) as global::Android.OS.BatteryManager;
        if (batteryManager is null)
            return false;

        return batteryManager.IsCharging;
    }

    /// <summary>Returns the current battery percentage (0–100), or <c>null</c> if unavailable.</summary>
    private static int? GetBatteryPercentage()
    {
        var context = Platform.AppContext;
        if (context is null)
            return null;

        var batteryManager = context.GetSystemService(Context.BatteryService) as global::Android.OS.BatteryManager;
        if (batteryManager is null)
            return null;

        return batteryManager.GetIntProperty((int)global::Android.OS.BatteryProperty.Capacity);
    }
}
