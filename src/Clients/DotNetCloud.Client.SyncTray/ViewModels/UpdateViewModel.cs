using System.Diagnostics;
using System.Threading;
using System.Windows.Input;
using DotNetCloud.Client.Core.Services;
using DotNetCloud.Client.SyncTray.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// Event args for restart-required notification.
/// </summary>
public sealed class RestartRequiredEventArgs : EventArgs
{
    /// <summary>Path to the downloaded update archive.</summary>
    public required string DownloadedFilePath { get; init; }
}

/// <summary>
/// View-model for the Update dialog.  Displays current vs. latest version,
/// release notes, and drives a two-step update flow: download first, then a
/// separate "restart to apply" action.
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
    private bool _isApplying;
    private double _downloadProgress;
    private string _statusMessage = string.Empty;
    private string? _downloadedFilePath;
    private string? _downloadedFileName;
    private long _downloadedSizeBytes;
    private string _downloadSpeedText = string.Empty;
    private ReleaseAsset? _platformAsset;
    private CancellationTokenSource? _downloadCts;

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
        private set
        {
            if (SetProperty(ref _isUpdateAvailable, value))
                OnPropertyChanged(nameof(CanDownload));
        }
    }

    /// <summary>Whether the "Download" action should be shown (update available, not yet downloaded).</summary>
    public bool CanDownload => IsUpdateAvailable && !IsDownloadComplete;

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
        private set
        {
            if (SetProperty(ref _isDownloading, value))
                OnPropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>Whether an update apply is in progress.</summary>
    public bool IsApplying
    {
        get => _isApplying;
        private set
        {
            if (SetProperty(ref _isApplying, value))
                OnPropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>Whether any long-running operation (check/download/apply) is in progress.</summary>
    public bool IsBusy => IsChecking || IsDownloading || IsApplying;

    /// <summary>Download progress from 0 to 100.</summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, value);
    }

    /// <summary>Human-readable download speed (e.g., "12.3 MB/s").</summary>
    public string DownloadSpeedText
    {
        get => _downloadSpeedText;
        private set => SetProperty(ref _downloadSpeedText, value);
    }

    /// <summary>Status message displayed in the dialog.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Whether a download has completed and is ready to apply.</summary>
    public bool IsDownloadComplete => !string.IsNullOrWhiteSpace(_downloadedFilePath);

    /// <summary>Full path of the downloaded update file.</summary>
    public string? DownloadedFilePath
    {
        get => _downloadedFilePath;
        private set
        {
            if (SetProperty(ref _downloadedFilePath, value))
            {
                OnPropertyChanged(nameof(IsDownloadComplete));
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }

    /// <summary>File name of the downloaded update file.</summary>
    public string? DownloadedFileName
    {
        get => _downloadedFileName;
        private set => SetProperty(ref _downloadedFileName, value);
    }

    /// <summary>Human-readable size of the downloaded file.</summary>
    public string DownloadedSizeText => FormatBytes(_downloadedSizeBytes);

    /// <summary>The best-matching platform asset for this machine, if any.</summary>
    public ReleaseAsset? PlatformAsset
    {
        get => _platformAsset;
        private set => SetProperty(ref _platformAsset, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Checks for updates.</summary>
    public ICommand CheckForUpdatesCommand { get; }

    /// <summary>Downloads the update (download only; apply is a separate step).</summary>
    public ICommand DownloadUpdateCommand { get; }

    /// <summary>Cancels an in-progress download.</summary>
    public ICommand CancelDownloadCommand { get; }

    /// <summary>Applies the downloaded update and restarts the application.</summary>
    public ICommand ApplyUpdateCommand { get; }

    /// <summary>Opens the release page on GitHub.</summary>
    public ICommand OpenReleasePageCommand { get; }

    /// <summary>Closes the dialog.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>Whether the dialog should close (bound by the view).</summary>
    public bool ShouldClose { get; private set; }

    /// <summary>
    /// Raised after the update has been applied and the application should restart.
    /// </summary>
    public event EventHandler<RestartRequiredEventArgs>? RestartRequired;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="UpdateViewModel"/>.</summary>
    /// <param name="updateService">Update service.</param>
    /// <param name="backgroundService">Background update checker.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="downloadedFilePath">Optional path of an update already downloaded in the background.</param>
    /// <param name="downloadedVersion">Optional version of the already-downloaded update.</param>
    public UpdateViewModel(
        IClientUpdateService updateService,
        UpdateCheckBackgroundService backgroundService,
        ILogger<UpdateViewModel> logger,
        string? downloadedFilePath = null,
        string? downloadedVersion = null)
    {
        _updateService = updateService;
        _backgroundService = backgroundService;
        _logger = logger;

        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        DownloadUpdateCommand = new AsyncRelayCommand(DownloadUpdateAsync);
        CancelDownloadCommand = new RelayCommand(CancelDownload);
        ApplyUpdateCommand = new AsyncRelayCommand(ApplyUpdateAsync);
        OpenReleasePageCommand = new RelayCommand(OpenReleasePage);
        CloseCommand = new RelayCommand(RequestClose);

        // Pre-populate from the latest background check result.
        var cached = _backgroundService.LatestCheckResult;
        if (cached is not null)
            ApplyCheckResult(cached);

        // Surface an update that was already downloaded in the background.
        if (!string.IsNullOrWhiteSpace(downloadedFilePath) &&
            (downloadedVersion is null ||
             cached is null ||
             string.Equals(cached.LatestVersion, downloadedVersion, StringComparison.OrdinalIgnoreCase)))
        {
            SetDownloadedFile(downloadedFilePath);
        }
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

        if (IsDownloading)
            return;

        _downloadCts = new CancellationTokenSource();
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadSpeedText = string.Empty;
        DownloadedFilePath = null;
        DownloadedFileName = null;
        _downloadedSizeBytes = 0;
        OnPropertyChanged(nameof(DownloadedSizeText));
        StatusMessage = $"Downloading {PlatformAsset.Name}…";

        try
        {
            var progress = new Progress<DownloadProgress>(OnDownloadProgress);
            var result = await _updateService.DownloadUpdateAsync(
                PlatformAsset,
                GetDownloadsDirectory(),
                progress,
                _downloadCts.Token);

            DownloadedFilePath = result.FilePath;
            DownloadedFileName = result.FileName;
            _downloadedSizeBytes = result.SizeBytes;
            OnPropertyChanged(nameof(DownloadedSizeText));
            DownloadProgress = 100;
            DownloadSpeedText = string.Empty;

            StatusMessage = result.Sha256Verified
                ? $"Downloaded {FormatBytes(result.SizeBytes)} — checksum verified."
                : $"Downloaded {FormatBytes(result.SizeBytes)} (checksum not verified — none published).";
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
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void OnDownloadProgress(DownloadProgress p)
    {
        DownloadProgress = p.Percent * 100;
        DownloadSpeedText = FormatBytes((long)p.BytesPerSecond) + "/s";

        if (p.TotalBytes is { } total && total > 0)
        {
            StatusMessage =
                $"Downloading… {FormatBytes(p.BytesDownloaded)} of {FormatBytes(total)} ({p.Percent:P0})";
        }
        else
        {
            StatusMessage = $"Downloading… {FormatBytes(p.BytesDownloaded)}";
        }
    }

    private void CancelDownload()
    {
        if (!IsDownloading || _downloadCts is null)
            return;

        StatusMessage = "Cancelling download…";
        _downloadCts.Cancel();
    }

    private async Task ApplyUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(DownloadedFilePath))
            return;

        if (IsApplying)
            return;

        IsApplying = true;
        StatusMessage = "Applying update…";
        try
        {
            await _updateService.ApplyUpdateAsync(DownloadedFilePath);

            StatusMessage = "Restarting…";
            RestartRequired?.Invoke(this, new RestartRequiredEventArgs
            {
                DownloadedFilePath = DownloadedFilePath,
            });
            ShouldClose = true;
            OnPropertyChanged(nameof(ShouldClose));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update apply failed.");
            StatusMessage = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }

    private void OpenReleasePage()
    {
        if (string.IsNullOrEmpty(ReleaseUrl))
            return;
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
        // Ignore close requests while a download or apply is in flight.
        if (IsDownloading || IsApplying)
            return;

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

    private void SetDownloadedFile(string filePath)
    {
        DownloadedFilePath = filePath;
        DownloadedFileName = Path.GetFileName(filePath);
        try
        {
            _downloadedSizeBytes = new FileInfo(filePath).Length;
        }
        catch
        {
            _downloadedSizeBytes = 0;
        }
        OnPropertyChanged(nameof(DownloadedSizeText));
        DownloadProgress = 100;
        DownloadSpeedText = string.Empty;
        StatusMessage = "Update already downloaded. Click “Restart to apply” to install it.";
    }

    private static string GetDownloadsDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Downloads", "DotNetCloud", "updates");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var order = 0;
        while (value >= 1024 && order < units.Length - 1)
        {
            order++;
            value /= 1024;
        }

        return $"{value:0.#} {units[order]}";
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "win-x64";
        if (OperatingSystem.IsMacOS())
            return "osx-x64";
        return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "linux-arm64",
            _ => "linux-x64",
        };
    }
}
