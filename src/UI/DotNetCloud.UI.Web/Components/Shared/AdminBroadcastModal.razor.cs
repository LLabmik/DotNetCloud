using DotNetCloud.Core.DTOs;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>
/// Code-behind for <see cref="AdminBroadcastModal"/>.
/// Owns the live subscription to administrator broadcasts and the dismissal state for
/// the current user.
/// </summary>
/// <remarks>
/// Delivery is best-effort realtime: the component subscribes to the circuit's SignalR
/// relay (which only Blazor relays join) and also fetches the active broadcast on start,
/// so a dropped connection or a late login still shows the message. Dismissals are
/// persisted server-side, so a dismissed broadcast never reappears.
/// </remarks>
public partial class AdminBroadcastModal : ComponentBase, IAsyncDisposable
{
    [Inject]
    private DotNetCloudApiClient ApiClient { get; set; } = default!;

    [Inject]
    private IRealtimeNotificationClient RealtimeClient { get; set; } = default!;

    [Inject]
    private ILogger<AdminBroadcastModal> Logger { get; set; } = default!;

    private readonly HashSet<Guid> _dismissedIds = [];
    private ActiveAdminBroadcastDto? _active;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        RealtimeClient.AdminBroadcastReceived += OnAdminBroadcastReceived;
        RealtimeClient.AdminBroadcastRemoved += OnAdminBroadcastRemoved;

        try
        {
            await RealtimeClient.StartAsync();
        }
        catch (Exception ex)
        {
            // The one-shot fetch below still surfaces an active broadcast.
            Logger.LogDebug(ex, "Failed to start the realtime broadcast subscription.");
        }

        await LoadActiveBroadcastAsync();
    }

    private void OnAdminBroadcastReceived(ActiveAdminBroadcastDto broadcast)
    {
        _ = InvokeAsync(() =>
        {
            if (_dismissedIds.Contains(broadcast.Id))
            {
                return;
            }

            _active = broadcast;
            StateHasChanged();
        });
    }

    private void OnAdminBroadcastRemoved(Guid broadcastId)
    {
        _ = InvokeAsync(() =>
        {
            if (_active?.Id != broadcastId)
            {
                return;
            }

            // Deleted by an admin — close without recording a dismissal.
            _active = null;
            StateHasChanged();
        });
    }

    private async Task LoadActiveBroadcastAsync()
    {
        try
        {
            var active = await ApiClient.GetActiveAdminBroadcastAsync();

            if (active is null || _dismissedIds.Contains(active.Id))
            {
                return;
            }

            _active = active;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to load the active administrator broadcast.");
        }
    }

    private async Task DismissAsync()
    {
        var broadcast = _active;
        _active = null;

        if (broadcast is not null)
        {
            _dismissedIds.Add(broadcast.Id);
        }

        StateHasChanged();

        if (broadcast is null)
        {
            return;
        }

        try
        {
            await ApiClient.DismissAdminBroadcastAsync(broadcast.Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record dismissal of broadcast {BroadcastId}.", broadcast.Id);
        }
    }

    private static string SeverityClass(AdminBroadcastSeverity severity) => severity switch
    {
        AdminBroadcastSeverity.Critical => "alert-danger",
        AdminBroadcastSeverity.Warning => "alert-warning",
        _ => string.Empty,
    };

    private static string SeverityIcon(AdminBroadcastSeverity severity) => severity switch
    {
        AdminBroadcastSeverity.Critical => "error",
        AdminBroadcastSeverity.Warning => "warning",
        _ => "info",
    };

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        RealtimeClient.AdminBroadcastReceived -= OnAdminBroadcastReceived;
        RealtimeClient.AdminBroadcastRemoved -= OnAdminBroadcastRemoved;
        return ValueTask.CompletedTask;
    }
}
