#if ANDROID
using Android.Util;
#endif
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// Server payload for unread count updates: { channelId, count, hasMention }.
/// </summary>
internal sealed record UnreadCountUpdatedPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("hasMention")] bool HasMention = false);

/// <summary>
/// Lightweight client-side mirror of the server's MessageDto for SignalR deserialization.
/// Only includes the fields needed for real-time display; full message is fetched on scroll-back.
/// </summary>
internal sealed record SignalRMessageDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("senderUserId")] Guid SenderUserId,
    [property: JsonPropertyName("senderName")] string? SenderName,
    [property: JsonPropertyName("sentAt")] DateTime SentAt,
    [property: JsonPropertyName("attachments")] IReadOnlyList<SignalRAttachmentDto>? Attachments = null);

/// <summary>
/// Server payload for new messages: { channelId, message }.
/// </summary>
internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] SignalRMessageDto Message);

/// <summary>
/// <see cref="ICoreHubClient"/> implementation that maintains a persistent SignalR
/// connection to the DotNetCloud CoreHub. Consolidates chat, calendar, and future
/// module events into a single WebSocket connection.
/// Designed to be long-lived as a singleton; the foreground service keeps it alive
/// when the app is backgrounded.
/// </summary>
internal sealed class SignalRChatClient : ICoreHubClient, IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ILogger<SignalRChatClient> _logger;
    private readonly IOfflineSyncService _offlineSync;
    private readonly ITokenRefreshService _tokenRefresh;
    private readonly IAppForegroundService _foregroundService;
    private readonly IChannelMuteStateService _muteState;
    private readonly ICalendarReminderScheduler _reminderScheduler;
    private readonly IServerReachabilityService _reachability;

    // Tracks channel groups joined so they can be re-joined after reconnection.
    private readonly ConcurrentDictionary<Guid, byte> _joinedChannels = new();

    // Last-used server URL so a manual reconnect can reuse it after automatic
    // reconnect attempts are exhausted.
    private string? _serverBaseUrl;
    private bool _reconnecting;

    /// <inheritdoc />
    public event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <inheritdoc />
    public event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <inheritdoc />
    public event Action? CalendarsChanged;

    /// <summary>Initializes a new <see cref="SignalRChatClient"/>.</summary>
    public SignalRChatClient(
        ILogger<SignalRChatClient> logger,
        IOfflineSyncService offlineSync,
        ITokenRefreshService tokenRefresh,
        IAppForegroundService foregroundService,
        IChannelMuteStateService muteState,
        ICalendarReminderScheduler reminderScheduler,
        IServerReachabilityService reachability)
    {
        _logger = logger;
        _offlineSync = offlineSync;
        _tokenRefresh = tokenRefresh;
        _foregroundService = foregroundService;
        _muteState = muteState;
        _reminderScheduler = reminderScheduler;
        _reachability = reachability;
    }

    /// <summary>
    /// Configures and opens the SignalR hub connection to the given server URL.
    /// The connection uses automatic reconnect with exponential back-off.
    /// </summary>
    /// <param name="serverBaseUrl">Root URL of the DotNetCloud server.</param>
    /// <param name="accessToken">Bearer token used for hub authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string serverBaseUrl, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        _serverBaseUrl = serverBaseUrl;

        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);

        var hubUrl = $"{serverBaseUrl.TrimEnd('/')}/hubs/core";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Fetch a FRESH token on every connect/reconnect so reconnects never use
                // a stale (expired) token. The refresh service proactively rotates the
                // access token before it expires.
                options.AccessTokenProvider = async () =>
                    await _tokenRefresh.EnsureFreshAccessTokenAsync(serverBaseUrl).ConfigureAwait(false);
                options.HttpMessageHandlerFactory = static _ => OAuthHttpClientHandlerFactory.CreateHandler();
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)])
            .WithKeepAliveInterval(TimeSpan.FromMinutes(2))
            .Build();

        _hub.On<UnreadCountUpdatedPayload>("UnreadCountUpdated", payload =>
            OnUnreadCountUpdated?.Invoke(this, new ChatUnreadCountUpdatedEventArgs(payload.ChannelId, payload.Count, payload.HasMention)));

        _hub.On<NewMessagePayload>("NewMessage", payload =>
        {
            var senderName = !string.IsNullOrEmpty(payload.Message.SenderName)
                ? payload.Message.SenderName
                : payload.Message.SenderUserId.ToString();
#if ANDROID
            Log.Info("DotNetCloud", $"SignalRChatClient: NewMessage received! channelId={payload.ChannelId}, content='{payload.Message.Content}', senderName='{senderName}', sentAt={payload.Message.SentAt:O}");
#endif

            string? attachmentsJson = null;
            if (payload.Message.Attachments is { Count: > 0 })
            {
                attachmentsJson = JsonSerializer.Serialize(payload.Message.Attachments);
            }

            OnNewChatMessage?.Invoke(this, new ChatMessageReceivedEventArgs(
                payload.ChannelId,
                string.Empty,
                senderName,
                payload.Message.Content,
                payload.Message.Id,
                payload.Message.SentAt,
                false,
                payload.Message.SenderUserId,
                attachmentsJson));

#if ANDROID
            Log.Info("DotNetCloud", $"SignalR notification: foreground={_foregroundService.IsInForeground}, channelId={payload.ChannelId}");

            // Post an Android notification if the app is backgrounded and the channel isn't muted.
            try
            {
                if (!_foregroundService.IsInForeground &&
                    Guid.TryParse(payload.ChannelId, out var chId) &&
                    !_muteState.IsMuted(chId))
                {
                    Log.Info("DotNetCloud", $"SignalR notification: posting for channel {payload.ChannelId}");
                    PostSignalRNotification(payload.ChannelId, senderName, payload.Message.Content);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"SignalR notification failed: {ex.Message}");
            }
#endif
        });

        // ── Calendar event handlers ─────────────────────────────────
        _hub.On<JsonElement>("CalendarEventDeleted", payload =>
        {
            try
            {
                var eventIdStr = payload.GetProperty("eventId").GetString();
                if (Guid.TryParse(eventIdStr, out var eventId))
                {
                    _logger.LogInformation(
                        "SignalR: calendar event {EventId} deleted — cancelling alarms.", eventId);
                    Log.Info("DotNetCloud", $"SignalR: calendar event {eventId} deleted — cancelling alarms.");
                    _reminderScheduler.CancelReminders(eventId);
                    CalendarsChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR: failed to handle CalendarEventDeleted.");
            }
        });

        _hub.On<JsonElement>("CalendarEventCreated", payload =>
        {
            try
            {
                _logger.LogInformation("SignalR: calendar event created — will refresh on next sync.");
                Log.Info("DotNetCloud", "SignalR: calendar event created received.");
                CalendarsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR: failed to handle CalendarEventCreated.");
            }
        });

        _hub.On<JsonElement>("CalendarEventUpdated", payload =>
        {
            try
            {
                _logger.LogInformation("SignalR: calendar event updated — will refresh on next sync.");
                CalendarsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR: failed to handle CalendarEventUpdated.");
            }
        });

        _hub.Reconnected += async connectionId =>
        {
            _logger.LogInformation("SignalR reconnected (connId={ConnectionId}). Re-joining channel groups and flushing pending messages.", connectionId);

            if (_hub?.State == HubConnectionState.Connected)
            {
                foreach (var (channelId, _) in _joinedChannels)
                {
                    try
                    {
                        var groupName = $"chat-channel-{channelId}";
                        await _hub.InvokeAsync("JoinGroupAsync", groupName).ConfigureAwait(false);
                        _logger.LogDebug("Re-joined SignalR group {Group} after reconnect.", groupName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to re-join channel group {ChannelId} after reconnect; will retry on next reconnect.", channelId);
                    }
                }
            }

            await _offlineSync.FlushAllAsync().ConfigureAwait(false);

            // Resync calendar alarms after reconnect to catch events deleted while offline.
            try
            {
                await _reminderScheduler.RescheduleAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resync calendar alarms after reconnect.");
            }
        };
        _hub.Closed += async error =>
        {
            // Automatic reconnect attempts have been exhausted. Schedule a manual
            // reconnect with backoff, gated by server reachability so we don't
            // hammer a dead server.
            _logger.LogWarning(error, "SignalR connection closed. Scheduling reconnect.");
            await ScheduleReconnectAsync();
        };

        try
        {
            await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SignalR connected to {HubUrl}.", hubUrl);

            // Sync calendar alarms on initial connect (catches events deleted while offline)
            try
            {
                await _reminderScheduler.RescheduleAllAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resync calendar alarms on initial connect.");
            }
        }
        catch
        {
            // StartAsync failed — null out _hub so downstream callers know there
            // is no active connection rather than pointing at a dead HubConnection.
            await _hub.DisposeAsync().ConfigureAwait(false);
            _hub = null;
            throw;
        }
    }

    /// <summary>Implements the parameterless <see cref="IChatSignalRClient.ConnectAsync(CancellationToken)"/> for compatibility.</summary>
    Task IChatSignalRClient.ConnectAsync(CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException(
            "Use the overload that accepts serverBaseUrl and accessToken."));

    /// <summary>
    /// Retries the SignalR connection with exponential backoff after automatic
    /// reconnect attempts are exhausted. Gated by server reachability so a dead
    /// server is not hammered; the connection resumes automatically on recovery.
    /// </summary>
    private async Task ScheduleReconnectAsync()
    {
        if (_reconnecting)
            return;
        _reconnecting = true;
        try
        {
            var delay = TimeSpan.FromSeconds(5);
            while (_serverBaseUrl is not null && !string.IsNullOrEmpty(_serverBaseUrl))
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (_reachability is null || _reachability.IsServerOnline)
                    {
                        await ConnectAsync(_serverBaseUrl, cancellationToken: default).ConfigureAwait(false);
                        _logger.LogInformation("SignalR reconnected after retry.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SignalR reconnect attempt failed; retrying.");
                }
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(2).Ticks));
            }
        }
        finally
        {
            _reconnecting = false;
        }
    }

    /// <inheritdoc />
    public async Task JoinChannelGroupAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        // Always track the join so we can re-join on reconnect — even if the hub
        // happens to be disconnected right now, the caller will re-trigger this
        // after ensuring connectivity.
        _joinedChannels.TryAdd(channelId, 0);

        // Give the hub a brief window to become connected before giving up.
        // This handles the case where ConnectAsync returned successfully but
        // the hub state hasn't transitioned to Connected yet, or where a
        // transient blip happened between ConnectAsync and JoinChannelGroupAsync.
        if (_hub?.State is not HubConnectionState.Connected)
        {
            for (var i = 0; i < 5 && _hub?.State is not HubConnectionState.Connected; i++)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        if (_hub?.State is not HubConnectionState.Connected)
        {
#if ANDROID
            Log.Warn("DotNetCloud", $"JoinChannelGroupAsync: cannot join {channelId}: hub not connected (state={_hub?.State}).");
#endif
            _logger.LogWarning("Cannot join channel group {ChannelId}: hub not connected (state={State}); tracked for retry on reconnect.",
                channelId, _hub?.State);
            return;
        }

        var groupName = $"chat-channel-{channelId}";
#if ANDROID
        Log.Info("DotNetCloud", $"JoinChannelGroupAsync: invoking JoinGroupAsync('{groupName}')...");
#endif
        try
        {
            await _hub.InvokeAsync("JoinGroupAsync", groupName).ConfigureAwait(false);
#if ANDROID
            Log.Info("DotNetCloud", $"JoinChannelGroupAsync: successfully joined group '{groupName}'.");
#endif
        }
        catch (Exception ex)
        {
#if ANDROID
            Log.Error("DotNetCloud", $"JoinChannelGroupAsync: FAILED to join group '{groupName}': {ex.Message}");
#endif
            _logger.LogWarning(ex, "Failed to join SignalR group {Group}", groupName);
        }
        _logger.LogDebug("Joined SignalR group {Group}.", groupName);
    }

    /// <inheritdoc />
    public async Task LeaveChannelGroupAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        _joinedChannels.TryRemove(channelId, out _);

        if (_hub?.State is not HubConnectionState.Connected)
        {
            _logger.LogDebug("Cannot leave channel group {ChannelId}: hub not connected (already untracked).", channelId);
            return;
        }

        var groupName = $"chat-channel-{channelId}";
        await _hub.InvokeAsync("LeaveGroupAsync", groupName, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Left SignalR group {Group}.", groupName);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);
    }

#if ANDROID
    private void PostSignalRNotification(string channelId, string senderName, string content)
    {
        var channelGuid = Guid.TryParse(channelId, out var g) ? g : Guid.Empty;

        var intent = new global::Android.Content.Intent(
            global::Android.App.Application.Context,
            typeof(DotNetCloud.Client.Android.MainActivity));
        intent.SetAction(global::Android.Content.Intent.ActionMain);
        intent.AddCategory(global::Android.Content.Intent.CategoryLauncher);
        if (channelGuid != Guid.Empty)
            intent.PutExtra("channelId", channelGuid.ToString());

        var pendingIntent = global::Android.App.PendingIntent.GetActivity(
            global::Android.App.Application.Context,
            channelGuid.GetHashCode(),
            intent,
            global::Android.App.PendingIntentFlags.Immutable | global::Android.App.PendingIntentFlags.UpdateCurrent);

        var iconRes = global::Android.App.Application.Context.Resources!
            .GetIdentifier("ic_notification", "drawable",
                global::Android.App.Application.Context.PackageName);
        if (iconRes == 0)
            iconRes = global::Android.Resource.Drawable.IcDialogInfo;

        var notification = new global::Android.App.Notification.Builder(
                global::Android.App.Application.Context,
                DotNetCloud.Client.Android.MainApplication.ChannelIdMessages)
            .SetContentTitle(senderName)
            .SetContentText(content)
            .SetSmallIcon(iconRes)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetGroup($"dnc_chat_{channelId}")
            .Build();

        var nm = (global::Android.App.NotificationManager?)
            global::Android.App.Application.Context.GetSystemService(
                global::Android.Content.Context.NotificationService);
        var notificationId = 2000 + (channelGuid.GetHashCode() & 0x0FFF);
        nm?.Notify(notificationId, notification);
    }
#endif
}
