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

/// <summary>Code-behind for the <see cref="Health"/> component.</summary>
public partial class Health : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private HealthReportDto? _health;
    private IReadOnlyList<BackgroundServiceStatusDto> _bgServices = [];

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            var healthTask = Api.GetHealthAsync();
            var bgTask = Api.GetBackgroundServicesAsync();
            await Task.WhenAll(healthTask, bgTask);
            _health = healthTask.Result;
            _bgServices = bgTask.Result;
        }
        catch
        {
            Toast.ShowError("Failed to load health status.");
        }
        finally
        {
            _loading = false;
        }
    }

    private static string GetBadgeClass(string status) => status switch
    {
        "Healthy" => "success",
        "Degraded" => "warning",
        "Unhealthy" => "danger",
        _ => "muted"
    };

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        var elapsed = DateTimeOffset.UtcNow - time;
        if (elapsed.TotalSeconds < 60) return $"{elapsed.Seconds}s ago";
        if (elapsed.TotalMinutes < 60) return $"{elapsed.Minutes}m ago";
        if (elapsed.TotalHours < 24) return $"{elapsed.Hours}h {elapsed.Minutes % 60}m ago";
        return $"{elapsed.Days}d {elapsed.Hours % 24}h ago";
    }

    private static string FormatDuration(double ms)
    {
        if (ms < 1000) return $"{ms:F0}ms";
        if (ms < 60_000) return $"{ms / 1000:F1}s";
        return $"{ms / 60_000:F1}m";
    }
}
