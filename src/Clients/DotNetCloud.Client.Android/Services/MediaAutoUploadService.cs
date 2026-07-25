using Android.Content;
using AndroidX.Core.App;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using AndroidUri = global::Android.Net.Uri;
using AndroidConnectivityManager = global::Android.Net.ConnectivityManager;
using AndroidTransportType = global::Android.Net.TransportType;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Periodically scans the device's MediaStore for new photos and videos, uploading them
/// to the active DotNetCloud server using the chunked upload protocol via <see cref="IFileRestClient"/>.
/// Organises uploads into an <c>AutoUpload/YYYY/MM</c> folder hierarchy by default.
/// Respects WiFi-only and enabled/disabled preferences.
/// </summary>
internal sealed class MediaAutoUploadService : IMediaAutoUploadService
{
    private const string PrefEnabled = "media_upload_enabled";
    private const string PrefWifiOnly = "media_upload_wifi_only";
    private const string PrefOrganizeByDate = "media_upload_organize_by_date";
    private const string PrefUploadFolderName = "media_upload_folder_name";
    private const string PrefLastPhotoTs = "media_upload_last_photo_ts";
    private const string PrefLastVideoTs = "media_upload_last_video_ts";
    private const int NotificationId = 3001;
    private const string DefaultUploadFolderName = "AutoUpload";
    private const string PrefDedupPrefix = "media_upload_dedup_";
    private const string PrefChargingOnly = "media_upload_charging_only";
    private const string PrefBatteryThreshold = "media_upload_battery_threshold";
    private const string PendingUploadsDirName = "PendingUploads";

    private readonly IServerConnectionStore _connectionStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IFileRestClient _fileApi;
    private readonly ILogger<MediaAutoUploadService> _logger;
    private readonly IAppForegroundService _foregroundService;
    private readonly TimeSpan _foregroundScanInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _backgroundScanInterval = TimeSpan.FromMinutes(60);

    // Cached folder IDs so we don't re-create folders on every upload.
    private Guid? _rootFolderId;
    private (int Year, int Month, Guid Id)? _cachedMonthFolder;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    // Real-time MediaStore observer — signals when new photos/videos appear.
    private MediaStoreContentObserver? _contentObserver;
    private Task? _observerListenerTask;

    /// <inheritdoc />
    public bool IsRunning => _loopCts is not null && !_loopCts.IsCancellationRequested;

    /// <summary>Initializes a new <see cref="MediaAutoUploadService"/>.</summary>
    public MediaAutoUploadService(
        IServerConnectionStore connectionStore,
        ISecureTokenStore tokenStore,
        IFileRestClient fileApi,
        IAppForegroundService foregroundService,
        ILogger<MediaAutoUploadService> logger)
    {
        _connectionStore = connectionStore;
        _tokenStore = tokenStore;
        _fileApi = fileApi;
        _foregroundService = foregroundService;
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
        => UploadNewMediaAsync(cancellationToken);

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
                _ = UploadNewMediaAsync(ct);
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

                // Use a longer scan interval when backgrounded to conserve battery.
                // When foregrounded, the MediaStoreContentObserver handles real-time detection.
                var delay = _foregroundService.IsInForeground
                    ? _foregroundScanInterval
                    : _backgroundScanInterval;
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media auto-upload scan failed.");
                await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            }
        }
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

        // Collect both photos and videos
        var lastPhotoTs = Preferences.Default.Get(PrefLastPhotoTs, 0L);
        var lastVideoTs = Preferences.Default.Get(PrefLastVideoTs, 0L);

        var photos = QueryNewMediaSince("content://media/external/images/media", lastPhotoTs);
        var videos = QueryNewMediaSince("content://media/external/video/media", lastVideoTs);

        var totalItems = photos.Count + videos.Count;
        if (totalItems == 0)
        {
            // Also check the local pending uploads queue — files saved by camera capture
            // while auto-upload was off.
            await UploadPendingFilesAsync(connection.ServerBaseUrl, accessToken, ct);
            return;
        }

        _logger.LogInformation("Found {PhotoCount} new photo(s) and {VideoCount} new video(s) to upload.",
            photos.Count, videos.Count);

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
        int uploaded = 0;

        // Upload photos
        foreach (var (contentUri, fileName, dateAdded) in photos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var mimeType = GuessMimeType(fileName, "image/jpeg");
                await UploadMediaItemAsync(
                    connection.ServerBaseUrl, accessToken, contentUri, fileName, mimeType, dateAdded, ct)
                    .ConfigureAwait(false);

                Preferences.Default.Set(PrefLastPhotoTs, dateAdded);
                uploaded++;
                ShowProgress(nm, appContext, "Uploading media", uploaded, totalItems);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload photo {FileName}.", fileName);
            }
        }

        // Upload videos
        foreach (var (contentUri, fileName, dateAdded) in videos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var mimeType = GuessMimeType(fileName, "video/mp4");
                await UploadMediaItemAsync(
                    connection.ServerBaseUrl, accessToken, contentUri, fileName, mimeType, dateAdded, ct)
                    .ConfigureAwait(false);

                Preferences.Default.Set(PrefLastVideoTs, dateAdded);
                uploaded++;
                ShowProgress(nm, appContext, "Uploading media", uploaded, totalItems);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload video {FileName}.", fileName);
            }
        }

        // Upload any files in the local pending queue (captured while setting was off).
        await UploadPendingFilesAsync(connection.ServerBaseUrl, accessToken, ct);

        nm.Cancel(NotificationId);
    }

    /// <summary>
    /// Uploads all files from the local pending uploads queue directory.
    /// These are photos/videos captured from the Files tab while auto-upload was off.
    /// After successful upload, the local file is deleted.
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

                // Check dedup before uploading.
                var fingerprint = ComputeFingerprint(ms, ms.Length, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (!string.IsNullOrEmpty(fingerprint) && Preferences.Default.ContainsKey(PrefDedupPrefix + fingerprint))
                {
                    _logger.LogDebug("Skipping pending {FileName} — already uploaded.", fileName);
                    SafeDeleteFile(filePath);
                    continue;
                }

                await _fileApi.UploadFileAsync(
                    serverBaseUrl, accessToken,
                    fileName, parentId,
                    ms, ms.Length, mimeType,
                    progress: null, ct).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(fingerprint))
                    Preferences.Default.Set(PrefDedupPrefix + fingerprint, filePath);

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

    private async Task UploadMediaItemAsync(
        string serverBaseUrl,
        string accessToken,
        string contentUri,
        string fileName,
        string mimeType,
        long dateAdded,
        CancellationToken ct)
    {
        var resolver = Platform.AppContext?.ContentResolver;
        if (resolver is null)
        {
            _logger.LogWarning("ContentResolver is null; cannot upload media.");
            return;
        }

        var uri = AndroidUri.Parse(contentUri);
        if (uri is null)
        {
            _logger.LogWarning("Failed to parse content URI: {Uri}", contentUri);
            return;
        }

        // Determine parent folder based on date-organization preference
        Guid? parentId = null;
        if (Preferences.Default.Get(PrefOrganizeByDate, true))
        {
            var mediaDt = DateTimeOffset.FromUnixTimeSeconds(dateAdded).LocalDateTime;
            parentId = await EnsureUploadFolderAsync(
                serverBaseUrl, accessToken, mediaDt.Year, mediaDt.Month, ct)
                .ConfigureAwait(false);
        }

        using var inputStream = resolver.OpenInputStream(uri);
        if (inputStream is null)
        {
            _logger.LogWarning("Failed to open input stream for URI: {Uri}", contentUri);
            return;
        }

        using var ms = new MemoryStream();
        await inputStream.CopyToAsync(ms, ct).ConfigureAwait(false);
        ms.Position = 0;

        // Dedup: skip if this file was already uploaded (fingerprint matches).
        var fingerprint = ComputeFingerprint(ms, ms.Length, dateAdded);
        if (!string.IsNullOrEmpty(fingerprint) && Preferences.Default.ContainsKey(PrefDedupPrefix + fingerprint))
        {
            _logger.LogDebug("Skipping {FileName} — already uploaded (fingerprint match).", fileName);
            return;
        }

        var result = await _fileApi.UploadFileAsync(
            serverBaseUrl, accessToken,
            fileName, parentId,
            ms, ms.Length, mimeType,
            progress: null, ct).ConfigureAwait(false);

        // Store dedup fingerprint so we don't re-upload if the timestamp resets.
        if (!string.IsNullOrEmpty(fingerprint))
        {
            Preferences.Default.Set(PrefDedupPrefix + fingerprint, contentUri);
        }

        _logger.LogInformation("Uploaded {FileName} ({Bytes} bytes) to {FolderName}.",
            fileName, ms.Length, parentId.HasValue ? "date folder" : "root");
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

    private static List<(string Uri, string FileName, long DateAdded)> QueryNewMediaSince(
        string mediaStoreUri, long afterTimestamp)
    {
        var result = new List<(string, string, long)>();
        var resolver = Platform.AppContext?.ContentResolver;
        if (resolver is null)
            return result;

        var uri = AndroidUri.Parse(mediaStoreUri);
        if (uri is null)
            return result;

        var projection = new[] { "_id", "_display_name", "date_added" };

        using var cursor = resolver.Query(
            uri, projection,
            selection: "date_added > ?",
            selectionArgs: [afterTimestamp.ToString()],
            sortOrder: "date_added ASC");

        if (cursor is null)
            return result;

        int idIdx = cursor.GetColumnIndexOrThrow("_id");
        int nameIdx = cursor.GetColumnIndexOrThrow("_display_name");
        int dateIdx = cursor.GetColumnIndexOrThrow("date_added");

        while (cursor.MoveToNext())
        {
            var id = cursor.GetLong(idIdx);
            var name = cursor.GetString(nameIdx) ?? $"media_{id}";
            var date = cursor.GetLong(dateIdx);
            result.Add(($"{mediaStoreUri}/{id}", name, date));
        }

        return result;
    }

    /// <summary>
    /// Computes a quick fingerprint for deduplication: SHA-256 of (first 4 KB + file size + dateAdded).
    /// This is a practical balance — full-file hashing of large videos is expensive on-device,
    /// while first-4KB + metadata is near-unique for consumer photos.
    /// </summary>
    private static string ComputeFingerprint(MemoryStream stream, long fileSize, long dateAdded)
    {
        var position = stream.Position;
        try
        {
            // Read up to 4 KB from the start of the stream.
            var sampleSize = (int)Math.Min(4096, stream.Length);
            var buffer = new byte[sampleSize + 8 + 8];
            stream.Position = 0;
            stream.ReadExactly(buffer, 0, sampleSize);

            // Append file size (8 bytes)
            BitConverter.GetBytes(fileSize).CopyTo(buffer, sampleSize);
            // Append dateAdded timestamp (8 bytes)
            BitConverter.GetBytes(dateAdded).CopyTo(buffer, sampleSize + 8);

            var hash = SHA256.HashData(buffer.AsSpan(0, sampleSize + 16));
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            stream.Position = position;
        }
    }

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
