using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Middleware;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Server-circuit SignalR client that listens for <c>notification.created</c> events
/// on the CoreHub and raises them for Blazor components (e.g. the notification bell).
/// Authenticates by forwarding the circuit's captured auth cookie over HTTP transports.
/// </summary>
internal sealed class RealtimeNotificationClient : IRealtimeNotificationClient, IAsyncDisposable
{
    private readonly CookieCaptureStore _cookieStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RealtimeNotificationClient> _logger;

    private HubConnection? _hub;

    public RealtimeNotificationClient(
        CookieCaptureStore cookieStore,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<RealtimeNotificationClient> logger)
    {
        _cookieStore = cookieStore;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public event Action<NotificationDto>? NotificationCreated;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hub is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_cookieStore.CookieHeader))
        {
            _logger.LogDebug("No auth cookie captured; skipping realtime notification connection.");
            return;
        }

        var httpsPort = _configuration.GetValue<int>("httpsPort", 5443);
        var hubUrl = $"https://localhost:{httpsPort}/hubs/core";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // HTTP transports only: the forwarded auth cookie authenticates the
                // connection. WebSockets would bypass HttpMessageHandlerFactory.
                options.Transports = HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ =>
                {
                    var inner = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = LoopbackCertificateValidator.Validate
                    };

                    return new CookieForwardingHandler(_cookieStore, _httpContextAccessor)
                    {
                        InnerHandler = inner
                    };
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<NotificationDto>("notification.created", notification =>
            NotificationCreated?.Invoke(notification));

        try
        {
            await _hub.StartAsync(cancellationToken);
            _logger.LogInformation("Realtime notification connection started.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start realtime notification connection.");
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}
