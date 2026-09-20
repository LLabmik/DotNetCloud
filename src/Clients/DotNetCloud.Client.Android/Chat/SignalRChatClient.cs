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
    [property: JsonPropertyName("attachments")] IReadOnlyList<SignalRAttachmentDto>? Attachments = null,
    [property: JsonPropertyName("linkPreview")] SignalRLinkPreviewDto? LinkPreview = null);

/// <summary>
/// Server payload for new messages: { channelId, message }.
/// </summary>
internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] SignalRMessageDto Message);

/// <summary>
/// Server payload for typing indicators: { channelId, userId, displayName }.
/// </summary>
internal sealed record TypingIndicatorPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("displayName")] string? DisplayName = null);

/// <summary>
/// Server payload for presence broadcasts: { userId, status, timestamp } (camelCase).
/// The single <c>UserPresence</c> event carries the user's derived 4-state status
/// ("Online" / "Away" / "DoNotDisturb" / "Offline"). Both web (Blazor circuit) and
/// native CoreHub presence transitions use the same shape.
/// </summary>
internal sealed record PresenceStatePayload(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp);

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
    private readonly IChatSoundPlayer _chatSound;
    private readonly ISecureTokenStore _tokenStore;

    // Tracks channel groups joined so they can be re-joined after reconnection.
    private readonly ConcurrentDictionary<Guid, byte> _joinedChannels = new();

    // Last-used server URL so a manual reconnect can reuse it after automatic
    // reconnect attempts are exhausted.
    private string? _serverBaseUrl;
    private bool _reconnecting;

    // Signed-in user, resolved from the id_token at connect time. Used to keep a user's own
    // message echo (sent from this phone or any other client) from dinging.
    private Guid _currentUserId;

    /// <inheritdoc />
    public event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <inheritdoc />
    public event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <inheritdoc />
    public event EventHandler<ChatTypingEventArgs>? OnChatTyping;

    /// <inheritdoc />
    public event EventHandler<UserPresenceChangedEventArgs>? OnUserPresenceChanged;

    /// <inheritdoc />
    public event EventHandler? Reconnected;

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
        IServerReachabilityService reachability,
        IChatSoundPlayer chatSound,
        ISecureTokenStore tokenStore)
    {
        _logger = logger;
        _offlineSync = offlineSync;
        _tokenRefresh = tokenRefresh;
        _foregroundService = foregroundService;
        _muteState = muteState;
        _reminderScheduler = reminderScheduler;
        _reachability = reachability;
        _chatSound = chatSound;
        _tokenStore = tokenStore;
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

        // Load the in-app alert sound up front so the first message of this process is not
        // swallowed while the platform decodes the resource.
        _chatSound.Prepare();

        // Resolve the signed-in user so a user's own message echo never dings. Resolution reads
        // the signed id_token (the access token is JWE-encrypted and cannot be decoded locally);
        // a failure here is non-fatal and only disables the own-message check.
        _currentUserId = await ResolveCurrentUserIdAsync(serverBaseUrl, cancellationToken).ConfigureAwait(false);

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

        _hub.On<TypingIndicatorPayload>("TypingIndicator", payload =>
        {
#if ANDROID
            Log.Info("DotNetCloud", $"SignalRChatClient: TypingIndicator channel={payload.ChannelId} userId={payload.UserId} displayName='{payload.DisplayName}'");
#endif
            OnChatTyping?.Invoke(this, new ChatTypingEventArgs(payload.ChannelId, payload.UserId, payload.DisplayName));
        });

        // Presence broadcasts are emitted by the server on every derived 4-state transition
        // (first/last connection, idle sweep Online ↔ Away, DND toggles) — covering both native
        // CoreHub connections AND web (Blazor circuit) users, so remote clients see web peers'
        // presence correctly.
        _hub.On<PresenceStatePayload>("UserPresence", payload =>
        {
            var status = NormalizePresenceStatus(payload.Status);
#if ANDROID
            Log.Info("DotNetCloud", $"SignalRChatClient: UserPresence userId={payload.UserId} status={status}");
#endif
            OnUserPresenceChanged?.Invoke(this, new UserPresenceChangedEventArgs(payload.UserId, status));
        });

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

            string? linkPreviewJson = null;
            if (payload.Message.LinkPreview is not null)
            {
                linkPreviewJson = JsonSerializer.Serialize(payload.Message.LinkPreview);
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
                attachmentsJson,
                linkPreviewJson));

#if ANDROID
            // Exactly one alert per message: a system notification while the app is not visible
            // (it carries its own sound), and the in-app ding while it is — notifications are
            // suppressed in the foreground, so without the ding an incoming message is silent.
            var alert = ChatAlertPolicy.Decide(
                _foregroundService.IsInForeground,
                _chatSound.IsEnabled,
                Guid.TryParse(payload.ChannelId, out var alertChannelId) && _muteState.IsMuted(alertChannelId),
                payload.Message.SenderUserId,
                _currentUserId);

            Log.Info("DotNetCloud", $"SignalR chat alert: decision={alert}, foreground={_foregroundService.IsInForeground}, channelId={payload.ChannelId}");

            try
            {
                switch (alert)
                {
                    case ChatAlertKind.InAppSound:
                        Log.Info("DotNetCloud", $"SignalR chat alert: playing ding for channel {payload.ChannelId}");
                        _chatSound.PlayMessageAlert();
                        break;

                    case ChatAlertKind.SystemNotification:
                        Log.Info("DotNetCloud", $"SignalR chat alert: posting notification for channel {payload.ChannelId}");
                        PostSignalRNotification(payload.ChannelId);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("DotNetCloud", $"SignalR chat alert failed: {ex.Message}");
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

            // Let subscribers (e.g. ChannelListViewModel) re-query presence snapshots after
            // the connection is re-established, so DM dots are correct even if peers changed
            // presence while we were disconnected.
            Reconnected?.Invoke(this, EventArgs.Empty);
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

                    // Pause reconnect attempts while the app is backgrounded. A hidden app
                    // has no UI to refresh, and retrying during Doze only drains the battery
                    // (Google Play efficiency rules). The loop resumes automatically when the
                    // app returns to the foreground.
                    if (_foregroundService is not null && !_foregroundService.IsInForeground)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                        continue;
                    }

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
    public async Task<IReadOnlyDictionary<Guid, string>> GetPresenceStatusAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds is null || userIds.Count == 0)
            return new Dictionary<Guid, string>();

        // On cold start the presence-dot seed can run before ChatConnectionService has finished
        // connecting the hub (channel load completes ~0.5 s before the hub connects). Returning
        // an empty snapshot here would leave every DM dot gray until a live event happens to
        // arrive, so briefly wait for the connection before querying.
        if (!await WaitForHubConnectionAsync(ct).ConfigureAwait(false))
        {
            _logger.LogDebug("Cannot query presence: hub not connected after wait (state={State}).", _hub?.State);
            return new Dictionary<Guid, string>();
        }

        try
        {
            // The server method is GetPresenceStatusAsync(IReadOnlyList<Guid>) and returns a
            // Guid→PresenceState dictionary serialized as Guid-string keys + enum-name values.
            // Sending the IDs as GUID strings round-trips cleanly through the JSON wire protocol.
            var result = await _hub!.InvokeAsync<Dictionary<string, string>>(
                "GetPresenceStatusAsync",
                userIds.Select(id => id.ToString()).ToList(),
                ct).ConfigureAwait(false);

            var mapped = new Dictionary<Guid, string>(result?.Count ?? 0);
            if (result is not null)
            {
                foreach (var (key, status) in result)
                {
                    if (Guid.TryParse(key, out var guid))
                        mapped[guid] = NormalizePresenceStatus(status);
                }
            }

            return mapped;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query presence status for {Count} users.", userIds.Count);
            return new Dictionary<Guid, string>();
        }
    }

    /// <summary>
    /// Waits briefly (~6 s) for the hub to reach <see cref="HubConnectionState.Connected"/> so
    /// cold-start callers don't get a silently-empty result. Returns <c>false</c> when the hub is
    /// absent/disconnected or the wait times out.
    /// </summary>
    private async Task<bool> WaitForHubConnectionAsync(CancellationToken ct)
    {
        const int maxAttempts = 20;
        var delay = TimeSpan.FromMilliseconds(300);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var hub = _hub;
            if (hub is not null && hub.State == HubConnectionState.Connected)
            {
                return true;
            }

            // Disconnected (not connecting/reconnecting) — it won't come up on its own.
            if (hub is not null && hub.State == HubConnectionState.Disconnected)
            {
                return false;
            }

            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return _hub?.State == HubConnectionState.Connected;
    }

    /// <inheritdoc />
    public async Task ReportActivityAsync(CancellationToken ct = default)
    {
        if (_hub?.State is not HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hub.InvokeAsync("PingAsync", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report presence activity to CoreHub");
        }
    }

    /// <summary>
    /// Normalizes a presence status value to one of the four canonical status strings,
    /// defaulting to <c>"Offline"</c> for absent/unknown values.
    /// </summary>
    private static string NormalizePresenceStatus(string? status)
    {
        return status?.Trim() switch
        {
            "Online" => "Online",
            "Away" => "Away",
            "DoNotDisturb" => "DoNotDisturb",
            _ => "Offline"
        };
    }

    /// <summary>
    /// Resolves the signed-in user's ID from the stored <c>id_token</c>.
    /// </summary>
    /// <remarks>
    /// The access token is JWE-encrypted and cannot be decoded client-side, so the <c>sub</c>
    /// claim is only readable from the signed id_token. Returns <see cref="Guid.Empty"/> when the
    /// token is missing or unreadable — callers must treat that as "unknown", never as "no alert".
    /// </remarks>
    private async Task<Guid> ResolveCurrentUserIdAsync(string serverBaseUrl, CancellationToken ct)
    {
        try
        {
            var idToken = await _tokenStore.GetIdTokenAsync(serverBaseUrl, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(idToken))
            {
                _logger.LogDebug("No id_token available; own-message detection is disabled.");
                return Guid.Empty;
            }

            var userId = AccessTokenUserIdExtractor.ExtractUserId(idToken);
            _logger.LogInformation("Resolved current user id from id_token: {UserId}", userId);
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve the current user id; own-message detection is disabled.");
            return Guid.Empty;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);
    }

#if ANDROID
    /// <summary>
    /// Posts the chat notification for the in-process (SignalR) path.
    /// </summary>
    /// <remarks>
    /// The text is chosen by the client even here, where the sender and the message content are
    /// known: notifications must stay completely generic ("New message"), because they are shown
    /// on the lock screen and in the notification shade. The message itself is read in the app.
    /// </remarks>
    /// <param name="channelId">Channel the message arrived in.</param>
    private void PostSignalRNotification(string channelId)
    {
        try
        {
            var plan = NotificationPayloadContract.MapToNotification(new NotificationPayload
            {
                V = NotificationPayloadContract.PayloadVersion,
                Type = NotificationPayloadContract.PayloadTypeMessage,
                ChannelId = channelId,
            });

            ChatNotificationRenderer.Render(
                global::Android.App.Application.Context, plan, _serverBaseUrl);
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Posting the chat notification failed: {ex.Message}");
        }
    }
#endif
}
