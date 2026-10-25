using System.Net.Http;
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
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="NotificationBell"/> component.</summary>
public partial class NotificationBell : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private bool _isOpen;
    private bool _isLoading;
    private int _unreadCount;
    private readonly List<NotificationDto> _notifications = [];

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        RealtimeClient.NotificationCreated += OnNotificationCreated;

        try { await RealtimeClient.StartAsync(); }
        catch { /* silent — polling fallback still works */ }

        await RefreshUnreadCountAsync();
        StartPolling();
    }

    private void StartPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();
        _pollTask = PollLoopAsync(_pollCts.Token);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshFromServerAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the component is disposed.
        }
    }

    private void OnNotificationCreated(NotificationDto notification)
    {
        _ = RefreshFromServerAsync();
    }

    private async Task RefreshFromServerAsync()
    {
        await RefreshUnreadCountAsync();

        if (_isOpen)
        {
            _isLoading = true;
            try
            {
                var latest = await ApiClient.GetUnreadNotificationsAsync(25);
                _notifications.Clear();
                _notifications.AddRange(latest);
            }
            catch { /* silent */ }
            finally
            {
                _isLoading = false;
            }
        }
    }

    private async Task ToggleDropdown()
    {
        _isOpen = !_isOpen;

        if (_isOpen)
        {
            _isLoading = true;
            try
            {
                _notifications.Clear();
                _notifications.AddRange(await ApiClient.GetUnreadNotificationsAsync(25));
            }
            finally
            {
                _isLoading = false;
            }
        }
    }

    private async Task RefreshUnreadCountAsync()
    {
        try { _unreadCount = await ApiClient.GetUnreadNotificationCountAsync(); }
        catch { /* silent */ }
    }

    private async Task OpenNotificationAsync(NotificationDto notification)
    {
        await ApiClient.MarkNotificationReadAsync(notification.Id);
        _notifications.RemoveAll(n => n.Id == notification.Id);
        await RefreshUnreadCountAsync();

        Navigation.NavigateTo(string.IsNullOrWhiteSpace(notification.ActionUrl)
            ? "/"
            : notification.ActionUrl);
    }

    private async Task MarkAllReadAsync()
    {
        await ApiClient.MarkAllNotificationsReadAsync();
        _notifications.Clear();
        await RefreshUnreadCountAsync();
    }

    public async ValueTask DisposeAsync()
    {
        RealtimeClient.NotificationCreated -= OnNotificationCreated;

        _pollCts?.Cancel();

        if (_pollTask is not null)
        {
            try { await _pollTask; }
            catch { /* ignore */ }
        }

        _pollCts?.Dispose();
    }
}
