using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// A single page in the carousel-based image viewer.
/// Holds the loaded image source and loading state.
/// </summary>
public sealed partial class ImageCarouselItem : ObservableObject
{
    /// <summary>Initializes a new carousel item for the given file.</summary>
    public ImageCarouselItem(Guid fileId, string fileName)
    {
        FileId = fileId;
        FileName = fileName;
    }

    /// <summary>File node ID.</summary>
    public Guid FileId { get; }

    /// <summary>Display name.</summary>
    public string FileName { get; }

    /// <summary>Loaded full-resolution image source.</summary>
    [ObservableProperty]
    private ImageSource? _source;

    /// <summary>Whether this item is still loading its image.</summary>
    [ObservableProperty]
    private bool _isItemLoading = true;
}

/// <summary>
/// ViewModel for the full-screen image viewer powered by a <see cref="CarouselView"/>
/// for smooth native swipe transitions.
/// Pre-loads adjacent images so swiping feels instant.
/// </summary>
public sealed partial class ImageViewerViewModel : ObservableObject, IQueryAttributable
{
    private readonly IFileRestClient _fileApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<ImageViewerViewModel> _logger;

    private List<FileItem> _folderItems = [];
    private string _serverUrl = string.Empty;
    private string _accessToken = string.Empty;
    private Guid _nodeId;
    private Guid? _folderId;
    private CancellationTokenSource? _loadCts;

    // ── URL mode (chat images) ───────────────────────────────────────
    private bool _isUrlMode;
    private string[] _imageUrls = [];
    private string[] _imageNames = [];
    private string _startUrl = string.Empty;

    /// <summary>Initializes a new <see cref="ImageViewerViewModel"/>.</summary>
    public ImageViewerViewModel(
        IFileRestClient fileApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<ImageViewerViewModel> logger)
    {
        _fileApi = fileApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("NodeId", out var nodeId) && nodeId is string nodeIdStr
            && Guid.TryParse(nodeIdStr, out var parsedNodeId))
        {
            _nodeId = parsedNodeId;
        }

        if (query.TryGetValue("FolderId", out var folderId) && folderId is string folderIdStr
            && Guid.TryParse(folderIdStr, out var parsedFolderId))
        {
            _folderId = parsedFolderId;
        }

        // URL mode (chat images) — a pipe-separated list of image URLs
        if (query.TryGetValue("ImageUrls", out var urlsObj) && urlsObj is string urlsStr
            && !string.IsNullOrWhiteSpace(urlsStr))
        {
            _imageUrls = urlsStr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _isUrlMode = _imageUrls.Length > 0;
        }

        // Optional display names, one per URL, same pipe-separated order
        if (query.TryGetValue("ImageNames", out var namesObj) && namesObj is string namesStr
            && !string.IsNullOrWhiteSpace(namesStr))
        {
            _imageNames = namesStr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Optional start URL — the initially selected image
        if (query.TryGetValue("StartUrl", out var startObj) && startObj is string startStr)
        {
            _startUrl = startStr;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await InitializeCommand.ExecuteAsync(null);
        });
    }

    // ── Carousel ────────────────────────────────────────────────────

    /// <summary>Carousel items — one per image in the current folder.</summary>
    public ObservableCollection<ImageCarouselItem> CarouselItems { get; } = [];

    /// <summary>Current carousel position (two-way bound to CarouselView.Position).</summary>
    [ObservableProperty]
    private int _currentPosition = -1;

    /// <summary>Whether the entire viewer is still loading the initial image.</summary>
    [ObservableProperty]
    private bool _isViewerLoading = true;

    /// <summary>Whether the header/info overlay is visible.</summary>
    [ObservableProperty]
    private bool _isHeaderVisible = true;

    /// <summary>Current file name displayed in the header.</summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>Current position indicator text (e.g. "3 / 15").</summary>
    [ObservableProperty]
    private string _positionText = string.Empty;

    // ── Metadata ─────────────────────────────────────────────────────

    /// <summary>EXIF/metadata for the current image.</summary>
    [ObservableProperty]
    private MediaMetadataDto? _metadata;

    /// <summary>Whether the EXIF info panel is expanded.</summary>
    [ObservableProperty]
    private bool _isMetadataExpanded;

    /// <summary>Whether the metadata panel shows any data.</summary>
    public bool HasMetadata => Metadata is not null;

    /// <summary>Formatted dimensions string (e.g. "4032 × 3024").</summary>
    public string? DimensionsDisplay => Metadata?.Width is not null && Metadata?.Height is not null
        ? $"{Metadata.Width} × {Metadata.Height}"
        : null;

    /// <summary>Formatted camera string (e.g. "Canon EOS R5").</summary>
    public string? CameraDisplay => (Metadata?.CameraMake, Metadata?.CameraModel) switch
    {
        (string make, string model) => $"{make} {model}",
        (string make, null) => make,
        (null, string model) => model,
        _ => null
    };

    /// <summary>Formatted lens string.</summary>
    public string? LensDisplay => Metadata?.LensModel;

    /// <summary>Formatted camera settings (e.g. "f/2.8 · 1/250 · ISO 100 · 35mm").</summary>
    public string? SettingsDisplay
    {
        get
        {
            var m = Metadata;
            if (m is null)
                return null;
            var parts = new List<string>();
            if (m.Aperture.HasValue)
                parts.Add($"f/{m.Aperture.Value:F1}");
            if (!string.IsNullOrEmpty(m.ShutterSpeed))
                parts.Add(m.ShutterSpeed);
            if (m.Iso.HasValue)
                parts.Add($"ISO {m.Iso.Value}");
            if (m.FocalLengthMm.HasValue)
                parts.Add($"{m.FocalLengthMm.Value:F0}mm");
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
    }

    /// <summary>Formatted flash status.</summary>
    public string? FlashDisplay => Metadata?.FlashFired switch
    {
        true => "Flash: On",
        false => "Flash: Off",
        _ => null
    };

    /// <summary>Formatted date taken.</summary>
    public string? DateTakenDisplay => Metadata?.TakenAtUtc?.ToLocalTime().ToString("MMM d, yyyy · h:mm tt");

    /// <summary>Formatted GPS coordinates.</summary>
    public string? GpsDisplay
    {
        get
        {
            var loc = Metadata?.Location;
            if (loc is null)
                return null;
            var alt = loc.AltitudeMetres.HasValue
                ? $" · {loc.AltitudeMetres.Value:F0}m"
                : string.Empty;
            return $"{loc.Latitude:F5}, {loc.Longitude:F5}{alt}";
        }
    }

    // ── Initialization ──────────────────────────────────────────────

    /// <summary>
    /// Loads the folder listing, populates the carousel, then loads the
    /// selected image and pre-loads adjacent ones.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            // ── URL mode (chat images) ────────────────────────────────
            if (_isUrlMode)
            {
                await InitializeUrlModeAsync(ct);
                return;
            }

            // ── File mode (browser images) ────────────────────────────
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            _serverUrl = serverUrl;
            _accessToken = token;

            _folderItems = (await _fileApi.ListChildrenAsync(serverUrl, token, _folderId, ct))
                .Where(f => string.Equals(f.NodeType, "File", StringComparison.OrdinalIgnoreCase)
                    && f.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (_folderItems.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Info", "No images found in this folder.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Populate carousel items (placeholders — images load on demand)
            CarouselItems.Clear();
            foreach (var item in _folderItems)
                CarouselItems.Add(new ImageCarouselItem(item.Id, item.Name));

            var startIndex = _folderItems.FindIndex(f => f.Id == _nodeId);
            if (startIndex < 0)
                startIndex = 0;

            // Set position — this triggers OnCurrentPositionChanged which loads the image
            CurrentPosition = startIndex;
            IsViewerLoading = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize image viewer for {NodeId}", _nodeId);
            await Shell.Current.DisplayAlertAsync("Error", "Could not load image.", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    /// <summary>
    /// Initializes the viewer in URL mode (used for chat image attachments).
    /// Populates the carousel with URL-based image sources directly,
    /// without downloading through the Files API.
    /// </summary>
    private async Task InitializeUrlModeAsync(CancellationToken ct)
    {
        try
        {
            CarouselItems.Clear();

            for (var i = 0; i < _imageUrls.Length; i++)
            {
                var url = _imageUrls[i];
                var name = i < _imageNames.Length ? _imageNames[i] : $"Image {i + 1}";

                var item = new ImageCarouselItem(Guid.Empty, name);
                // Set the image source directly from the URL — MAUI Image loads it asynchronously
                item.Source = ImageSource.FromUri(new Uri(url));
                item.IsItemLoading = false;
                CarouselItems.Add(item);
            }

            if (CarouselItems.Count == 0)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Find the start index matching the start URL, default to 0
            var startIndex = 0;
            if (!string.IsNullOrEmpty(_startUrl))
            {
                var idx = Array.IndexOf(_imageUrls, _startUrl);
                if (idx >= 0)
                    startIndex = idx;
            }

            CurrentPosition = startIndex;
            IsViewerLoading = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize URL-mode image viewer.");
            await Shell.Current.GoToAsync("..");
        }
    }

    /// <summary>
    /// Called whenever <see cref="CurrentPosition"/> changes — either by user swipe
    /// or programmatic scroll. Loads the visible image and pre-loads neighbours.
    /// </summary>
    partial void OnCurrentPositionChanged(int value)
    {
        // Cancel any in-flight preloads
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        if (value < 0)
            return;

        // ── URL mode (chat images) — update header only, images are already set ──
        if (_isUrlMode)
        {
            if (value >= CarouselItems.Count)
                return;
            var urlItem = CarouselItems[value];
            FileName = urlItem.FileName;
            PositionText = $"{value + 1} / {CarouselItems.Count}";
            return;
        }

        // ── File mode ────────────────────────────────────────────────
        if (value >= _folderItems.Count)
            return;

        var item = _folderItems[value];
        FileName = item.Name;
        PositionText = $"{value + 1} / {_folderItems.Count}";
        Metadata = null;
        OnPropertyChanged(nameof(HasMetadata));
        OnPropertyChanged(nameof(DimensionsDisplay));
        OnPropertyChanged(nameof(CameraDisplay));
        OnPropertyChanged(nameof(LensDisplay));
        OnPropertyChanged(nameof(SettingsDisplay));
        OnPropertyChanged(nameof(FlashDisplay));
        OnPropertyChanged(nameof(DateTakenDisplay));
        OnPropertyChanged(nameof(GpsDisplay));

        // Load the current image + preload neighbours in the background
        _ = LoadImageAsync(value, ct);
        _ = LoadImageAsync(value - 1, ct); // previous
        _ = LoadImageAsync(value + 1, ct); // next
    }

    /// <summary>Downloads the full-resolution image for the item at <paramref name="index"/>.</summary>
    private async Task LoadImageAsync(int index, CancellationToken ct)
    {
        if (index < 0 || index >= _folderItems.Count || ct.IsCancellationRequested)
            return;

        var carouselItem = CarouselItems[index];
        if (!carouselItem.IsItemLoading)
            return; // already loaded

        try
        {
            var fileItem = _folderItems[index];
            using var stream = await _fileApi.DownloadAsync(_serverUrl, _accessToken, fileItem.Id, ct);
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            ms.Position = 0;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ct.IsCancellationRequested)
                    return;
                carouselItem.Source = ImageSource.FromStream(() => new MemoryStream(ms.ToArray()));
                carouselItem.IsItemLoading = false;
            });

            // Fetch EXIF metadata for the current (visible) image
            if (index == CurrentPosition)
            {
                _ = LoadMetadataAsync(fileItem.Id, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Swiped away — ignore
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load image at index {Index}", index);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ct.IsCancellationRequested)
                    carouselItem.IsItemLoading = false;
            });
        }
    }

    private async Task LoadMetadataAsync(Guid nodeId, CancellationToken ct)
    {
        try
        {
            var metadata = await _fileApi.GetFileMetadataAsync(_serverUrl, _accessToken, nodeId, ct);
            if (metadata is not null && !ct.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Metadata = metadata;
                    OnPropertyChanged(nameof(HasMetadata));
                    OnPropertyChanged(nameof(DimensionsDisplay));
                    OnPropertyChanged(nameof(CameraDisplay));
                    OnPropertyChanged(nameof(LensDisplay));
                    OnPropertyChanged(nameof(SettingsDisplay));
                    OnPropertyChanged(nameof(FlashDisplay));
                    OnPropertyChanged(nameof(DateTakenDisplay));
                    OnPropertyChanged(nameof(GpsDisplay));
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load metadata for {NodeId}", nodeId);
        }
    }

    // ── Commands ────────────────────────────────────────────────────

    /// <summary>Toggles the header/info overlay visibility.</summary>
    [RelayCommand]
    private void ToggleHeader() => IsHeaderVisible = !IsHeaderVisible;

    /// <summary>Toggles the EXIF metadata info panel.</summary>
    [RelayCommand]
    private void ToggleMetadata() => IsMetadataExpanded = !IsMetadataExpanded;

    /// <summary>Closes the image viewer and returns to the file list.</summary>
    [RelayCommand]
    private async Task CloseAsync()
    {
        _loadCts?.Cancel();
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Shares the current image via the system share sheet.</summary>
    [RelayCommand]
    private async Task ShareAsync()
    {
        try
        {
            if (CurrentPosition < 0 || CurrentPosition >= CarouselItems.Count)
                return;

            var item = CarouselItems[CurrentPosition];
            var fileName = item.FileName;

            byte[] data;
            if (_isUrlMode && CurrentPosition < _imageUrls.Length)
            {
                // URL mode (chat images) — download from the URL directly
                using var client = new HttpClient();
                data = await client.GetByteArrayAsync(_imageUrls[CurrentPosition]);
            }
            else if (!_isUrlMode && CurrentPosition < _folderItems.Count)
            {
                // File mode — download via the Files API
                using var stream = await _fileApi.DownloadAsync(_serverUrl, _accessToken, _folderItems[CurrentPosition].Id, CancellationToken.None);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                data = ms.ToArray();
            }
            else
            {
                return;
            }

            var tempFile = System.IO.Path.Combine(FileSystem.CacheDirectory, "share_image.jpg");
            await System.IO.File.WriteAllBytesAsync(tempFile, data);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = fileName,
                File = new ShareFile(tempFile),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share image.");
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
}
