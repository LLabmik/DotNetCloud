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

/// <summary>Code-behind for the <see cref="SyncDevices"/> component.</summary>
public partial class SyncDevices : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private IReadOnlyList<DeviceSyncStatusDto> _devices = [];
    private Guid? _togglingDeviceId;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            _devices = await Api.ListDeviceSyncStatusAsync();
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Failed to load sync status. {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ToggleDeviceActiveAsync(DeviceSyncStatusDto device)
    {
        _togglingDeviceId = device.DeviceId;
        try
        {
            var newState = !device.IsActive;
            await Api.SetDeviceActiveAsync(device.DeviceId, newState);
            Toast.ShowSuccess($"{device.DeviceName} {(newState ? "activated" : "deactivated")}.");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Failed to update device. {ex.Message}");
        }
        finally
        {
            _togglingDeviceId = null;
        }
    }

    private static string GetLagBadge(DeviceSyncStatusDto device)
    {
        if (device.LastAcknowledgedSequence is null)
            return "muted";
        if (device.Lag == 0)
            return "success";
        if (device.Lag <= 10)
            return "warning";
        return "danger";
    }

    private static string GetLagText(DeviceSyncStatusDto device)
    {
        if (device.LastAcknowledgedSequence is null)
            return "No data";
        if (device.Lag == 0)
            return "Caught up";
        return $"{device.Lag} behind";
    }

    private static string FormatRelativeTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;
        if (elapsed.TotalMinutes < 1)
            return "Just now";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30)
            return $"{(int)elapsed.TotalDays}d ago";
        return utcTime.ToString("yyyy-MM-dd");
    }
}
