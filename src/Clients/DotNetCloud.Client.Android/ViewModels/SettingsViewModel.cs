using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for the settings and linked accounts screen.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    internal const string PrefEnabled = "media_upload_enabled";
    internal const string PrefWifiOnly = "media_upload_wifi_only";
    internal const string PrefOrganizeByDate = "media_upload_organize_by_date";
    internal const string PrefUploadFolderName = "media_upload_folder_name";
    internal const string DefaultUploadFolderName = "InstantUpload";

    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IMediaAutoUploadService _mediaUploadService;
    private readonly IBatteryOptimizationService _batteryService;
    private readonly IAppPreferences _preferences;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>Raised when the user logs out and the app should return to login.</summary>
    public event EventHandler? LoggedOut;

    /// <summary>Initializes a new <see cref="SettingsViewModel"/>.</summary>
    public SettingsViewModel(
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        IMediaAutoUploadService mediaUploadService,
        IBatteryOptimizationService batteryService,
        IAppPreferences preferences,
        ILogger<SettingsViewModel> logger)
    {
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _mediaUploadService = mediaUploadService;
        _batteryService = batteryService;
        _preferences = preferences;
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

    // ── File sync settings ───────────────────────────────────────────

    [ObservableProperty]
    private bool _autoUploadEnabled;

    [ObservableProperty]
    private bool _wifiOnlyEnabled;

    [ObservableProperty]
    private bool _organizeByDate;

    [ObservableProperty]
    private string _uploadFolderName;

    // ── Battery optimization ─────────────────────────────────────────

    [ObservableProperty]
    private bool _isBatteryOptimized = true;

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
            _ = _mediaUploadService.StartAsync();
        else
            _ = _mediaUploadService.StopAsync();
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

    // ── Commands ─────────────────────────────────────────────────────

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
            await Shell.Current.DisplayAlert("Sync Disabled",
                "Enable auto-upload first to sync files.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            await _mediaUploadService.ScanAndUploadNowAsync(ct);
            await Shell.Current.DisplayAlert("Sync Complete",
                "All new media has been uploaded.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual sync failed.");
            await Shell.Current.DisplayAlert("Sync Failed",
                $"Could not complete sync: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
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
    }
}
