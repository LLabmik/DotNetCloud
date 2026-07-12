using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the full-screen image viewer.
/// Supports swipe navigation between images in the same folder,
/// full-resolution loading, and EXIF metadata display.
/// </summary>
[QueryProperty(nameof(NodeId), "NodeId")]
[QueryProperty(nameof(FolderId), "FolderId")]
public sealed partial class ImageViewerViewModel : ObservableObject
{
    private readonly IFileRestClient _fileApi;
    private readonly IThumbnailCache _thumbnailCache;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<ImageViewerViewModel> _logger;

    private List<FileItem> _folderItems = [];
    private int _currentIndex;
    private string _serverUrl = string.Empty;
    private string _accessToken = string.Empty;

    /// <summary>Initializes a new <see cref="ImageViewerViewModel"/>.</summary>
    public ImageViewerViewModel(
        IFileRestClient fileApi,
        IThumbnailCache thumbnailCache,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<ImageViewerViewModel> logger)
    {
        _fileApi = fileApi;
        _thumbnailCache = thumbnailCache;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <summary>The currently viewed file node ID (set via navigation query).</summary>
    public Guid NodeId { get; set; }

    /// <summary>The folder containing this image (set via navigation query).</summary>
    public Guid? FolderId { get; set; }

    /// <summary>Full-resolution image source for display.</summary>
    [ObservableProperty]
    private ImageSource? _source;

    /// <summary>Current file name displayed in the header.</summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>Whether the full-resolution image is loading.</summary>
    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>Whether the header/info overlay is visible.</summary>
    [ObservableProperty]
    private bool _isHeaderVisible = true;

    /// <summary>EXIF/metadata for the current image.</summary>
    [ObservableProperty]
    private MediaMetadataDto? _metadata;

    /// <summary>Whether the EXIF info panel is expanded.</summary>
    [ObservableProperty]
    private bool _isMetadataExpanded;

    /// <summary>Whether there is a previous image to swipe to.</summary>
    [ObservableProperty]
    private bool _hasPrevious;

    /// <summary>Whether there is a next image to swipe to.</summary>
    [ObservableProperty]
    private bool _hasNext;

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

    /// <summary>
    /// Called after navigation query properties are set.
    /// Loads the folder listing, then shows the requested image.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            _serverUrl = serverUrl;
            _accessToken = token;

            // Load folder listing to enable swipe navigation
            _folderItems = (await _fileApi.ListChildrenAsync(serverUrl, token, FolderId, ct))
                .Where(f => string.Equals(f.NodeType, "File", StringComparison.OrdinalIgnoreCase)
                    && f.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            _currentIndex = _folderItems.FindIndex(f => f.Id == NodeId);
            if (_currentIndex < 0)
                _currentIndex = 0;

            await LoadCurrentImageAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize image viewer for {NodeId}", NodeId);
            await Shell.Current.DisplayAlertAsync("Error", "Could not load image.", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    /// <summary>Loads the full-resolution image and metadata for the current index.</summary>
    private async Task LoadCurrentImageAsync(CancellationToken ct)
    {
        IsLoading = true;
        IsMetadataExpanded = false;
        Metadata = null;

        try
        {
            var item = _folderItems[_currentIndex];
            FileName = item.Name;
            HasPrevious = _currentIndex > 0;
            HasNext = _currentIndex < _folderItems.Count - 1;

            // Load full-resolution image
            using var stream = await _fileApi.DownloadAsync(_serverUrl, _accessToken, item.Id, ct);
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            ms.Position = 0;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Source = ImageSource.FromStream(() => new MemoryStream(ms.ToArray()));
                IsLoading = false;
            });

            // Load metadata in background
            _ = LoadMetadataAsync(item.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load image at index {Index}", _currentIndex);
            IsLoading = false;
        }
    }

    private async Task LoadMetadataAsync(Guid nodeId, CancellationToken ct)
    {
        try
        {
            var metadata = await _fileApi.GetFileMetadataAsync(_serverUrl, _accessToken, nodeId, ct);
            if (metadata is not null)
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

    /// <summary>Navigates to the previous image in the folder.</summary>
    [RelayCommand]
    private async Task NavigatePreviousAsync(CancellationToken ct)
    {
        if (_currentIndex <= 0)
            return;
        _currentIndex--;
        await LoadCurrentImageAsync(ct);
    }

    /// <summary>Navigates to the next image in the folder.</summary>
    [RelayCommand]
    private async Task NavigateNextAsync(CancellationToken ct)
    {
        if (_currentIndex >= _folderItems.Count - 1)
            return;
        _currentIndex++;
        await LoadCurrentImageAsync(ct);
    }

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
        await Shell.Current.GoToAsync("..");
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
