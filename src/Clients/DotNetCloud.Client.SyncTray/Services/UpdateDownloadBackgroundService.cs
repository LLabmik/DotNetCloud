using System.Runtime.InteropServices;
using DotNetCloud.Client.Core.Services;
using DotNetCloud.Client.SyncTray.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Services;

/// <summary>
/// Downloads updates in the background once a newer version is discovered.
/// The actual apply/restart step is always left to the user.
/// </summary>
public sealed class UpdateDownloadBackgroundService : IDisposable
{
    private readonly IClientUpdateService _updateService;
    private readonly UpdateCheckBackgroundService _checkService;
    private readonly SettingsViewModel _settings;
    private readonly TrayViewModel _trayVm;
    private readonly ILogger<UpdateDownloadBackgroundService> _logger;

    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="UpdateDownloadBackgroundService"/>.</summary>
    public UpdateDownloadBackgroundService(
        IClientUpdateService updateService,
        UpdateCheckBackgroundService checkService,
        SettingsViewModel settings,
        TrayViewModel trayVm,
        ILogger<UpdateDownloadBackgroundService> logger)
    {
        _updateService = updateService;
        _checkService = checkService;
        _settings = settings;
        _trayVm = trayVm;
        _logger = logger;
    }

    /// <summary>Starts listening for update-available events.</summary>
    public void Start()
    {
        if (_disposed)
            return;

        _checkService.UpdateAvailable += OnUpdateAvailable;
        _logger.LogInformation("Background update download service started.");
    }

    private void OnUpdateAvailable(object? sender, UpdateCheckResult result)
    {
        if (!result.IsUpdateAvailable)
            return;

        if (!_settings.AutoDownloadUpdates)
        {
            _logger.LogDebug("Auto-download disabled; skipping background download.");
            return;
        }

        if (_trayVm.IsUpdateDownloaded || _trayVm.IsUpdateDownloading)
        {
            _logger.LogDebug("Update already downloaded or download in progress; skipping.");
            return;
        }

        var asset = FindPlatformAsset(result);
        if (asset is null)
        {
            _logger.LogWarning("No platform asset found for background download.");
            return;
        }

        _ = DownloadInBackgroundAsync(result.LatestVersion, asset);
    }

    private async Task DownloadInBackgroundAsync(string version, ReleaseAsset asset)
    {
        _cts = new CancellationTokenSource();
        _trayVm.IsUpdateDownloading = true;
        try
        {
            var result = await _updateService.DownloadUpdateAsync(
                asset,
                GetDownloadsDirectory(),
                progress: null,
                _cts.Token);

            _trayVm.SetDownloadedUpdate(version, result.FilePath);
            _trayVm.NotifyUpdateDownloaded(version);

            _logger.LogInformation("Background update download complete: {Path}.", result.FilePath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background update download cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background update download failed.");
        }
        finally
        {
            _trayVm.IsUpdateDownloading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static string GetDownloadsDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Downloads", "DotNetCloud", "updates");
    }

    private static ReleaseAsset? FindPlatformAsset(UpdateCheckResult result)
    {
        var platform = GetCurrentPlatform();
        return result.Assets.FirstOrDefault(a =>
            string.Equals(a.Platform, platform, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "win-x64";
        if (OperatingSystem.IsMacOS())
            return "osx-x64";
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "linux-arm64",
            _ => "linux-x64",
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _checkService.UpdateAvailable -= OnUpdateAvailable;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
