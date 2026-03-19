using System.Collections.ObjectModel;
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
    private readonly ILogger<FileBrowserViewModel> _logger;

    private readonly Stack<(Guid? FolderId, string Name)> _navigationStack = new();

    /// <summary>Initializes a new <see cref="FileBrowserViewModel"/>.</summary>
    public FileBrowserViewModel(
        IFileRestClient fileApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<FileBrowserViewModel> logger)
    {
        _fileApi = fileApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
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
                    foreach (var item in sorted)
                        Items.Add(new FileItemViewModel(item));

                    HasCompletedInitialLoad = true;

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
            await Shell.Current.DisplayAlert("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
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
            await Shell.Current.DisplayAlert("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
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
                await Shell.Current.DisplayAlert("Not Supported", "Camera capture is not available on this device.", "OK");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is null)
                return;

            await UploadMediaFileAsync(photo, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Photo capture upload failed.");
            await Shell.Current.DisplayAlert("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
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
                await Shell.Current.DisplayAlert("Not Supported", "Video capture is not available on this device.", "OK");
                return;
            }

            var video = await MediaPicker.Default.CaptureVideoAsync();
            if (video is null)
                return;

            await UploadMediaFileAsync(video, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video capture upload failed.");
            await Shell.Current.DisplayAlert("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
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
        var confirm = await Shell.Current.DisplayAlert(
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
            await Shell.Current.DisplayAlert("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
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
            await UploadStreamAsync(serverUrl, token, picked.FileName, tempMs, fileSize, picked.ContentType, ct);
            return;
        }

        await UploadStreamAsync(serverUrl, token, picked.FileName, stream, fileSize, picked.ContentType, ct);
    }

    private async Task UploadMediaFileAsync(FileResult media, CancellationToken ct)
    {
        IsUploading = true;
        UploadFileName = media.FileName;
        UploadProgress = 0;

        var (serverUrl, token) = await GetCredentialsAsync(ct);

        using var stream = await media.OpenReadAsync();
        long fileSize = stream.CanSeek ? stream.Length : 0;

        if (fileSize == 0)
        {
            // Copy to memory for non-seekable streams
            using var tempMs = new MemoryStream();
            await stream.CopyToAsync(tempMs, ct);
            fileSize = tempMs.Length;
            tempMs.Position = 0;
            await UploadStreamAsync(serverUrl, token, media.FileName, tempMs, fileSize, media.ContentType, ct);
            return;
        }

        await UploadStreamAsync(serverUrl, token, media.FileName, stream, fileSize, media.ContentType, ct);
    }

    private async Task UploadStreamAsync(
        string serverUrl, string token,
        string fileName, Stream stream, long fileSize, string? mimeType,
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
            serverUrl, token, fileName, CurrentFolderId,
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
            await Shell.Current.DisplayAlert("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
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
    public string Icon => IsFolder ? "📁" : GetFileIcon(Name, MimeType);

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
            if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "🖼️";
            if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "🎬";
            if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return "🎵";
            if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return "📝";
            if (mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return "📕";
            if (mimeType.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Contains("compressed", StringComparison.OrdinalIgnoreCase)) return "📦";
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
