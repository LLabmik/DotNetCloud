using DotNetCloud.Client.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// <see cref="IChatSignalRClient"/> implementation that maintains a persistent SignalR
/// connection to the DotNetCloud chat hub. Designed to be long-lived as a singleton;
/// the foreground service keeps it alive when the app is backgrounded.
/// </summary>
internal sealed class SignalRChatClient : IChatSignalRClient, IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ILogger<SignalRChatClient> _logger;

    /// <inheritdoc />
    public event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <inheritdoc />
    public event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <summary>Initializes a new <see cref="SignalRChatClient"/>.</summary>
    public SignalRChatClient(ILogger<SignalRChatClient> logger) => _logger = logger;

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

        var hubUrl = $"{serverBaseUrl.TrimEnd('/')}/hubs/chat";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)])
            .Build();

        _hub.On<string, int, bool>("UnreadCountUpdated", (channelId, count, hasMention) =>
            OnUnreadCountUpdated?.Invoke(this, new ChatUnreadCountUpdatedEventArgs(channelId, count, hasMention)));

        _hub.On<string, string, string, string, bool>("NewMessage", (channelId, channelName, sender, preview, isMention) =>
            OnNewChatMessage?.Invoke(this, new ChatMessageReceivedEventArgs(channelId, channelName, sender, preview, isMention)));

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected (connId={ConnectionId}).", connectionId);
            return Task.CompletedTask;
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);
    }
}
