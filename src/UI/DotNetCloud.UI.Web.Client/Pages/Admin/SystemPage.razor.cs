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

/// <summary>Code-behind for the <see cref="SystemPage"/> component.</summary>
public partial class SystemPage : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    [Inject] private DotNetCloudApiClient ApiClient { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private readonly CancellationTokenSource _cts = new();
    private AdminSystemStatusDto? _status;
    private DateTimeOffset? _lastSuccessUtc;
    private bool _stale;
    private bool _refreshing;
    private bool _initialLoadFailed;
    private bool _errorToastShown;

    protected override async Task OnInitializedAsync()
    {
        await FetchAsync();
        _ = PollAsync(_cts.Token);
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await FetchAsync();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The page was navigated away from; the loop ends with it.
        }
    }

    private async Task FetchAsync()
    {
        try
        {
            var status = await ApiClient.GetSystemStatusAsync(_cts.Token);
            if (status is null)
            {
                throw new InvalidOperationException("Empty system status response.");
            }

            _status = status;
            _lastSuccessUtc = DateTimeOffset.UtcNow;
            _stale = false;
            _initialLoadFailed = false;
            _errorToastShown = false;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Component disposed.
        }
        catch
        {
            _stale = _status is not null;
            _initialLoadFailed = _status is null;

            // One toast per outage, not one per polling tick.
            if (!_errorToastShown)
            {
                _errorToastShown = true;
                Toast.ShowError("System metrics could not be refreshed.");
            }
        }
    }

    private async Task RefreshAsync()
    {
        _refreshing = true;
        try
        {
            await FetchAsync();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string FormatPercent(double? percent) =>
        percent is null ? "—" : $"{percent.Value:F1}%";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:F0} {units[unit]}" : $"{value:F1} {units[unit]}";
    }

    private static string FormatRate(double bytesPerSecond) => $"{FormatBytes((long)Math.Round(bytesPerSecond))}/s";

    private static string FormatDuration(double ms)
    {
        if (ms < 1000) return $"{ms:F0} ms";
        if (ms < 60_000) return $"{ms / 1000:F1} s";
        return $"{ms / 60_000:F1} min";
    }

    private static string FormatUptime(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{span.Seconds}s";
    }

    private static string FormatTimeAgo(DateTimeOffset? time)
    {
        if (time is null || time.Value == default || time.Value == DateTimeOffset.MinValue)
        {
            return "—";
        }

        var elapsed = DateTimeOffset.UtcNow - time.Value;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed.TotalSeconds < 60) return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h {(int)elapsed.Minutes % 60}m ago";
        return $"{(int)elapsed.TotalDays}d {(int)elapsed.TotalHours % 24}h ago";
    }
}
