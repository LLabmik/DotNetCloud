using Android.Content;
using AndroidX.Core.App;
using DotNetCloud.Client.Android.Auth;
using AndroidUri = global::Android.Net.Uri;
using AndroidConnectivityManager = global::Android.Net.ConnectivityManager;
using AndroidTransportType = global::Android.Net.TransportType;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Periodically scans the device's MediaStore for new photos and uploads them to the
/// active DotNetCloud server using the chunked upload protocol.
/// Respects WiFi-only and enabled/disabled preferences.
/// </summary>
internal sealed class PhotoAutoUploadService : IPhotoAutoUploadService
{
    private const string PrefEnabled = "photo_upload_enabled";
    private const string PrefWifiOnly = "photo_upload_wifi_only";
    private const string PrefLastUploadAt = "photo_upload_last_ts";
    private const int NotificationId = 3000;
    private const int MaxChunkSize = 4 * 1024 * 1024;

    private readonly IServerConnectionStore _connectionStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly HttpClient _http;
    private readonly ILogger<PhotoAutoUploadService> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(15);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    /// <inheritdoc />
    public bool IsRunning => _loopCts is not null && !_loopCts.IsCancellationRequested;

    /// <summary>Initializes a new <see cref="PhotoAutoUploadService"/>.</summary>
    public PhotoAutoUploadService(
        IServerConnectionStore connectionStore,
        ISecureTokenStore tokenStore,
        HttpClient http,
        ILogger<PhotoAutoUploadService> logger)
    {
        _connectionStore = connectionStore;
        _tokenStore = tokenStore;
        _http = http;
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
        => UploadNewPhotosAsync(cancellationToken);

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UploadNewPhotosAsync(ct).ConfigureAwait(false);
                await Task.Delay(_scanInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo auto-upload scan failed.");
                await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            }
        }
    }

    private async Task UploadNewPhotosAsync(CancellationToken ct)
    {
        if (!Preferences.Default.Get(PrefEnabled, false))
            return;

        if (Preferences.Default.Get(PrefWifiOnly, true) && !IsOnWifi())
        {
            _logger.LogDebug("Photo auto-upload skipped — not on WiFi.");
            return;
        }

        var connection = _connectionStore.GetActive();
        if (connection is null)
            return;

        var accessToken = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            .ConfigureAwait(false);
        if (accessToken is null)
            return;

        var lastTimestamp = Preferences.Default.Get(PrefLastUploadAt, 0L);
        var photos = QueryNewPhotosSince(lastTimestamp);

        if (photos.Count == 0)
            return;

        _logger.LogInformation("Found {Count} new photo(s) to upload.", photos.Count);

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

        foreach (var (contentUri, fileName, dateAdded) in photos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await UploadPhotoAsync(connection.ServerBaseUrl, accessToken, contentUri, fileName, ct)
                    .ConfigureAwait(false);

                Preferences.Default.Set(PrefLastUploadAt, dateAdded);
                uploaded++;

                // NotificationCompat.Builder fluent setters return Builder? in the AndroidX binding
                // even though the real Java API is @NonNull. The chain is always safe here.
#pragma warning disable CS8602
                var notification = new NotificationCompat.Builder(appContext, MainApplication.ChannelIdUpload)
                    .SetSmallIcon(global::Android.Resource.Drawable.IcMenuGallery)
                    .SetContentTitle("Uploading photos")
                    .SetContentText($"{uploaded} of {photos.Count} uploaded")
                    .SetProgress(photos.Count, uploaded, false)
                    .SetOngoing(true)
                    .Build()!;
#pragma warning restore CS8602
                nm.Notify(NotificationId, notification);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload photo {FileName}.", fileName);
            }
        }

        nm.Cancel(NotificationId);
    }

    private async Task UploadPhotoAsync(
        string serverBaseUrl,
        string accessToken,
        string contentUri,
        string fileName,
        CancellationToken ct)
    {
        var resolver = Platform.AppContext?.ContentResolver;
        if (resolver is null)
        {
            _logger.LogWarning("ContentResolver is null; cannot upload photo.");
            return;
        }

        byte[] data;

        var uri = AndroidUri.Parse(contentUri);
        if (uri is null)
        {
            _logger.LogWarning("Failed to parse content URI: {Uri}", contentUri);
            return;
        }

        using (var inputStream = resolver.OpenInputStream(uri))
        {
            if (inputStream is null)
            {
                _logger.LogWarning("Failed to open input stream for URI: {Uri}", contentUri);
                return;
            }

            using var ms = new MemoryStream();
            await inputStream.CopyToAsync(ms, ct).ConfigureAwait(false);
            data = ms.ToArray();
        }

        // Split using fixed 4 MB chunks; compute per-chunk SHA-256
        var chunks = SplitIntoChunks(data);
        var chunkHashes = chunks.Select(c => Convert.ToHexStringLower(SHA256.HashData(c))).ToList();

        var baseUrl = serverBaseUrl.TrimEnd('/');
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 1. Initiate upload session
        var initiatePayload = new
        {
            fileName,
            totalSize = (long)data.Length,
            mimeType = "image/jpeg",
            chunkHashes
        };
        using var initiateResp = await _http
            .PostAsJsonAsync($"{baseUrl}/api/v1/files/upload/initiate", initiatePayload, ct)
            .ConfigureAwait(false);
        initiateResp.EnsureSuccessStatusCode();

        var initiateResult = await initiateResp.Content
            .ReadFromJsonAsync<InitiateUploadResult>(cancellationToken: ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty initiate response.");

        var existing = new HashSet<string>(
            initiateResult.ExistingChunks ?? [], StringComparer.OrdinalIgnoreCase);

        // 2. Upload missing chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            var hash = chunkHashes[i];
            if (existing.Contains(hash))
                continue;

            using var chunkContent = new ByteArrayContent(chunks[i]);
            chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var chunkResp = await _http
                .PutAsync($"{baseUrl}/api/v1/files/upload/{initiateResult.SessionId}/chunks/{hash}", chunkContent, ct)
                .ConfigureAwait(false);
            chunkResp.EnsureSuccessStatusCode();
        }

        // 3. Complete upload
        using var completeResp = await _http
            .PostAsync($"{baseUrl}/api/v1/files/upload/{initiateResult.SessionId}/complete", null, ct)
            .ConfigureAwait(false);
        completeResp.EnsureSuccessStatusCode();

        _logger.LogInformation("Uploaded photo {FileName} ({Bytes} bytes).", fileName, data.Length);
    }

    private static List<(string Uri, string FileName, long DateAdded)> QueryNewPhotosSince(long afterTimestamp)
    {
        var result = new List<(string, string, long)>();
        var resolver = Platform.AppContext?.ContentResolver;
        if (resolver is null)
            return result;

        var mediaUri = AndroidUri.Parse("content://media/external/images/media");
        if (mediaUri is null)
            return result;

        var projection = new[] { "_id", "_display_name", "date_added" };

        using var cursor = resolver.Query(
            mediaUri, projection,
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
            var name = cursor.GetString(nameIdx) ?? $"photo_{id}.jpg";
            var date = cursor.GetLong(dateIdx);
            result.Add(($"content://media/external/images/media/{id}", name, date));
        }

        return result;
    }

    private static List<byte[]> SplitIntoChunks(byte[] data)
    {
        var chunks = new List<byte[]>();
        int offset = 0;
        while (offset < data.Length)
        {
            int size = Math.Min(MaxChunkSize, data.Length - offset);
            var chunk = new byte[size];
            Array.Copy(data, offset, chunk, 0, size);
            chunks.Add(chunk);
            offset += size;
        }
        return chunks.Count > 0 ? chunks : [data];
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

    // ── Private DTOs ────────────────────────────────────────────────────────

    private sealed record InitiateUploadResult(
        [property: JsonPropertyName("sessionId")] string SessionId,
        [property: JsonPropertyName("existingChunks")] IReadOnlyList<string>? ExistingChunks);
}
