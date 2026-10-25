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

/// <summary>Code-behind for the <see cref="Broadcast"/> component.</summary>
public partial class Broadcast : ComponentBase, IAsyncDisposable
{
    private const int MaxHistory = 100;

    private bool _loading = true;
    private bool _busy;
    private string? _error;

    private string _title = string.Empty;
    private string _message = string.Empty;
    private AdminBroadcastSeverity _severity = AdminBroadcastSeverity.Warning;
    private DateTime? _scheduledLocal;
    private DateTime? _expiresLocal;

    private readonly List<AdminBroadcastDto> _broadcasts = [];
    private DncConfirmDialog _confirmDialog = default!;
    private CancellationTokenSource? _refreshCts;

    [Inject] private DotNetCloudApiClient ApiClient { get; set; } = default!;
    [Inject] private IMarkdownRenderer MarkdownRenderer { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();

        // Scheduled broadcasts are delivered by a 30-second background poll on the
        // server, so the history refreshes on the same cadence: the Status badge and
        // the Sent column then update on their own rather than going stale until the
        // administrator presses Refresh.
        _refreshCts = new CancellationTokenSource();
        _ = AutoRefreshAsync(_refreshCts.Token);
    }

    /// <summary>
    /// Periodically re-reads the history so scheduled sends are reflected without
    /// a manual refresh.
    /// </summary>
    private async Task AutoRefreshAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                // Never clobber the list while the administrator is mid-action.
                if (_busy || _loading)
                {
                    continue;
                }

                await InvokeAsync(async () =>
                {
                    await LoadAsync(silent: true);
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The page was navigated away from; the loop ends with it.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_refreshCts is null)
        {
            return;
        }

        await _refreshCts.CancelAsync();
        _refreshCts.Dispose();
        _refreshCts = null;
    }

    private async Task LoadAsync(bool silent = false)
    {
        if (!silent)
        {
            _loading = true;
        }

        try
        {
            var broadcasts = await ApiClient.ListAdminBroadcastsAsync(MaxHistory);
            _broadcasts.Clear();
            _broadcasts.AddRange(broadcasts);
        }
        catch (Exception ex)
        {
            _error = "Unable to load broadcast history.";
            Toast.ShowError(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private Task SendNowAsync() => CreateAsync(_scheduledLocal, sendNow: true);

    private Task ScheduleAsync() => CreateAsync(_scheduledLocal, sendNow: false);

    private async Task CreateAsync(DateTime? scheduledLocal, bool sendNow)
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_title) || string.IsNullOrWhiteSpace(_message))
        {
            _error = "A title and a message are required.";
            return;
        }

        var scheduledUtc = sendNow ? null : ToUtc(scheduledLocal);
        if (!sendNow && scheduledUtc is null)
        {
            _error = "Pick a send time, or use \"Send now\".";
            return;
        }

        if (!sendNow && scheduledUtc <= DateTime.UtcNow)
        {
            _error = "The send time must be in the future. Use \"Send now\" to send immediately.";
            return;
        }

        var expiresUtc = ToUtc(_expiresLocal);
        if (expiresUtc is not null && expiresUtc <= (scheduledUtc ?? DateTime.UtcNow))
        {
            _error = "The expiry time must be later than the send time.";
            return;
        }

        _busy = true;
        try
        {
            var created = await ApiClient.CreateAdminBroadcastAsync(new CreateAdminBroadcastRequest
            {
                Title = _title.Trim(),
                Message = _message.Trim(),
                Severity = _severity,
                ScheduledForUtc = scheduledUtc,
                ExpiresAtUtc = expiresUtc,
            });

            if (created is null)
            {
                _error = "The broadcast could not be created.";
                return;
            }

            Toast.ShowSuccess(created.Status == AdminBroadcastStatus.Scheduled
                ? "Broadcast scheduled."
                : "Broadcast sent.");

            ResetForm();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _error = "The broadcast could not be created.";
            Toast.ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SendExistingNowAsync(AdminBroadcastDto broadcast)
    {
        _busy = true;
        try
        {
            if (await ApiClient.SendAdminBroadcastNowAsync(broadcast.Id))
            {
                Toast.ShowSuccess("Broadcast sent.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("The broadcast could not be sent.");
            }
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task DeleteAsync(AdminBroadcastDto broadcast)
    {
        var confirmed = await _confirmDialog.ShowAsync(
            $"Delete \"{broadcast.Title}\"? Users who have not dismissed it will stop seeing it, and the message and its dismissal records are removed permanently.",
            "Delete broadcast");

        if (!confirmed)
        {
            return;
        }

        _busy = true;
        try
        {
            if (await ApiClient.DeleteAdminBroadcastAsync(broadcast.Id))
            {
                Toast.ShowSuccess("Broadcast deleted.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("The broadcast could not be deleted.");
            }
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ResetForm()
    {
        _title = string.Empty;
        _message = string.Empty;
        _severity = AdminBroadcastSeverity.Warning;
        _scheduledLocal = null;
        _expiresLocal = null;
    }

    /// <summary>
    /// Converts a value from a <c>datetime-local</c> input (browser local time, no zone
    /// information) into a UTC timestamp.
    /// </summary>
    private static DateTime? ToUtc(DateTime? local)
    {
        if (local is null)
        {
            return null;
        }

        return local.Value.Kind switch
        {
            DateTimeKind.Utc => local.Value,
            DateTimeKind.Local => local.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(local.Value, DateTimeKind.Local).ToUniversalTime(),
        };
    }

    /// <summary>
    /// Text for the Status badge. A scheduled broadcast also shows when it will be
    /// sent, so the administrator does not have to cross-reference the Created column.
    /// </summary>
    private static string StatusLabel(AdminBroadcastDto broadcast) => broadcast.Status switch
    {
        AdminBroadcastStatus.Scheduled when broadcast.ScheduledForUtc is not null =>
            $"Scheduled {broadcast.ScheduledForUtc.Value.ToLocalTime():g}",
        _ => broadcast.Status.ToString(),
    };

    private static string SeverityIcon(AdminBroadcastSeverity severity) => severity switch
    {
        AdminBroadcastSeverity.Critical => "error",
        AdminBroadcastSeverity.Warning => "warning",
        _ => "info",
    };
}
