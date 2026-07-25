using Android.Content;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for the settings and linked accounts screen.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    internal const string PrefEnabled = "media_upload_enabled";
    internal const string PrefWifiOnly = "media_upload_wifi_only";
    internal const string PrefOrganizeByDate = "media_upload_organize_by_date";
    internal const string PrefUploadFolderName = "media_upload_folder_name";
    internal const string PrefLastServerUrl = "last_server_url";
    internal const string PrefChargingOnly = "media_upload_charging_only";
    internal const string PrefBatteryThreshold = "media_upload_battery_threshold";
    internal const string DefaultUploadFolderName = "AutoUpload";

    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IMediaAutoUploadService _mediaUploadService;
    private readonly IBatteryOptimizationService _batteryService;
    private readonly IExactAlarmPermissionService _exactAlarmPermission;
    private readonly INotificationPermissionService _notificationPermission;
    private readonly IAppPreferences _preferences;
    private readonly IAndroidUpdateService _updateService;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>Raised when the user logs out and the app should return to login.</summary>
    public event EventHandler? LoggedOut;

    /// <summary>Initializes a new <see cref="SettingsViewModel"/>.</summary>
    public SettingsViewModel(
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        IMediaAutoUploadService mediaUploadService,
        IBatteryOptimizationService batteryService,
        IExactAlarmPermissionService exactAlarmPermission,
        INotificationPermissionService notificationPermission,
        IAppPreferences preferences,
        IAndroidUpdateService updateService,
        ILogger<SettingsViewModel> logger)
    {
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _mediaUploadService = mediaUploadService;
        _batteryService = batteryService;
        _exactAlarmPermission = exactAlarmPermission;
        _notificationPermission = notificationPermission;
        _preferences = preferences;
        _updateService = updateService;
        _logger = logger;

        var active = serverStore.GetActive();
        ServerDisplayName = active?.DisplayName ?? string.Empty;
        AccountEmail = active?.AccountEmail ?? string.Empty;
        ServerBaseUrl = active?.ServerBaseUrl ?? string.Empty;

        // Load persisted sync preferences
        _autoUploadEnabled = _preferences.Get(PrefEnabled, false);
        _wifiOnlyEnabled = _preferences.Get(PrefWifiOnly, true);
        _organizeByDate = _preferences.Get(PrefOrganizeByDate, true);
        _uploadFolderName = _preferences.Get(PrefUploadFolderName, DefaultUploadFolderName);
        _chargingOnly = _preferences.Get(PrefChargingOnly, false);
        _batteryThreshold = _preferences.Get(PrefBatteryThreshold, 20);

        RefreshBatteryStatus();
    }

    // ── Account ──────────────────────────────────────────────────────

    /// <summary>Display name of the connected server.</summary>
    [ObservableProperty]
    private string _serverDisplayName = string.Empty;

    /// <summary>Email of the logged-in account.</summary>
    [ObservableProperty]
    private string _accountEmail = string.Empty;

    /// <summary>Base URL of the active server connection.</summary>
    [ObservableProperty]
    private string _serverBaseUrl = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Whether a module rescan is currently in progress.</summary>
    [ObservableProperty]
    private bool _isRescanning;

    /// <summary>Status message shown after a module rescan attempt.</summary>
    [ObservableProperty]
    private string _rescanStatus = string.Empty;

    // ── File sync settings ───────────────────────────────────────────

    [ObservableProperty]
    private bool _autoUploadEnabled;

    [ObservableProperty]
    private bool _wifiOnlyEnabled;

    [ObservableProperty]
    private bool _organizeByDate;

    [ObservableProperty]
    private string _uploadFolderName;

    // ── Upload constraints ──────────────────────────────────────────

    [ObservableProperty]
    private bool _chargingOnly;

    [ObservableProperty]
    private int _batteryThreshold = 20;

    // ── Battery optimization ─────────────────────────────────────────

    [ObservableProperty]
    private bool _isBatteryOptimized = true;

    /// <summary>Whether exact alarm permission is denied (affects calendar reminder timing).</summary>
    [ObservableProperty]
    private bool _isExactAlarmDenied = true;

    /// <summary>Whether notification permission is denied (Android 13+) — blocks all notifications.</summary>
    [ObservableProperty]
    private bool _isNotificationDenied = true;

    [ObservableProperty]
    private string _batteryStatusText = "Checking…";

    [ObservableProperty]
    private Color _batteryStatusColor = Colors.Gray;

    // ── Sync preference change handlers ──────────────────────────────

    partial void OnAutoUploadEnabledChanged(bool value)
    {
        _preferences.Set(PrefEnabled, value);
        _logger.LogInformation("Auto-upload {State}.", value ? "enabled" : "disabled");

        if (value)
        {
            _ = _mediaUploadService.StartAsync();

            // Start the foreground service so uploads survive backgrounding.
            try
            {
                var ctx = global::Android.App.Application.Context;
                var intent = new Intent(ctx, typeof(global::DotNetCloud.Client.Android.MediaUploadForegroundService));
                intent.SetAction(global::DotNetCloud.Client.Android.MediaUploadForegroundService.ActionStart);
                ctx.StartForegroundService(intent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start media upload foreground service.");
            }
        }
        else
        {
            _ = _mediaUploadService.StopAsync();

            // Stop the foreground service to release resources.
            try
            {
                var ctx = global::Android.App.Application.Context;
                var intent = new Intent(ctx, typeof(global::DotNetCloud.Client.Android.MediaUploadForegroundService));
                intent.SetAction(global::DotNetCloud.Client.Android.MediaUploadForegroundService.ActionStop);
                ctx.StartForegroundService(intent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop media upload foreground service.");
            }
        }
    }

    partial void OnWifiOnlyEnabledChanged(bool value)
    {
        _preferences.Set(PrefWifiOnly, value);
        _logger.LogInformation("WiFi-only upload set to {Value}.", value);
    }

    partial void OnOrganizeByDateChanged(bool value)
    {
        _preferences.Set(PrefOrganizeByDate, value);
        _logger.LogInformation("Organize by date set to {Value}.", value);
    }

    partial void OnUploadFolderNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _preferences.Set(PrefUploadFolderName, value.Trim());
            _logger.LogInformation("Upload folder name set to '{FolderName}'.", value.Trim());
        }
    }

    partial void OnChargingOnlyChanged(bool value)
    {
        _preferences.Set(PrefChargingOnly, value);
        _logger.LogInformation("Charging-only upload set to {Value}.", value);
    }

    partial void OnBatteryThresholdChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        if (clamped != value)
            BatteryThreshold = clamped;
        _preferences.Set(PrefBatteryThreshold, clamped);
        _logger.LogInformation("Battery upload threshold set to {Value}%.", clamped);
    }

    // ── Commands ─────────────────────────────────────────────────────

    /// <summary>Opens the system exact alarm permission settings.</summary>
    [RelayCommand]
    private void RequestExactAlarmPermission()
    {
        _exactAlarmPermission.OpenPermissionSettings();
        // Refresh after a delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsExactAlarmDenied = !_exactAlarmPermission.HasExactAlarmPermission();
            });
        });
    }

    /// <summary>Opens the system notification permission settings (Android 13+).</summary>
    [RelayCommand]
    private void RequestNotificationPermission()
    {
        _notificationPermission.OpenNotificationSettings();
        // Refresh after a delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsNotificationDenied = !_notificationPermission.HasNotificationPermission();
            });
        });
    }

    /// <summary>Opens the system battery optimization exemption dialog.</summary>
    [RelayCommand]
    private async Task RequestBatteryExemptionAsync()
    {
        await _batteryService.RequestExemptionAsync();
        // Re-check after a short delay (user may have returned from settings)
        await Task.Delay(1000);
        RefreshBatteryStatus();
    }

    /// <summary>Prompts the user to change the upload target folder name.</summary>
    [RelayCommand]
    private async Task ChangeUploadFolderAsync()
    {
        if (Shell.Current is null)
            return;

        var name = await Shell.Current.DisplayPromptAsync(
            "Upload Folder", "Enter the server folder name for auto-uploads:",
            accept: "Save", cancel: "Cancel",
            initialValue: UploadFolderName);

        if (!string.IsNullOrWhiteSpace(name))
            UploadFolderName = name.Trim();
    }

    /// <summary>Triggers an immediate scan and upload cycle.</summary>
    [RelayCommand]
    private async Task SyncNowAsync(CancellationToken ct)
    {
        if (!AutoUploadEnabled)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync("Sync Disabled",
                    "Enable auto-upload first to sync files.", "OK");
            }
            return;
        }

        IsBusy = true;
        try
        {
            await _mediaUploadService.ScanAndUploadNowAsync(ct);
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync("Sync Complete",
                    "All new media has been uploaded.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual sync failed.");
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync("Sync Failed",
                    $"Could not complete sync: {ex.Message}", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Triggers a full rescan of optional modules on the server, updating tab visibility.</summary>
    [RelayCommand]
    private async Task RescanModulesAsync()
    {
        IsRescanning = true;
        RescanStatus = string.Empty;
        try
        {
            // Trigger module rescan if running inside a MAUI application context.
            // In test environments, Application.Current may be null — gracefully skip.
            var appType = Microsoft.Maui.Controls.Application.Current?.GetType();
            var method = appType?.GetMethod("TriggerModuleRescanAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method is not null)
            {
                var task = (Task?)method.Invoke(null, null);
                if (task is not null)
                    await task;
            }
            RescanStatus = "Modules rescanned successfully.";
            _logger.LogInformation("Manual module rescan completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Module rescan failed.");
            RescanStatus = $"Rescan failed: {ex.Message}";
        }
        finally
        {
            IsRescanning = false;
        }
    }

    /// <summary>Logs out from the current server, clears stored tokens, and raises <see cref="LoggedOut"/>.</summary>
    [RelayCommand]
    private async Task LogOutAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            if (!string.IsNullOrEmpty(ServerBaseUrl))
            {
                // Save the URL so the login page can pre-fill it
                _preferences.Set(PrefLastServerUrl, ServerBaseUrl);
                await _tokenStore.DeleteTokensAsync(ServerBaseUrl, ct);
                _serverStore.Remove(ServerBaseUrl);
            }
            _logger.LogInformation("User logged out from {ServerBaseUrl}.", ServerBaseUrl);
            LoggedOut?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    /// <summary>Refreshes battery optimization status — call when the page appears.</summary>
    public void RefreshBatteryStatus()
    {
        var exempt = _batteryService.IsIgnoringBatteryOptimizations();
        IsBatteryOptimized = !exempt;
        BatteryStatusText = exempt ? "Unrestricted" : "Restricted — tap to fix";
        BatteryStatusColor = exempt ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B");

        // Also refresh exact alarm permission
        IsExactAlarmDenied = !_exactAlarmPermission.HasExactAlarmPermission();

        // Also refresh notification permission (Android 13+)
        IsNotificationDenied = !_notificationPermission.HasNotificationPermission();
    }

    // ── Update notification ──────────────────────────────────────────

    /// <summary>App version display string (e.g. "DotNetCloud for Android v0.1.7").</summary>
    public string AppVersionText { get; } = $"DotNetCloud for Android v{GetAppVersion()}";

    /// <summary>Whether an update notification banner should be shown.</summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    /// <summary>The latest available version string (e.g. "0.2.0").</summary>
    [ObservableProperty]
    private string _updateVersion = string.Empty;

    /// <summary>Brief release notes for the available update.</summary>
    [ObservableProperty]
    private string _updateReleaseNotes = string.Empty;

    /// <summary>Release URL for the update (fallback for store link).</summary>
    [ObservableProperty]
    private string? _updateReleaseUrl;

    /// <summary>Whether a manual update check is in progress.</summary>
    [ObservableProperty]
    private bool _isCheckingForUpdate;

    /// <summary>Status message after a manual update check.</summary>
    [ObservableProperty]
    private string _updateCheckStatus = string.Empty;

    /// <summary>Checks for updates on page load (respects once-per-day and dismiss).</summary>
    public async Task CheckForUpdateOnLaunchAsync(CancellationToken ct = default)
    {
        var result = await _updateService.CheckOnLaunchAsync(ct);
        if (result is not null)
            ShowUpdateBanner(result);
    }

    /// <summary>Manually triggers an update check, bypassing once-per-day throttle.</summary>
    [RelayCommand]
    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        IsCheckingForUpdate = true;
        UpdateCheckStatus = string.Empty;
        try
        {
            // Clear the last-check date to force a fresh check.
            _preferences.Set(AndroidUpdateService.PrefLastCheckDate, string.Empty);
            var result = await _updateService.CheckOnLaunchAsync(ct);
            if (result is not null)
            {
                ShowUpdateBanner(result);
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateCheckStatus = "You're up to date!";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Manual update check failed.");
            UpdateCheckStatus = "Check failed — try again later.";
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    /// <summary>Opens the store listing or release page for the available update.</summary>
    [RelayCommand]
    private async Task OpenUpdateAsync()
    {
        await _updateService.OpenStoreListingAsync(UpdateReleaseUrl);
    }

    /// <summary>Dismisses the update banner for the current latest version.</summary>
    [RelayCommand]
    private void DismissUpdate()
    {
        if (!string.IsNullOrEmpty(UpdateVersion))
            _updateService.DismissVersion(UpdateVersion);
        IsUpdateAvailable = false;
    }

    private void ShowUpdateBanner(UpdateCheckResult result)
    {
        IsUpdateAvailable = true;
        UpdateVersion = result.LatestVersion;
        UpdateReleaseUrl = result.ReleaseUrl;

        // Show first ~200 chars of release notes as summary.
        var notes = result.ReleaseNotes ?? string.Empty;
        UpdateReleaseNotes = notes.Length > 200 ? notes[..200] + "…" : notes;
    }

    private static string GetAppVersion()
    {
        // Assembly.GetEntryAssembly() returns null on Android — fall back to
        // the SettingsViewModel assembly which is in the same APK.
        var assembly = System.Reflection.Assembly.GetEntryAssembly()
                       ?? typeof(SettingsViewModel).Assembly;
        var attr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly);
        var version = attr?.InformationalVersion ?? "0.0.0";
        var plusIdx = version.IndexOf('+');
        return plusIdx >= 0 ? version[..plusIdx] : version;
    }
}
