using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>Code-behind for the <see cref="BackupSettings"/> component.</summary>
public partial class BackupSettings : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private const string Module = "dotnetcloud.core";

    private bool _loading = true;
    private bool _saving;
    private bool _runningBackup;
    private BackupResult? _lastBackupResult;
    private BackupStatusInfo? _backupStatus;
    private ConfirmDialog _confirmDialog = default!;

    // Form fields
    private bool _enabled;
    private string _schedule = "daily";
    private TimeOnly _backupTimeUtc = new(2, 0);
    private string _backupDayOfWeek = "Sunday";
    private string _backupDirectory = string.Empty;
    private int _retentionCount = 7;
    private bool _includeDatabase = true;
    private bool _includeFileStorage = true;
    private bool _includeModuleData = true;
    private bool _encryptBackups;
    private string _encryptionPassphrase = string.Empty;
    private bool _notifyOnSuccess;
    private bool _notifyOnFailure = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _loading = true;
        try
        {
            var settings = await Api.ListSettingsAsync(Module);
            PopulateFields(settings);
        }
        catch
        {
            Toast.ShowError("Failed to load backup settings.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateFields(IReadOnlyList<SystemSettingDto> settings)
    {
        _enabled = GetBool(settings, "Backup:Enabled");
        _schedule = GetString(settings, "Backup:Schedule", "daily");
        var timeStr = GetString(settings, "Backup:TimeUtc", "02:00");
        if (TimeOnly.TryParse(timeStr, out var t))
            _backupTimeUtc = t;
        _backupDayOfWeek = GetString(settings, "Backup:DayOfWeek", "Sunday");
        _backupDirectory = GetString(settings, "Backup:Directory", "/var/lib/dotnetcloud/backups");
        _retentionCount = GetInt(settings, "Backup:RetentionCount", 7);
        _includeDatabase = GetBool(settings, "Backup:IncludeDatabase", defaultValue: true);
        _includeFileStorage = GetBool(settings, "Backup:IncludeFileStorage", defaultValue: true);
        _includeModuleData = GetBool(settings, "Backup:IncludeModuleData", defaultValue: true);
        _encryptBackups = GetBool(settings, "Backup:Encrypt");
        _encryptionPassphrase = GetString(settings, "Backup:EncryptionPassphrase");
        _notifyOnSuccess = GetBool(settings, "Backup:NotifyOnSuccess");
        _notifyOnFailure = GetBool(settings, "Backup:NotifyOnFailure", defaultValue: true);
    }

    private async Task SaveAsync()
    {
        if (_encryptBackups && _encryptionPassphrase.Length < 12)
        {
            Toast.ShowError("Encryption passphrase must be at least 12 characters.");
            return;
        }

        if (_retentionCount < 1)
        {
            Toast.ShowError("Retention count must be at least 1.");
            return;
        }

        _saving = true;
        try
        {
            var tasks = new[]
            {
                Api.UpsertSettingAsync(Module, "Backup:Enabled", Str(_enabled.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:Schedule", Str(_schedule)),
                Api.UpsertSettingAsync(Module, "Backup:TimeUtc", Str(_backupTimeUtc.ToString("HH:mm"))),
                Api.UpsertSettingAsync(Module, "Backup:DayOfWeek", Str(_backupDayOfWeek)),
                Api.UpsertSettingAsync(Module, "Backup:Directory", Str(_backupDirectory)),
                Api.UpsertSettingAsync(Module, "Backup:RetentionCount", Str(_retentionCount.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:IncludeDatabase", Str(_includeDatabase.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:IncludeFileStorage", Str(_includeFileStorage.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:IncludeModuleData", Str(_includeModuleData.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:Encrypt", Str(_encryptBackups.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:EncryptionPassphrase",
                    new UpsertSystemSettingDto { Value = _encryptionPassphrase, IsSensitive = true }),
                Api.UpsertSettingAsync(Module, "Backup:NotifyOnSuccess", Str(_notifyOnSuccess.ToString())),
                Api.UpsertSettingAsync(Module, "Backup:NotifyOnFailure", Str(_notifyOnFailure.ToString()))
            };

            var results = await Task.WhenAll(tasks);
            if (results.All(r => r))
                Toast.ShowSuccess("Backup settings saved.");
            else
                Toast.ShowError("Some settings could not be saved.");
        }
        catch
        {
            Toast.ShowError("Failed to save backup settings.");
        }
        finally
        {
            _saving = false;
        }
    }

    // ---- Setting helpers ----

    private static string GetString(IReadOnlyList<SystemSettingDto> settings, string key, string defaultValue = "")
    {
        var s = settings.FirstOrDefault(x => x.Key == key);
        return s?.Value ?? defaultValue;
    }

    private static bool GetBool(IReadOnlyList<SystemSettingDto> settings, string key, bool defaultValue = false)
    {
        var val = GetString(settings, key);
        return string.IsNullOrEmpty(val) ? defaultValue : bool.TryParse(val, out var b) ? b : defaultValue;
    }

    private static int GetInt(IReadOnlyList<SystemSettingDto> settings, string key, int defaultValue = 0)
    {
        var val = GetString(settings, key);
        return string.IsNullOrEmpty(val) ? defaultValue : int.TryParse(val, out var i) ? i : defaultValue;
    }

    private static UpsertSystemSettingDto Str(string value, string? description = null) =>
        new() { Value = value, Description = description };

    private async Task RunBackupNowAsync()
    {
        if (_runningBackup) return;

        _runningBackup = true;
        _lastBackupResult = null;
        try
        {
            var result = await Api.RunBackupAsync();
            _lastBackupResult = result;
            if (result?.Success == true)
            {
                Toast.ShowSuccess($"Backup completed: {result.FilePath}");
            }
            else
            {
                Toast.ShowError($"Backup failed: {result?.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            _lastBackupResult = new BackupResult { Success = false, ErrorMessage = ex.Message };
            Toast.ShowError($"Backup failed: {ex.Message}");
        }
        finally
        {
            _runningBackup = false;
            _backupStatus = await Api.GetBackupStatusAsync();
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
