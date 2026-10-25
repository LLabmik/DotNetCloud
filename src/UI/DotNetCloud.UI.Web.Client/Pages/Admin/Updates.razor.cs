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

/// <summary>Code-behind for the <see cref="Updates"/> component.</summary>
public partial class Updates : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private bool _checking;
    private UpdateCheckResult? _updateCheck;
    private IReadOnlyList<ReleaseInfo> _releases = [];
    private string? _expandedRelease;
    private string _runtimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        try
        {
            _updateCheck = await Api.CheckForUpdateAsync();
            _releases = await Api.GetRecentReleasesAsync(10);
        }
        catch
        {
            Toast.ShowError("Failed to load update information.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        _checking = true;
        try
        {
            _updateCheck = await Api.CheckForUpdateAsync();
            _releases = await Api.GetRecentReleasesAsync(10);

            if (_updateCheck?.IsUpdateAvailable == true)
            {
                Toast.ShowSuccess($"Update available: v{_updateCheck.LatestVersion}");
            }
            else
            {
                Toast.ShowSuccess("You are running the latest version.");
            }
        }
        catch
        {
            Toast.ShowError("Failed to check for updates.");
        }
        finally
        {
            _checking = false;
        }
    }

    private static string FormatDate(DateTimeOffset? dt)
    {
        if (dt is null) return "—";
        var span = DateTimeOffset.UtcNow - dt.Value;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        return dt.Value.ToString("yyyy-MM-dd");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }
}
