using Android.Content;
using AndroidX.Core.App;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using AndroidUri = global::Android.Net.Uri;
using AndroidConnectivityManager = global::Android.Net.ConnectivityManager;
using AndroidTransportType = global::Android.Net.TransportType;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Periodically scans the device's MediaStore for new photos and videos, uploading them
/// to the active DotNetCloud server using the chunked upload protocol via <see cref="IFileRestClient"/>.
/// Organises uploads into an <c>InstantUpload/YYYY/MM</c> folder hierarchy by default.
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
    private const string DefaultUploadFolderName = "InstantUpload";

    private readonly IServerConnectionStore _connectionStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IFileRestClient _fileApi;
    private readonly ILogger<MediaAutoUploadService> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(15);

    // Cached folder IDs so we don't re-create folders on every upload.
    private Guid? _rootFolderId;
    private (int Year, int Month, Guid Id)? _cachedMonthFolder;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    /// <inheritdoc />
    public bool IsRunning => _loopCts is not null && !_loopCts.IsCancellationRequested;

    /// <summary>Initializes a new <see cref="MediaAutoUploadService"/>.</summary>
    public MediaAutoUploadService(
        IServerConnectionStore connectionStore,
        ISecureTokenStore tokenStore,
        IFileRestClient fileApi,
        ILogger<MediaAutoUploadService> logger)
    {
        _connectionStore = connectionStore;
        _tokenStore = tokenStore;
        _fileApi = fileApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        _loopCts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_loopCts is null)
            return;

        await _loopCts.CancelAsync().ConfigureAwait(false);
        try { await (_loopTask ?? Task.CompletedTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _loopCts.Dispose();
        _loopCts = null;
    }

    /// <inheritdoc />
    public Task ScanAndUploadNowAsync(CancellationToken cancellationToken = default)
        => UploadNewMediaAsync(cancellationToken);

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UploadNewMediaAsync(ct).ConfigureAwait(false);
                await Task.Delay(_scanInterval, ct).ConfigureAwait(false);
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
            return;

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

        nm.Cancel(NotificationId);
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

        var result = await _fileApi.UploadFileAsync(
            serverBaseUrl, accessToken,
            fileName, parentId,
            ms, ms.Length, mimeType,
            progress: null, ct).ConfigureAwait(false);

        _logger.LogInformation("Uploaded {FileName} ({Bytes} bytes) to {FolderName}.",
            fileName, ms.Length, parentId.HasValue ? "date folder" : "root");
    }

    /// <summary>
    /// Ensures the <c>InstantUpload/YYYY/MM</c> folder chain exists on the server and
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
}
