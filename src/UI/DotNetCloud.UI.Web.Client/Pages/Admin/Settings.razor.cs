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

/// <summary>Code-behind for the <see cref="Settings"/> component.</summary>
public partial class Settings : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private bool _actionInProgress;
    private bool _restartBannerDismissed;
    private string _moduleFilter = string.Empty;
    private IReadOnlyList<SystemSettingDto> _settings = [];
    private OidcKeyRotationStatus? _rotationStatus;
    private SystemSettingDto? _editing;
    private string _editValue = string.Empty;
    private string _editDescription = string.Empty;
    private ConfirmDialog _confirmDialog = default!;

    // Dedicated "Online indicators" presence idle-threshold field
    // (system setting dotnetcloud.core / PresenceIdleTimeoutMinutes).
    private const string PresenceModule = "dotnetcloud.core";
    private const string PresenceIdleTimeoutKey = "PresenceIdleTimeoutMinutes";
    private const int PresenceIdleMinutesDefault = 3;
    private const int PresenceIdleMinutesMin = 1;
    private const int PresenceIdleMinutesMax = 60;

    private int _presenceIdleMinutes = PresenceIdleMinutesDefault;

    protected override async Task OnInitializedAsync()
    {
        await LoadSettingsAsync();
        await LoadOidcKeyRotationStatusAsync();
        await LoadPresenceIdleAsync();
    }

    private async Task LoadPresenceIdleAsync()
    {
        try
        {
            var setting = await Api.GetSettingAsync(PresenceModule, PresenceIdleTimeoutKey);
            _presenceIdleMinutes = int.TryParse(setting?.Value, out var value)
                ? Math.Clamp(value, PresenceIdleMinutesMin, PresenceIdleMinutesMax)
                : PresenceIdleMinutesDefault;
        }
        catch
        {
            _presenceIdleMinutes = PresenceIdleMinutesDefault;
        }
    }

    private async Task SavePresenceIdleAsync()
    {
        var minutes = Math.Clamp(_presenceIdleMinutes, PresenceIdleMinutesMin, PresenceIdleMinutesMax);
        _presenceIdleMinutes = minutes;
        _actionInProgress = true;
        try
        {
            var dto = new UpsertSystemSettingDto
            {
                Value = minutes.ToString(),
                Description = "Presence idle threshold in minutes — a connected user with no real interaction for this long shows as Away/yellow (clamp 1-60)"
            };

            if (await Api.UpsertSettingAsync(PresenceModule, PresenceIdleTimeoutKey, dto))
            {
                Toast.ShowSuccess($"Online indicators idle threshold set to {minutes} minute(s).");
            }
            else
            {
                Toast.ShowError("Failed to save the online indicators setting.");
            }
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task LoadOidcKeyRotationStatusAsync()
    {
        try
        {
            _rotationStatus = await Api.GetOidcKeyRotationStatusAsync();
        }
        catch
        {
            _rotationStatus = null;
        }
    }

    private async Task LoadSettingsAsync()
    {
        _loading = true;
        try
        {
            _settings = await Api.ListSettingsAsync(
                string.IsNullOrWhiteSpace(_moduleFilter) ? null : _moduleFilter);
        }
        catch
        {
            Toast.ShowError("Failed to load settings.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void EditSetting(SystemSettingDto setting)
    {
        _editing = setting;
        _editValue = setting.Value;
        _editDescription = setting.Description ?? string.Empty;
    }

    private void CancelEdit() => _editing = null;

    private async Task SaveSettingAsync()
    {
        if (_editing is null) return;
        _actionInProgress = true;
        var dto = new UpsertSystemSettingDto
        {
            Value = _editValue,
            Description = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription
        };
        if (await Api.UpsertSettingAsync(_editing.Module, _editing.Key, dto))
        {
            Toast.ShowSuccess("Setting updated.");
            _editing = null;
            await LoadSettingsAsync();
        }
        else Toast.ShowError("Failed to update setting.");
        _actionInProgress = false;
    }

    private async Task DeleteSettingAsync(SystemSettingDto setting)
    {
        if (!await _confirmDialog.ShowAsync($"Delete setting '{setting.Module}.{setting.Key}'?", "Delete Setting"))
            return;
        _actionInProgress = true;
        if (await Api.DeleteSettingAsync(setting.Module, setting.Key))
        {
            Toast.ShowSuccess("Setting deleted.");
            await LoadSettingsAsync();
        }
        else Toast.ShowError("Failed to delete setting.");
        _actionInProgress = false;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? string.Concat(value.AsSpan(0, maxLength), "...") : value;

    private async Task RotateOidcKeysAsync()
    {
        if (!await _confirmDialog.ShowAsync(
                "Rotate OpenIddict signing and encryption keys now? Existing keys will be backed up. " +
                "New keys take effect on the next server restart.",
                "Rotate OIDC Keys"))
            return;

        _actionInProgress = true;
        try
        {
            var result = await Api.RotateOidcKeysAsync();
            if (result is null)
            {
                Toast.ShowError("Failed to rotate OIDC keys.");
            }
            else
            {
                var backup = string.IsNullOrEmpty(result.BackupPath) ? "(no previous keys)" : $" Backup: {result.BackupPath}";
                Toast.ShowSuccess($"OIDC keys rotated. {result.Note}{backup}");

                // Refresh the banner state — a restart is now pending.
                _restartBannerDismissed = false;
                await LoadOidcKeyRotationStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Failed to rotate OIDC keys: {ex.Message}");
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task RestartServerAsync()
    {
        if (!await _confirmDialog.ShowAsync(
                "Restart the DotNetCloud server now? The service will stop and restart in a few seconds. " +
                "All connected clients will briefly disconnect.",
                "Restart Server"))
            return;

        _actionInProgress = true;
        try
        {
            var ok = await Api.RestartServerAsync();
            if (ok)
            {
                Toast.ShowSuccess("Restart requested. The server will restart in a few seconds...");
            }
            else
            {
                Toast.ShowError("Failed to request restart.");
            }
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Failed to request restart: {ex.Message}");
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private void DismissRestartBanner() => _restartBannerDismissed = true;
}
