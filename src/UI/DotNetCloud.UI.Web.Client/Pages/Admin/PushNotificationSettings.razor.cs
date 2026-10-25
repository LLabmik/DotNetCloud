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

/// <summary>Code-behind for the <see cref="PushNotificationSettings"/> component.</summary>
public partial class PushNotificationSettings : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private const string Module = "dotnetcloud.chat";

    private bool _loading = true;
    private bool _saving;
    private ConfirmDialog _confirmDialog = default!;

    // FCM fields
    private bool _fcmEnabled = true;
    private string _fcmProjectId = string.Empty;
    private string _fcmCredentialsPath = string.Empty;
    private bool _fcmDryRun;

    // Notification delivery toggles
    private bool _notifyFileShared = true;
    private bool _notifyPublicLinkAccessed = true;
    private bool _notifyQuotaWarning = true;
    private bool _notifyShareExpiring = true;
    private bool _notifyChatMention = true;

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
            Toast.ShowError("Failed to load push notification settings.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateFields(IReadOnlyList<SystemSettingDto> settings)
    {
        _fcmEnabled = GetBool(settings, "Push:Fcm:Enabled", defaultValue: true);
        _fcmProjectId = GetString(settings, "Push:Fcm:ProjectId");
        _fcmCredentialsPath = GetString(settings, "Push:Fcm:CredentialsPath");
        _fcmDryRun = GetBool(settings, "Push:Fcm:DryRun");
        _notifyFileShared = GetBool(settings, "Push:Delivery:FileShared", defaultValue: true);
        _notifyPublicLinkAccessed = GetBool(settings, "Push:Delivery:PublicLinkAccessed", defaultValue: true);
        _notifyQuotaWarning = GetBool(settings, "Push:Delivery:QuotaWarning", defaultValue: true);
        _notifyShareExpiring = GetBool(settings, "Push:Delivery:ShareExpiring", defaultValue: true);
        _notifyChatMention = GetBool(settings, "Push:Delivery:ChatMention", defaultValue: true);
    }

    private async Task SaveAsync()
    {
        _saving = true;
        try
        {
            var tasks = new[]
            {
                Api.UpsertSettingAsync(Module, "Push:Fcm:Enabled", Str(_fcmEnabled.ToString())),
                Api.UpsertSettingAsync(Module, "Push:Fcm:ProjectId", Str(_fcmProjectId)),
                Api.UpsertSettingAsync(Module, "Push:Fcm:CredentialsPath",
                    new UpsertSystemSettingDto { Value = _fcmCredentialsPath, IsSensitive = true }),
                Api.UpsertSettingAsync(Module, "Push:Fcm:DryRun", Str(_fcmDryRun.ToString())),
                Api.UpsertSettingAsync(Module, "Push:Delivery:FileShared", Str(_notifyFileShared.ToString())),
                Api.UpsertSettingAsync(Module, "Push:Delivery:PublicLinkAccessed", Str(_notifyPublicLinkAccessed.ToString())),
                Api.UpsertSettingAsync(Module, "Push:Delivery:QuotaWarning", Str(_notifyQuotaWarning.ToString())),
                Api.UpsertSettingAsync(Module, "Push:Delivery:ShareExpiring", Str(_notifyShareExpiring.ToString())),
                Api.UpsertSettingAsync(Module, "Push:Delivery:ChatMention", Str(_notifyChatMention.ToString()))
            };

            var results = await Task.WhenAll(tasks);
            if (results.All(r => r))
                Toast.ShowSuccess("Push notification settings saved. Restart the Chat module to apply changes.");
            else
                Toast.ShowError("Some settings could not be saved.");
        }
        catch
        {
            Toast.ShowError("Failed to save push notification settings.");
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

    private static UpsertSystemSettingDto Str(string value, string? description = null) =>
        new() { Value = value, Description = description };
}
