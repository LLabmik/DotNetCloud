using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// Server payload for unread count updates: { channelId, count }.
/// </summary>
internal sealed record UnreadCountUpdatedPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("count")] int Count);

/// <summary>
/// Server payload for new messages: { channelId, message }.
/// </summary>
internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] string Message);

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
    private string? _serverBaseUrl;
    private string? _accessToken;

    /// <inheritdoc />
    public event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <inheritdoc />
    public event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <summary>Initializes a new <see cref="SignalRChatClient"/>.</summary>
    public SignalRChatClient(
        ILogger<SignalRChatClient> logger,
        IPendingMessageQueue pendingQueue,
        IChatRestClient restClient)
    {
        _logger = logger;
        _pendingQueue = pendingQueue;
        _restClient = restClient;
    }

    /// <summary>
    /// Configures and opens the SignalR hub connection to the given server URL.
    /// The connection uses automatic reconnect with exponential back-off.
    /// </summary>
    /// <param name="serverBaseUrl">Root URL of the DotNetCloud server.</param>
    /// <param name="accessToken">Bearer token used for hub authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string serverBaseUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);

        _serverBaseUrl = serverBaseUrl;
        _accessToken = accessToken;

        var hubUrl = $"{serverBaseUrl.TrimEnd('/')}/hubs/core";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)])
            .Build();

        _hub.On<UnreadCountUpdatedPayload>("UnreadCountUpdated", payload =>
            OnUnreadCountUpdated?.Invoke(this, new ChatUnreadCountUpdatedEventArgs(payload.ChannelId, payload.Count, false)));

        _hub.On<NewMessagePayload>("NewMessage", payload =>
            OnNewChatMessage?.Invoke(this, new ChatMessageReceivedEventArgs(payload.ChannelId, string.Empty, string.Empty, payload.Message, false)));

        _hub.Reconnected += async connectionId =>
        {
            _logger.LogInformation("SignalR reconnected (connId={ConnectionId}). Flushing pending messages.", connectionId);
            await FlushPendingMessagesAsync().ConfigureAwait(false);
        };
        _hub.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed.");
            return Task.CompletedTask;
        };

        await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("SignalR connected to {HubUrl}.", hubUrl);
    }

    /// <summary>Implements the parameterless <see cref="IChatSignalRClient.ConnectAsync"/> for compatibility.</summary>
    Task IChatSignalRClient.ConnectAsync(CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException(
            "Use the overload that accepts serverBaseUrl and accessToken."));

    private async Task FlushPendingMessagesAsync()
    {
        if (_serverBaseUrl is null || _accessToken is null)
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
                    _serverBaseUrl, _accessToken,
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
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);
    }
}
