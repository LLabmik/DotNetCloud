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

/// <summary>Code-behind for the <see cref="ModuleDetail"/> component.</summary>
public partial class ModuleDetail : ComponentBase
{
    [Parameter] public string ModuleId { get; set; } = string.Empty;
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    private ConfirmDialog _confirmDialog = default!;

    private bool _loading = true;
    private bool _actionInProgress;
    private bool _consentActionInProgress;
    private ModuleDto? _module;
    private ModuleConsentOverviewDto? _consent;
    private ModuleDataStoreDto? _dataStore;
    private ModuleCredentialDto? _credential;
    private ModuleQuotaDto? _quota;
    private IReadOnlyList<ModuleSettingDto> _settings = [];
    private IReadOnlyList<OutstandingErasure> _outstandingErasures = [];

    private bool DestroyDataOnUninstall { get; set; }
    private bool RemoveFilesOnUninstall { get; set; } = true;

    // Quota inputs (null = inherit the policy default).
    private int? QuotaMemoryLimitMb { get; set; }
    private int? QuotaCpuPercent { get; set; }
    private int? QuotaDataDirMb { get; set; }
    private int? QuotaDatabaseMb { get; set; }
    private int? QuotaConnectionLimit { get; set; }
    private int? QuotaRequestsPerMinute { get; set; }
    private int? QuotaEventsPerMinute { get; set; }
    private int? QuotaNotificationsPerMinute { get; set; }
    private int? QuotaAuditEntriesPerMinute { get; set; }

    // Module settings editor (the module settings plane; distinct from core system settings).
    private string SettingKey { get; set; } = string.Empty;
    private string SettingValue { get; set; } = string.Empty;
    private bool SettingIsSecret { get; set; }
    private string? _editingSettingKey;

    protected override async Task OnInitializedAsync()
    {
        await TimeProvider.EnsureInitializedAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _module = await Api.GetModuleAsync(ModuleId);
            _consent = await Api.GetModuleConsentAsync(ModuleId);

            if (_module is not null && !_module.IsRequired)
            {
                _dataStore = await Api.GetModuleDataStoreAsync(ModuleId);
                _credential = await Api.GetModuleCredentialAsync(ModuleId);
                _quota = await Api.GetModuleQuotaAsync(ModuleId);
                LoadQuotaInputs(_quota);
                _settings = await Api.ListModuleSettingsAsync(ModuleId);
                _outstandingErasures = await LoadOutstandingErasuresAsync();
            }
        }
        catch
        {
            Toast.ShowError("Failed to load module details.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void LoadQuotaInputs(ModuleQuotaDto? quota)
    {
        QuotaMemoryLimitMb = quota?.MemoryLimitMb;
        QuotaCpuPercent = quota?.CpuPercent;
        QuotaDataDirMb = quota?.DataDirMb;
        QuotaDatabaseMb = quota?.DatabaseMb;
        QuotaConnectionLimit = quota?.ConnectionLimit;
        QuotaRequestsPerMinute = quota?.RequestsPerMinute;
        QuotaEventsPerMinute = quota?.EventsPerMinute;
        QuotaNotificationsPerMinute = quota?.NotificationsPerMinute;
        QuotaAuditEntriesPerMinute = quota?.AuditEntriesPerMinute;
    }

    /// <summary>
    /// Finds the pending account-erasure requests that still list this module as outstanding.
    /// </summary>
    private async Task<IReadOnlyList<OutstandingErasure>> LoadOutstandingErasuresAsync()
    {
        var requests = await Api.ListErasureRequestsAsync(includeCompleted: false);
        var rows = new List<OutstandingErasure>();

        foreach (var request in requests)
        {
            var moduleRow = request.Modules.FirstOrDefault(
                m => string.Equals(m.ModuleId, ModuleId, StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(m.Status, "Acknowledged", StringComparison.OrdinalIgnoreCase));

            if (moduleRow is not null)
            {
                rows.Add(new OutstandingErasure(
                    request.RequestId, request.UserId, request.RequestedAtUtc, moduleRow.Attempts, moduleRow.LastError));
            }
        }

        return rows;
    }

    private async Task RevokeConsentAsync(Guid userId)
    {
        _consentActionInProgress = true;

        try
        {
            if (await Api.RevokeModuleConsentForUserAsync(ModuleId, userId))
            {
                Toast.ShowSuccess("Consent revoked for the user.");
                _consent = await Api.GetModuleConsentAsync(ModuleId);
            }
            else
            {
                Toast.ShowError("Failed to revoke consent.");
            }
        }
        finally
        {
            _consentActionInProgress = false;
        }
    }

    private async Task StartAsync()
    {
        _actionInProgress = true;
        if (await Api.StartModuleAsync(ModuleId))
        {
            Toast.ShowSuccess("Module started.");
            await LoadAsync();
        }
        else
            Toast.ShowError("Failed to start module.");
        _actionInProgress = false;
    }

    private async Task StopAsync()
    {
        _actionInProgress = true;
        if (await Api.StopModuleAsync(ModuleId))
        {
            Toast.ShowSuccess("Module stopped.");
            await LoadAsync();
        }
        else
            Toast.ShowError("Failed to stop module.");
        _actionInProgress = false;
    }

    private async Task RestartAsync()
    {
        _actionInProgress = true;
        if (await Api.RestartModuleAsync(ModuleId))
        {
            Toast.ShowSuccess("Module restarted.");
            await LoadAsync();
        }
        else
            Toast.ShowError("Failed to restart module.");
        _actionInProgress = false;
    }

    private async Task ProvisionDataStoreAsync()
    {
        _actionInProgress = true;
        try
        {
            var result = await Api.ProvisionModuleDataStoreAsync(ModuleId);
            if (result is { Success: true })
            {
                Toast.ShowSuccess("Data store provisioned.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError(result?.Error ?? "Provisioning failed.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task RotateDataStoreCredentialsAsync()
    {
        _actionInProgress = true;
        try
        {
            var result = await Api.RotateModuleDataStoreCredentialsAsync(ModuleId);
            if (result is { Success: true })
            {
                Toast.ShowSuccess("Credentials rotated. Restart the module to use them.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError(result?.Error ?? "Rotation failed.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task DestroyDataStoreAsync()
    {
        if (!await _confirmDialog.ShowAsync(
                $"Permanently drop the schema '{_dataStore?.SchemaName}' and principal '{_dataStore?.PrincipalName}'? All module data is lost.",
                "Destroy data store"))
        {
            return;
        }

        _actionInProgress = true;
        try
        {
            var result = await Api.DestroyModuleDataStoreAsync(ModuleId);
            if (result is { Success: true })
            {
                Toast.ShowSuccess("Data store destroyed.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError(result?.Error ?? "Destroy failed.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task RotateCredentialAsync()
    {
        _actionInProgress = true;
        try
        {
            var rotated = await Api.RotateModuleCredentialAsync(ModuleId);
            if (rotated is not null)
            {
                Toast.ShowSuccess("Launch credential rotated.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("Failed to rotate the launch credential.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task RevokeCredentialAsync()
    {
        if (!await _confirmDialog.ShowAsync(
                "Revoke the launch credential? Every call from the running module is rejected until it restarts.",
                "Revoke credential"))
        {
            return;
        }

        _actionInProgress = true;
        try
        {
            var revoked = await Api.RevokeModuleCredentialAsync(ModuleId);
            if (revoked is not null)
            {
                Toast.ShowSuccess("Launch credential revoked.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("Failed to revoke the launch credential.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task SaveQuotaAsync()
    {
        _actionInProgress = true;
        try
        {
            var dto = new UpdateModuleQuotaDto
            {
                MemoryLimitMb = QuotaMemoryLimitMb,
                CpuPercent = QuotaCpuPercent,
                DataDirMb = QuotaDataDirMb,
                DatabaseMb = QuotaDatabaseMb,
                ConnectionLimit = QuotaConnectionLimit,
                RequestsPerMinute = QuotaRequestsPerMinute,
                EventsPerMinute = QuotaEventsPerMinute,
                NotificationsPerMinute = QuotaNotificationsPerMinute,
                AuditEntriesPerMinute = QuotaAuditEntriesPerMinute,
            };

            var updated = await Api.UpdateModuleQuotaAsync(ModuleId, dto);
            if (updated is not null)
            {
                _quota = updated;
                LoadQuotaInputs(updated);
                Toast.ShowSuccess("Quota saved and applied to the rate limiter.");
            }
            else
            {
                Toast.ShowError("Failed to save the quota.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private void EditSetting(ModuleSettingDto setting)
    {
        _editingSettingKey = setting.Key;
        SettingKey = setting.Key;
        // A secret value is never returned to the browser, so the field starts empty and must be
        // retyped to change it.
        SettingValue = setting.Value ?? string.Empty;
        SettingIsSecret = setting.IsSecret;
    }

    private void ClearSettingEditor()
    {
        _editingSettingKey = null;
        SettingKey = string.Empty;
        SettingValue = string.Empty;
        SettingIsSecret = false;
    }

    private async Task SaveSettingAsync()
    {
        var key = SettingKey.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            Toast.ShowError("Enter a setting key.");
            return;
        }

        if (SettingIsSecret && string.IsNullOrEmpty(SettingValue))
        {
            // Saving an empty secret would replace the stored credential with an empty string.
            Toast.ShowError("Enter a new value for the secret, or delete the setting instead.");
            return;
        }

        _actionInProgress = true;
        try
        {
            var saved = await Api.SetModuleSettingAsync(ModuleId, key, new UpsertModuleSettingDto
            {
                Value = SettingValue,
                IsSecret = SettingIsSecret,
            });

            if (saved is null)
            {
                Toast.ShowError("Failed to save the setting.");
                return;
            }

            _settings = await Api.ListModuleSettingsAsync(ModuleId);
            ClearSettingEditor();
            Toast.ShowSuccess("Setting saved. The module reads it on its next request.");
        }
        finally { _actionInProgress = false; }
    }

    private async Task DeleteSettingAsync(ModuleSettingDto setting)
    {
        _actionInProgress = true;
        try
        {
            if (!await Api.DeleteModuleSettingAsync(ModuleId, setting.Key))
            {
                Toast.ShowError("Failed to delete the setting.");
                return;
            }

            _settings = await Api.ListModuleSettingsAsync(ModuleId);
            if (string.Equals(_editingSettingKey, setting.Key, StringComparison.Ordinal))
            {
                ClearSettingEditor();
            }

            Toast.ShowSuccess("Setting deleted.");
        }
        finally { _actionInProgress = false; }
    }

    private async Task UninstallAsync()
    {
        var dataChoice = DestroyDataOnUninstall ? "its data is destroyed" : "its data is kept";
        if (!await _confirmDialog.ShowAsync(
                $"Uninstall '{_module?.Id}'? {dataChoice}.",
                "Uninstall module"))
        {
            return;
        }

        _actionInProgress = true;
        try
        {
            var result = await Api.UninstallModuleAsync(ModuleId, DestroyDataOnUninstall, RemoveFilesOnUninstall);
            if (result is { Success: true })
            {
                Toast.ShowSuccess(result.DataStoreRetained
                    ? "Module uninstalled; its data store was kept as orphaned."
                    : "Module uninstalled.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError(result?.Error ?? "Uninstall failed.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private static string Effective(int? value) => value is null ? "inherit" : value.Value.ToString();

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'");

    private static string GetTrustClass(string trustTier) => trustTier switch
    {
        "Required" => "muted",
        "Bundled" => "success",
        "Store" => "warning",
        _ => "muted"
    };

    private static string GetSandboxClass(string sandboxStatus) => sandboxStatus switch
    {
        "Active" => "success",
        "LegacyOverride" => "warning",
        "PendingProvisioning" => "warning",
        "QuotaExceeded" => "danger",
        "Error" => "danger",
        _ => "muted"
    };

    private static string GetDataStoreClass(string status) => status switch
    {
        "Active" => "success",
        "Pending" => "warning",
        "Orphaned" => "warning",
        "Dropping" => "warning",
        "Error" => "danger",
        _ => "muted"
    };

    private static string GetStatusClass(string status) => status switch
    {
        "Enabled" => "success",
        "Disabled" => "muted",
        _ => "warning"
    };

    /// <summary>One outstanding erasure for this module, flattened for display.</summary>
    private sealed record OutstandingErasure(
        Guid RequestId,
        Guid UserId,
        DateTime RequestedAtUtc,
        int Attempts,
        string? LastError);
}
