using Android.Util;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// Server payload for unread count updates: { channelId, count }.
/// </summary>
internal sealed record UnreadCountUpdatedPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("count")] int Count);

/// <summary>
/// Lightweight client-side mirror of the server's MessageDto for SignalR deserialization.
/// Only includes the fields needed for real-time display; full message is fetched on scroll-back.
/// </summary>
internal sealed record SignalRMessageDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("senderUserId")] Guid SenderUserId,
    [property: JsonPropertyName("senderName")] string? SenderName,
    [property: JsonPropertyName("sentAt")] DateTime SentAt);

/// <summary>
/// Server payload for new messages: { channelId, message }.
/// </summary>
internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] SignalRMessageDto Message);

/// <summary>
/// <see cref="IChatSignalRClient"/> implementation that maintains a persistent SignalR
/// connection to the DotNetCloud chat hub. Designed to be long-lived as a singleton;
/// the foreground service keeps it alive when the app is backgrounded.
/// </summary>
internal sealed class SignalRChatClient : IChatSignalRClient, IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ILogger<SignalRChatClient> _logger;
    private readonly IPendingMessageQueue _pendingQueue;
    private readonly IChatRestClient _restClient;
    private readonly ISecureTokenStore _tokenStore;
    private string? _serverBaseUrl;

    // Tracks channel groups joined so they can be re-joined after reconnection.
    private readonly ConcurrentDictionary<Guid, byte> _joinedChannels = new();

    /// <inheritdoc />
    public event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <inheritdoc />
    public event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <summary>Initializes a new <see cref="SignalRChatClient"/>.</summary>
    public SignalRChatClient(
        ILogger<SignalRChatClient> logger,
        IPendingMessageQueue pendingQueue,
        IChatRestClient restClient,
        ISecureTokenStore tokenStore)
    {
        _logger = logger;
        _pendingQueue = pendingQueue;
        _restClient = restClient;
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
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);

        _serverBaseUrl = serverBaseUrl;

        var hubUrl = $"{serverBaseUrl.TrimEnd('/')}/hubs/core";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Fetch a fresh token on every connect/reconnect to handle expiry.
                // Falls back to the injected ISecureTokenStore which handles refresh flows.
                options.AccessTokenProvider = async () =>
                    await _tokenStore.GetAccessTokenAsync(serverBaseUrl).ConfigureAwait(false);
                options.HttpMessageHandlerFactory = static _ => OAuthHttpClientHandlerFactory.CreateHandler();
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)])
            .Build();

        _hub.On<UnreadCountUpdatedPayload>("UnreadCountUpdated", payload =>
            OnUnreadCountUpdated?.Invoke(this, new ChatUnreadCountUpdatedEventArgs(payload.ChannelId, payload.Count, false)));

        _hub.On<NewMessagePayload>("NewMessage", payload =>
        {
            var senderName = !string.IsNullOrEmpty(payload.Message.SenderName)
                ? payload.Message.SenderName
                : payload.Message.SenderUserId.ToString();
            Log.Info("DotNetCloud", $"SignalRChatClient: NewMessage received! channelId={payload.ChannelId}, content='{payload.Message.Content}', senderName='{senderName}', sentAt={payload.Message.SentAt:O}");
            OnNewChatMessage?.Invoke(this, new ChatMessageReceivedEventArgs(
                payload.ChannelId,
                string.Empty,
                senderName,
                payload.Message.Content,
                payload.Message.Id,
                payload.Message.SentAt,
                false,
                payload.Message.SenderUserId));
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

            await FlushPendingMessagesAsync().ConfigureAwait(false);
        };
        _hub.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed.");
            return Task.CompletedTask;
        };

        try
        {
            await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SignalR connected to {HubUrl}.", hubUrl);
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

    private async Task FlushPendingMessagesAsync()
    {
        if (_serverBaseUrl is null)
            return;

        var accessToken = await _tokenStore.GetAccessTokenAsync(_serverBaseUrl).ConfigureAwait(false);
        if (accessToken is null)
            return;

        var pending = await _pendingQueue.GetAllAsync().ConfigureAwait(false);
        if (pending.Count == 0)
            return;

        _logger.LogInformation("Flushing {Count} pending message(s) after reconnect.", pending.Count);
        var flushed = new List<long>(pending.Count);
        foreach (var msg in pending)
        {
            try
            {
                await _restClient.SendMessageAsync(
                    _serverBaseUrl, accessToken,
                    msg.ChannelId, msg.Content)
                    .ConfigureAwait(false);
                flushed.Add(msg.RowId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush pending message {RowId}; will retry on next reconnect.", msg.RowId);
                break; // stop on first failure to preserve ordering
            }
        }

        if (flushed.Count > 0)
            await _pendingQueue.RemoveAsync(flushed).ConfigureAwait(false);
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
            Log.Warn("DotNetCloud", $"JoinChannelGroupAsync: cannot join {channelId}: hub not connected (state={_hub?.State}).");
            _logger.LogWarning("Cannot join channel group {ChannelId}: hub not connected (state={State}); tracked for retry on reconnect.",
                channelId, _hub?.State);
            return;
        }

        var groupName = $"chat-channel-{channelId}";
        Log.Info("DotNetCloud", $"JoinChannelGroupAsync: invoking JoinGroupAsync('{groupName}')...");
        try
        {
            await _hub.InvokeAsync("JoinGroupAsync", groupName, cancellationToken).ConfigureAwait(false);
            Log.Info("DotNetCloud", $"JoinChannelGroupAsync: successfully joined group '{groupName}'.");
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"JoinChannelGroupAsync: FAILED to join group '{groupName}': {ex.Message}");
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
}
