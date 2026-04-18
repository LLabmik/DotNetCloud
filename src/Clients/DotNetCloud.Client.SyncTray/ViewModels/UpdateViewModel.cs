using System.Diagnostics;
using System.Windows.Input;
using DotNetCloud.Client.Core.Services;
using DotNetCloud.Client.SyncTray.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the Update dialog.  Displays current vs. latest version,
/// release notes, download progress, and actions.
/// </summary>
public sealed class UpdateViewModel : ViewModelBase
{
    private readonly IClientUpdateService _updateService;
    private readonly UpdateCheckBackgroundService _backgroundService;
    private readonly ILogger<UpdateViewModel> _logger;

    private string _currentVersion = string.Empty;
    private string _latestVersion = string.Empty;
    private string? _releaseNotes;
    private string? _releaseUrl;
    private DateTimeOffset? _publishedAt;
    private bool _isUpdateAvailable;
    private bool _isChecking;
    private bool _isDownloading;
    private double _downloadProgress;
    private string _statusMessage = string.Empty;
    private string? _downloadedFilePath;
    private ReleaseAsset? _platformAsset;

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>The currently running version.</summary>
    public string CurrentVersion
    {
        get => _currentVersion;
        private set => SetProperty(ref _currentVersion, value);
    }

    /// <summary>The latest available version.</summary>
    public string LatestVersion
    {
        get => _latestVersion;
        private set => SetProperty(ref _latestVersion, value);
    }

    /// <summary>Markdown release notes for the latest version.</summary>
    public string? ReleaseNotes
    {
        get => _releaseNotes;
        private set => SetProperty(ref _releaseNotes, value);
    }

    /// <summary>URL to the GitHub release page.</summary>
    public string? ReleaseUrl
    {
        get => _releaseUrl;
        private set => SetProperty(ref _releaseUrl, value);
    }

    /// <summary>When the latest release was published.</summary>
    public DateTimeOffset? PublishedAt
    {
        get => _publishedAt;
        private set => SetProperty(ref _publishedAt, value);
    }

    /// <summary>Whether a newer version is available for download.</summary>
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }

    /// <summary>Whether an update check is in progress.</summary>
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    /// <summary>Whether a download is in progress.</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        private set => SetProperty(ref _isDownloading, value);
    }

    /// <summary>Download progress from 0 to 100.</summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, value);
    }

    /// <summary>Status message displayed in the dialog.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Whether a download has completed and is ready to apply.</summary>
    public bool IsDownloadComplete => _downloadedFilePath is not null;

    /// <summary>The best-matching platform asset for this machine, if any.</summary>
    public ReleaseAsset? PlatformAsset
    {
        get => _platformAsset;
        private set => SetProperty(ref _platformAsset, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Checks for updates.</summary>
    public ICommand CheckForUpdatesCommand { get; }

    /// <summary>Downloads the update.</summary>
    public ICommand DownloadUpdateCommand { get; }

    /// <summary>Opens the release page on GitHub.</summary>
    public ICommand OpenReleasePageCommand { get; }

    /// <summary>Closes the dialog.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>Whether the dialog should close (bound by the view).</summary>
    public bool ShouldClose { get; private set; }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="UpdateViewModel"/>.</summary>
    public UpdateViewModel(
        IClientUpdateService updateService,
        UpdateCheckBackgroundService backgroundService,
        ILogger<UpdateViewModel> logger)
    {
        _updateService = updateService;
        _backgroundService = backgroundService;
        _logger = logger;

        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        DownloadUpdateCommand = new AsyncRelayCommand(DownloadUpdateAsync);
        OpenReleasePageCommand = new RelayCommand(OpenReleasePage);
        CloseCommand = new RelayCommand(RequestClose);

        // Pre-populate from the latest background check result.
        var cached = _backgroundService.LatestCheckResult;
        if (cached is not null)
            ApplyCheckResult(cached);
    }

    // ── Actions ───────────────────────────────────────────────────────────

    private async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        StatusMessage = "Checking for updates…";
        try
        {
            var result = await _backgroundService.CheckAsync();
            ApplyCheckResult(result);
            StatusMessage = result.IsUpdateAvailable
                ? $"Version {result.LatestVersion} is available!"
                : "You are running the latest version.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual update check failed.");
            StatusMessage = "Update check failed. Try again later.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task DownloadUpdateAsync()
    {
        if (PlatformAsset is null)
        {
            StatusMessage = "No download available for this platform.";
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = $"Downloading {PlatformAsset.Name}…";

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p * 100;
                StatusMessage = $"Downloading… {p:P0}";
            });

            _downloadedFilePath = await _updateService.DownloadUpdateAsync(PlatformAsset, progress);
            StatusMessage = "Download complete. Restart to apply the update.";
            OnPropertyChanged(nameof(IsDownloadComplete));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update download failed.");
            StatusMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private void OpenReleasePage()
    {
        if (string.IsNullOrEmpty(ReleaseUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ReleaseUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open release page in browser.");
        }
    }

    private void RequestClose()
    {
        ShouldClose = true;
        OnPropertyChanged(nameof(ShouldClose));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ApplyCheckResult(UpdateCheckResult result)
    {
        CurrentVersion = result.CurrentVersion;
        LatestVersion = result.LatestVersion;
        ReleaseNotes = result.ReleaseNotes;
        ReleaseUrl = result.ReleaseUrl;
        PublishedAt = result.PublishedAt;
        IsUpdateAvailable = result.IsUpdateAvailable;

        // Find the best asset for the current platform.
        var platform = GetCurrentPlatform();
        PlatformAsset = result.Assets.FirstOrDefault(a =>
            string.Equals(a.Platform, platform, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "win-x64";
        if (OperatingSystem.IsMacOS()) return "osx-x64";
        return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "linux-arm64",
            _ => "linux-x64",
        };
    }
}
