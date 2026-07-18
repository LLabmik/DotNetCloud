using System.Collections.ObjectModel;
using Android.Util;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for the file browser screen.</summary>
public sealed partial class FileBrowserViewModel : ObservableObject
{
    private readonly IFileRestClient _fileApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IThumbnailCache _thumbnailCache;
    private readonly IMediaAutoUploadService _autoUploadService;
    private readonly ILogger<FileBrowserViewModel> _logger;

    private readonly Stack<(Guid? FolderId, string Name)> _navigationStack = new();
    private CancellationTokenSource? _thumbnailLoadCts;

    /// <summary>Initializes a new <see cref="FileBrowserViewModel"/>.</summary>
    public FileBrowserViewModel(
        IFileRestClient fileApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        IThumbnailCache thumbnailCache,
        IMediaAutoUploadService autoUploadService,
        ILogger<FileBrowserViewModel> logger)
    {
        _fileApi = fileApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _thumbnailCache = thumbnailCache;
        _autoUploadService = autoUploadService;
        _logger = logger;

        _navigationStack.Push((null, "My Files"));
    }

    /// <summary>All visible file/folder items, bound to the UI.</summary>
    public ObservableCollection<FileItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoadError))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoadError))]
    private string? _errorMessage;

    /// <summary>True when a load attempt has finished and failed (not while still loading).</summary>
    public bool ShowInitialLoadError => !IsLoading && !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    private bool _hasCompletedInitialLoad;

    /// <summary>Whether the page is currently visible. Prevents background loads from setting ErrorMessage after the page disappears.</summary>
    internal bool IsActive { get; set; }

    [ObservableProperty]
    private string _currentFolderName = "My Files";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    private string? _uploadFileName;

    // ── Auto-upload status ──────────────────────────────────────────

    /// <summary>Status text for the auto-upload banner (e.g. "Watching for new photos", "Uploading 3 of 12").</summary>
    [ObservableProperty]
    private string _autoUploadStatusText = string.Empty;

    /// <summary>Whether auto-upload is enabled in settings.</summary>
    [ObservableProperty]
    private bool _isAutoUploadEnabled;

    /// <summary>Last auto-upload timestamp, formatted for display.</summary>
    [ObservableProperty]
    private string _lastUploadTimestampText = string.Empty;

    // ── Quota ────────────────────────────────────────────────────────

    [ObservableProperty]
    private long _quotaUsedBytes;

    [ObservableProperty]
    private long _quotaTotalBytes;

    [ObservableProperty]
    private string _quotaDisplayText = string.Empty;

    [ObservableProperty]
    private double _quotaPercentage;

    // ── Navigation ───────────────────────────────────────────────────

    /// <summary>Current folder ID (null = root).</summary>
    private Guid? CurrentFolderId => _navigationStack.Count > 0 ? _navigationStack.Peek().FolderId : null;

    /// <summary>Breadcrumb trail from root to current folder.</summary>
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [new(null, "My Files")];

    /// <summary>Loads file items for the current folder.</summary>
    [RelayCommand]
    private async Task LoadFilesAsync(CancellationToken ct)
    {
        RefreshAutoUploadStatus();
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // On cold start the first HTTP request may timeout while the connection pool
            // warms up. Retry silently so the error label never flashes before data arrives.
            var maxAttempts = HasCompletedInitialLoad ? 1 : 3;
            Exception? lastException = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                        await Task.Delay(800, ct);

                    var (serverUrl, token) = await GetCredentialsAsync(ct);
                    var items = await FetchWithRetryAsync(
                        () => _fileApi.ListChildrenAsync(serverUrl, token, CurrentFolderId, ct), ct);

                    // Sort: folders first, then by name
                    var sorted = items
                        .OrderByDescending(i => string.Equals(i.NodeType, "Folder", StringComparison.OrdinalIgnoreCase))
                        .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    Items.Clear();

                    // Pin the AutoUpload folder at the top when browsing root.
                    if (CurrentFolderId is null)
                    {
                        var uploadFolderName = Preferences.Default.Get(
                            SettingsViewModel.PrefUploadFolderName,
                            SettingsViewModel.DefaultUploadFolderName);
                        var pinned = sorted.FirstOrDefault(f =>
                            string.Equals(f.Name, uploadFolderName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(f.NodeType, "Folder", StringComparison.OrdinalIgnoreCase));
                        if (pinned is not null)
                        {
                            Items.Add(new FileItemViewModel(pinned) { IsPinned = true });
                            sorted.Remove(pinned);
                        }

                        // Show local pending uploads as virtual file items at the top.
                        var pendingDir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "PendingUploads");
                        if (Directory.Exists(pendingDir))
                        {
                            foreach (var pendingFile in Directory.GetFiles(pendingDir))
                            {
                                var fileName = System.IO.Path.GetFileName(pendingFile);
                                var fileInfo = new System.IO.FileInfo(pendingFile);
                                Items.Add(new FileItemViewModel(
                                    new FileItem(
                                        Id: Guid.Empty,
                                        Name: fileName,
                                        NodeType: "File",
                                        Size: fileInfo.Length,
                                        MimeType: GuessMimeType(fileName),
                                        ParentId: null,
                                        UpdatedAt: fileInfo.LastWriteTimeUtc,
                                        ChildCount: 0))
                                { IsPending = true });
                            }
                        }
                    }

                    foreach (var item in sorted)
                        Items.Add(new FileItemViewModel(item));

                    HasCompletedInitialLoad = true;

                    // Load thumbnails in background
                    LoadThumbnailsAsync(serverUrl, token);

                    // Load quota in background
                    _ = LoadQuotaAsync(serverUrl, token, ct);
                    return;
                }
                catch (Exception ex) when ((ex is TaskCanceledException or OperationCanceledException) && Items.Count > 0)
                {
                    _logger.LogDebug(ex, "Transient timeout during file reload; keeping existing data.");
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxAttempts)
                        _logger.LogDebug(ex, "Initial load attempt {Attempt} of {MaxAttempts} failed; retrying.", attempt, maxAttempts);
                }
            }

            if (lastException is not null)
            {
                if (IsActive)
                {
                    _logger.LogError(lastException, "Failed to load files.");
                    ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(lastException);
                }
                else
                {
                    _logger.LogDebug(lastException, "Load failed while page inactive; suppressing error display.");
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Navigates into a folder or opens a file for download.</summary>
    [RelayCommand]
    private async Task SelectItemAsync(FileItemViewModel item, CancellationToken ct)
    {
        if (string.Equals(item.NodeType, "Folder", StringComparison.OrdinalIgnoreCase))
        {
            _navigationStack.Push((item.Id, item.Name));
            CurrentFolderName = item.Name;
            CanGoBack = _navigationStack.Count > 1;
            UpdateBreadcrumbs();
            await LoadFilesAsync(ct);
        }
        else if (item.IsImage)
        {
            // Pass IDs as strings — Shell's query attribute system uses Convert.ChangeType
            // which cannot handle Guid directly.
            await Shell.Current.GoToAsync("ImageViewer", new Dictionary<string, object>
            {
                ["NodeId"] = item.Id.ToString(),
                ["FolderId"] = CurrentFolderId?.ToString() ?? ""
            });
        }
        else
        {
            await DownloadAndOpenFileAsync(item, ct);
        }
    }

    /// <summary>Navigates back to the parent folder.</summary>
    [RelayCommand]
    private async Task GoBackAsync(CancellationToken ct)
    {
        if (_navigationStack.Count <= 1)
            return;

        _navigationStack.Pop();
        var current = _navigationStack.Peek();
        CurrentFolderName = current.Name;
        CanGoBack = _navigationStack.Count > 1;
        UpdateBreadcrumbs();
        await LoadFilesAsync(ct);
    }

    /// <summary>Navigates to a specific breadcrumb in the path.</summary>
    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(BreadcrumbItem crumb, CancellationToken ct)
    {
        // Pop the stack until we reach the target breadcrumb
        while (_navigationStack.Count > 0 && _navigationStack.Peek().FolderId != crumb.FolderId)
            _navigationStack.Pop();

        if (_navigationStack.Count == 0)
            _navigationStack.Push((null, "My Files"));

        var current = _navigationStack.Peek();
        CurrentFolderName = current.Name;
        CanGoBack = _navigationStack.Count > 1;
        UpdateBreadcrumbs();
        await LoadFilesAsync(ct);
    }

    /// <summary>Rebuilds the breadcrumb collection from the current navigation stack.</summary>
    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        foreach (var entry in _navigationStack.Reverse())
            Breadcrumbs.Add(new BreadcrumbItem(entry.FolderId, entry.Name));
    }

    /// <summary>Prompts the user to create a new folder in the current directory.</summary>
    [RelayCommand]
    private async Task CreateFolderAsync(CancellationToken ct)
    {
        var name = await Shell.Current.DisplayPromptAsync(
            "New Folder", "Enter folder name:", accept: "Create", cancel: "Cancel");

        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            await _fileApi.CreateFolderAsync(serverUrl, token, name.Trim(), CurrentFolderId, ct);
            await LoadFilesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder.");
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
    }

    /// <summary>Picks a file from the device and uploads it to the current folder.</summary>
    [RelayCommand]
    private async Task UploadFileAsync(CancellationToken ct)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a file to upload"
            });

            if (result is null)
                return;

            await UploadPickedFileAsync(result, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload failed.");
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
        finally
        {
            IsUploading = false;
            UploadFileName = null;
            UploadProgress = 0;
        }
    }

    /// <summary>Captures a photo with the camera and uploads it immediately.</summary>
    [RelayCommand]
    private async Task CapturePhotoAsync(CancellationToken ct)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Shell.Current.DisplayAlertAsync("Not Supported", "Camera capture is not available on this device.", "OK");
                return;
            }

            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlertAsync("Permission Denied", "Camera permission is required to take photos. Please grant camera access in your device settings.", "OK");
                    return;
                }
            }

            // Request location permission so the camera app can embed GPS in EXIF.
            await RequestLocationPermissionAsync();

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null)
                return;

            await UploadMediaFileAsync(photo, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Photo capture upload failed.");
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
        finally
        {
            IsUploading = false;
            UploadFileName = null;
            UploadProgress = 0;
        }
    }

    /// <summary>Captures a video with the camera and uploads it immediately.</summary>
    [RelayCommand]
    private async Task CaptureVideoAsync(CancellationToken ct)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Shell.Current.DisplayAlertAsync("Not Supported", "Video capture is not available on this device.", "OK");
                return;
            }

            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlertAsync("Permission Denied", "Camera permission is required to record videos. Please grant camera access in your device settings.", "OK");
                    return;
                }
            }

            // Request location permission so the camera app can embed GPS in EXIF.
            await RequestLocationPermissionAsync();

            var video = await MediaPicker.Default.CaptureVideoAsync();
            if (video is null)
                return;

            await UploadMediaFileAsync(video, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video capture upload failed.");
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
        finally
        {
            IsUploading = false;
            UploadFileName = null;
            UploadProgress = 0;
        }
    }

    /// <summary>Deletes a file or folder after confirmation.</summary>
    [RelayCommand]
    private async Task DeleteItemAsync(FileItemViewModel item, CancellationToken ct)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete", $"Move \"{item.Name}\" to trash?", "Delete", "Cancel");

        if (!confirm)
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            await _fileApi.DeleteAsync(serverUrl, token, item.Id, ct);
            Items.Remove(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {NodeName}.", item.Name);
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private async Task UploadPickedFileAsync(FileResult picked, CancellationToken ct)
    {
        IsUploading = true;
        UploadFileName = picked.FileName;
        UploadProgress = 0;

        var (serverUrl, token) = await GetCredentialsAsync(ct);

        using var stream = await picked.OpenReadAsync();

        // Get file size — stream.Length may not work for all content URIs
        long fileSize;
        if (stream.CanSeek)
        {
            fileSize = stream.Length;
        }
        else
        {
            using var tempMs = new MemoryStream();
            await stream.CopyToAsync(tempMs, ct);
            fileSize = tempMs.Length;
            tempMs.Position = 0;
            await UploadStreamAsync(serverUrl, token, picked.FileName, tempMs, fileSize, picked.ContentType, CurrentFolderId, ct);
            return;
        }

        await UploadStreamAsync(serverUrl, token, picked.FileName, stream, fileSize, picked.ContentType, CurrentFolderId, ct);
    }

    private async Task UploadMediaFileAsync(FileResult media, CancellationToken ct)
    {
        IsUploading = true;
        UploadProgress = 0;

        var generatedName = BuildAndroidStyleFileName(media);
        UploadFileName = generatedName;

        // Always save the captured photo to a local pending uploads directory.
        // This ensures no data leaves the device unless auto-upload is enabled.
        var (uploadStream, fileSize) = await ReadMediaStreamAsync(media, ct);
        await SaveToPendingUploadsAsync(generatedName, uploadStream, ct);

        // Only upload to the server if auto-upload is enabled.
        if (!Preferences.Default.Get(SettingsViewModel.PrefEnabled, false))
        {
            uploadStream.Dispose();
            _logger.LogInformation("Captured {FileName} saved locally (auto-upload is off).", generatedName);
            return;
        }

        uploadStream.Position = 0;
        var (serverUrl, token) = await GetCredentialsAsync(ct);

        Guid? targetFolderId;
        try
        {
            targetFolderId = await _autoUploadService.ResolveUploadTargetFolderAsync(
                serverUrl, token, timestamp: DateTime.UtcNow, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve AutoUpload target folder; falling back to current folder.");
            targetFolderId = CurrentFolderId;
        }

        await UploadStreamAsync(serverUrl, token, generatedName, uploadStream, fileSize, media.ContentType, targetFolderId, ct);

        // Upload succeeded — remove from local pending queue.
        var pendingPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "PendingUploads", generatedName);
        if (File.Exists(pendingPath))
        {
            try
            { File.Delete(pendingPath); }
            catch { /* Best effort */ }
        }
    }

    /// <summary>
    /// Saves media bytes to the local pending uploads queue directory.
    /// Photos appear in the Files tab with a ☁️ Pending badge and will be
    /// uploaded when auto-upload is enabled.
    /// </summary>
    private static async Task SaveToPendingUploadsAsync(string fileName, Stream data, CancellationToken ct)
    {
        var pendingDir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "PendingUploads");
        Directory.CreateDirectory(pendingDir);
        var filePath = System.IO.Path.Combine(pendingDir, fileName);

        using var fileStream = File.Create(filePath);
        data.Position = 0;
        await data.CopyToAsync(fileStream, ct);
    }

    private static string GuessMimeType(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" or ".heif" => "image/heif",
            ".mp4" => "video/mp4",
            _ => "image/jpeg"
        };
    }

    /// <summary>
    /// Reads a <see cref="FileResult"/> stream into memory, returning the stream and its length.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    private static async Task<(Stream Stream, long Length)> ReadMediaStreamAsync(FileResult media, CancellationToken ct)
    {
        var stream = await media.OpenReadAsync();
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        stream.Dispose();
        ms.Position = 0;
        return (ms, ms.Length);
    }

    /// <summary>
    /// Requests location permission if not already granted. The camera app checks
    /// whether the calling app has location permission before embedding GPS in EXIF.
    /// This is best-effort — the user can deny without blocking the capture.
    /// </summary>
    private static async Task RequestLocationPermissionAsync()
    {
        if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
        {
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }
    }

    /// <summary>
    /// Builds an Android-style filename like <c>IMG_20260706_120000.jpg</c> or
    /// <c>VID_20260706_120000.mp4</c> based on the media content type and the
    /// current timestamp, preserving the original file extension.
    /// </summary>
    private static string BuildAndroidStyleFileName(FileResult media)
    {
        var extension = Path.GetExtension(media.FileName);
        var prefix = media.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true
            ? "VID"
            : "IMG";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{prefix}_{timestamp}{extension}";
    }

    /// <summary>
    /// Refreshes the auto-upload status properties from preferences.
    /// Called on every page load so the banner always reflects the current state.
    /// </summary>
    private void RefreshAutoUploadStatus()
    {
        var prefs = Preferences.Default;
        var enabled = prefs.Get("media_upload_enabled", false);
        IsAutoUploadEnabled = enabled;

        // Count pending files in the local upload queue.
        var pendingDir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "PendingUploads");
        var pendingCount = Directory.Exists(pendingDir)
            ? Directory.GetFiles(pendingDir).Length
            : 0;

        if (!enabled)
        {
            AutoUploadStatusText = pendingCount > 0
                ? $"📤 {pendingCount} pending upload(s) — enable auto-upload to sync"
                : "Auto-upload paused";
            LastUploadTimestampText = string.Empty;
            return;
        }

        // Show the last upload timestamp if available.
        var lastPhotoTs = prefs.Get("media_upload_last_photo_ts", 0L);
        var lastVideoTs = prefs.Get("media_upload_last_video_ts", 0L);
        var latestTs = Math.Max(lastPhotoTs, lastVideoTs);

        if (latestTs > 0)
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(latestTs).LocalDateTime;
            LastUploadTimestampText = $"Last upload: {dt:MMM dd, yyyy h:mm tt}";
        }
        else
        {
            LastUploadTimestampText = string.Empty;
        }

        var status = _autoUploadService.IsRunning ? "Watching for new photos..." : "Auto-upload idle";
        if (pendingCount > 0)
            status = $"📤 {pendingCount} pending upload(s)";
        AutoUploadStatusText = status;
    }

    private async Task UploadStreamAsync(
        string serverUrl, string token,
        string fileName, Stream stream, long fileSize, string? mimeType,
        Guid? parentId,
        CancellationToken ct)
    {
        var progress = new Progress<FileTransferProgress>(p =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UploadProgress = p.TotalBytes > 0 ? (double)p.BytesTransferred / p.TotalBytes : 0;
            });
        });

        await _fileApi.UploadFileAsync(
            serverUrl, token, fileName, parentId,
            stream, fileSize, mimeType, progress, ct);

        await LoadFilesAsync(ct);
    }

    private async Task DownloadAndOpenFileAsync(FileItemViewModel item, CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);

            using var stream = await _fileApi.DownloadAsync(serverUrl, token, item.Id, ct);

            var downloadsPath = System.IO.Path.Combine(
                FileSystem.CacheDirectory, "downloads");
            Directory.CreateDirectory(downloadsPath);
            var localPath = System.IO.Path.Combine(downloadsPath, item.Name);

            using (var fileStream = File.Create(localPath))
            {
                await stream.CopyToAsync(fileStream, ct);
            }

            _logger.LogInformation("Downloaded {FileName} to {Path}.", item.Name, localPath);

            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(localPath)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {FileName}.", item.Name);
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
    }

    private async Task LoadQuotaAsync(string serverUrl, string token, CancellationToken ct)
    {
        try
        {
            var quota = await _fileApi.GetQuotaAsync(serverUrl, token, ct);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                QuotaUsedBytes = quota.UsedBytes;
                QuotaTotalBytes = quota.TotalBytes;
                QuotaPercentage = quota.TotalBytes > 0 ? (double)quota.UsedBytes / quota.TotalBytes : 0;
                QuotaDisplayText = quota.TotalBytes > 0
                    ? $"{FormatSize(quota.UsedBytes)} of {FormatSize(quota.TotalBytes)} used"
                    : $"{FormatSize(quota.UsedBytes)} used";
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load quota.");
        }
    }

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }

    private static async Task<T> FetchWithRetryAsync<T>(Func<Task<T>> fetchFunc, CancellationToken ct)
    {
        try
        {
            return await fetchFunc();
        }
        catch (Exception ex) when ((ex is TaskCanceledException or OperationCanceledException) && !ct.IsCancellationRequested)
        {
            // Single silent retry for transient timeout (not explicit cancellation)
            await Task.Delay(500, ct);
            return await fetchFunc();
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    /// <summary>
    /// Loads thumbnails for image items in the current folder listing.
    /// Uses a semaphore to limit concurrent downloads and cancels if the user navigates away.
    /// </summary>
    private async void LoadThumbnailsAsync(string serverUrl, string token)
    {
        // Cancel any previous thumbnail load operation
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts = new CancellationTokenSource();
        var ct = _thumbnailLoadCts.Token;

        var imageItems = Items.Where(i => i.IsImage && !i.IsPending).ToList();
        Log.Warn("DotNetCloud", $"LoadThumbnailsAsync: found {imageItems.Count} image items out of {Items.Count} total");
        if (imageItems.Count == 0)
            return;

        // Load thumbnails one at a time
        foreach (var item in imageItems)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                Log.Info("DotNetCloud", $"LoadThumbnailsAsync: fetching thumbnail for {item.Id} ({item.Name})");
                var source = await _thumbnailCache.GetThumbnailAsync(item.Id, serverUrl, token, ct);
                if (source is not null)
                {
                    Log.Info("DotNetCloud", $"LoadThumbnailsAsync: got thumbnail for {item.Id}");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        item.Thumbnail = source;
                    });
                }
                else
                {
                    Log.Warn("DotNetCloud", $"LoadThumbnailsAsync: GetThumbnailAsync returned null for {item.Id}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"LoadThumbnailsAsync: exception for {item.Id}: {ex.Message}");
                _logger.LogDebug(ex, "Failed to load thumbnail for {FileId}", item.Id);
            }
        }
    }
}

/// <summary>ViewModel for a single file or folder item in the browser.</summary>
public sealed partial class FileItemViewModel : ObservableObject
{
    /// <summary>Initializes a new <see cref="FileItemViewModel"/> from a <see cref="FileItem"/>.</summary>
    public FileItemViewModel(FileItem item)
    {
        Id = item.Id;
        Name = item.Name;
        NodeType = item.NodeType;
        Size = item.Size;
        MimeType = item.MimeType;
        UpdatedAt = item.UpdatedAt;
        ChildCount = item.ChildCount;
        IsFolder = string.Equals(item.NodeType, "Folder", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Thumbnail image source for image files.</summary>
    [ObservableProperty]
    private ImageSource? _thumbnail;

    /// <summary>Whether this item is an image file with a supported MIME type.</summary>
    public bool IsImage => MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Whether this item is pinned at the top (e.g. the AutoUpload folder).</summary>
    public bool IsPinned { get; set; }

    /// <summary>Whether this item is a local pending upload (not yet on server).</summary>
    public bool IsPending { get; set; }

    /// <summary>Node ID.</summary>
    public Guid Id { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>"File" or "Folder".</summary>
    public string NodeType { get; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; }

    /// <summary>MIME type.</summary>
    public string? MimeType { get; }

    /// <summary>Last modified timestamp.</summary>
    public DateTime UpdatedAt { get; }

    /// <summary>Child count for folders.</summary>
    public int ChildCount { get; }

    /// <summary>Whether this item is a folder.</summary>
    public bool IsFolder { get; }

    /// <summary>Icon glyph for display.</summary>
    public string Icon => IsPending ? "☁️" : IsFolder ? "📁" : GetFileIcon(Name, MimeType);

    /// <summary>Formatted file size string.</summary>
    public string SizeDisplay => IsFolder
        ? (ChildCount == 1 ? "1 item" : $"{ChildCount} items")
        : FormatSize(Size);

    /// <summary>Formatted date string.</summary>
    public string DateDisplay => UpdatedAt.ToLocalTime().ToString("MMM d, yyyy");

    private static string GetFileIcon(string name, string? mimeType)
    {
        if (mimeType is not null)
        {
            if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return "🖼️";
            if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return "🎬";
            if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                return "🎵";
            if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return "📝";
            if (mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                return "📕";
            if (mimeType.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Contains("compressed", StringComparison.OrdinalIgnoreCase))
                return "📦";
        }

        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "📕",
            ".doc" or ".docx" => "📘",
            ".xls" or ".xlsx" => "📊",
            ".ppt" or ".pptx" => "📙",
            ".zip" or ".tar" or ".gz" or ".7z" or ".rar" => "📦",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => "🖼️",
            ".mp4" or ".mkv" or ".avi" or ".webm" or ".mov" => "🎬",
            ".mp3" or ".flac" or ".ogg" or ".wav" => "🎵",
            ".cs" or ".js" or ".ts" or ".py" or ".java" => "💻",
            ".md" or ".txt" or ".log" => "📝",
            _ => "📄"
        };
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

/// <summary>Represents a single segment in the breadcrumb navigation trail.</summary>
/// <param name="FolderId">Folder ID (null for root).</param>
/// <param name="Name">Display name.</param>
public sealed record BreadcrumbItem(Guid? FolderId, string Name);
