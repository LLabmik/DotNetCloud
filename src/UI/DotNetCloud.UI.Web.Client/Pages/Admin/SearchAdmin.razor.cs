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

/// <summary>Code-behind for the <see cref="SearchAdmin"/> component.</summary>
public partial class SearchAdmin : ComponentBase, IDisposable
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private bool _reindexTriggered;
    private SearchAdminStatusDto? _status;
    private System.Threading.Timer? _pollTimer;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            _status = await Api.GetSearchAdminStatusAsync();
            UpdatePolling();
        }
        catch
        {
            Toast.ShowError("Failed to load search engine status.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task TriggerFullReindexAsync()
    {
        _reindexTriggered = true;
        try
        {
            var success = await Api.TriggerSearchReindexAsync();
            if (success)
            {
                Toast.ShowSuccess("Full reindex started.");
                // Brief delay to let the service start, then refresh
                await Task.Delay(1000);
                await RefreshAsync();
            }
            else
            {
                Toast.ShowError("Failed to trigger reindex.");
                _reindexTriggered = false;
            }
        }
        catch
        {
            Toast.ShowError("Failed to trigger reindex.");
            _reindexTriggered = false;
        }
    }

    private async Task TriggerModuleReindexAsync(string moduleId)
    {
        try
        {
            var success = await Api.TriggerSearchModuleReindexAsync(moduleId);
            if (success)
            {
                Toast.ShowSuccess($"Reindex triggered for {FormatModuleName(moduleId)}.");
            }
            else
            {
                Toast.ShowError($"Failed to reindex {FormatModuleName(moduleId)}.");
            }
        }
        catch
        {
            Toast.ShowError($"Failed to reindex {FormatModuleName(moduleId)}.");
        }
    }

    /// <summary>
    /// Starts/stops a polling timer when a reindex is running so the UI stays updated.
    /// </summary>
    private void UpdatePolling()
    {
        // Always clear the queued button state once we have a valid status refresh
        _reindexTriggered = false;

        if (_status?.IsReindexing == true)
        {
            _pollTimer ??= new System.Threading.Timer(async _ =>
            {
                try
                {
                    _status = await Api.GetSearchAdminStatusAsync();
                    await InvokeAsync(StateHasChanged);

                    // Stop polling when reindex finishes
                    if (_status?.IsReindexing != true)
                    {
                        StopPolling();
                    }
                }
                catch
                {
                    // Ignore poll failures
                }
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
        else
        {
            StopPolling();
        }
    }

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private string FormatLastReindex()
    {
        if (_status?.LastFullReindexAt is null && _status?.LastFullReindexJob?.CompletedAt is null)
            return "Never";

        var dt = _status.LastFullReindexJob?.CompletedAt ?? _status.LastFullReindexAt;
        return dt.HasValue ? FormatTimeAgo(dt.Value) : "Never";
    }

    private static string FormatTimeAgo(DateTimeOffset dt)
    {
        var span = DateTimeOffset.UtcNow - dt;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("yyyy-MM-dd HH:mm");
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 60) return $"{seconds:F1}s";
        var minutes = (int)(seconds / 60);
        var secs = seconds % 60;
        return $"{minutes}m {secs:F0}s";
    }

    private string ProgressPercent()
    {
        if (_status?.ReindexProgress is null || _status.ReindexProgress.DocumentsTotal == 0)
            return "0";
        var pct = (double)_status.ReindexProgress.DocumentsProcessed / _status.ReindexProgress.DocumentsTotal * 100;
        return Math.Min(pct, 100).ToString("F1");
    }

    private static string FormatModuleName(string moduleId) => moduleId switch
    {
        "files" => "Files",
        "notes" => "Notes",
        "calendar" => "Calendar",
        "contacts" => "Contacts",
        "chat" => "Chat",
        "music" => "Music",
        "photos" => "Photos",
        "tracks" => "Tracks",
        _ => moduleId
    };

    private static string GetJobBadgeClass(string status) => status switch
    {
        "Completed" => "success",
        "Running" => "warning",
        "Pending" => "muted",
        "Failed" => "danger",
        _ => "muted"
    };

    public void Dispose()
    {
        StopPolling();
    }
}
