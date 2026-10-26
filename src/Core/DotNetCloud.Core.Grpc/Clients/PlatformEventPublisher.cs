using System.Text.Json;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc.Clients;

/// <summary>
/// gRPC implementation of <see cref="IPlatformEventPublisher"/>: hands platform events to
/// Core.Server over the <c>CoreCapabilities.PublishEvent</c> rpc.
/// </summary>
/// <remarks>
/// <para>
/// The channel is the module→core internal channel the supervisor provisions
/// (<c>DOTNETCLOUD_CORE_ENDPOINT</c>). Every call carries the supervisor-issued module credential
/// via <see cref="ModuleIdentityMetadata"/>, so the core derives the publishing identity itself.
/// </para>
/// <para>
/// Unlike the audit logger, a failure here is <b>not</b> swallowed. A background job that cannot
/// publish has not notified anyone; reporting success would record work that never happened.
/// </para>
/// </remarks>
internal sealed class PlatformEventPublisher : IPlatformEventPublisher, IDisposable
{
    private readonly ILogger<PlatformEventPublisher> _logger;
    private readonly string? _coreEndpoint;
    private readonly string _moduleId;
    private GrpcChannel? _channel;
    private CoreCapabilities.CoreCapabilitiesClient? _client;
    private readonly object _sync = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformEventPublisher"/> class.
    /// </summary>
    /// <param name="logger">The logger for this publisher.</param>
    public PlatformEventPublisher(ILogger<PlatformEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = PlatformEventContract.TypeName<TEvent>();

        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            throw new PlatformEventPublishException(
                eventType,
                $"Cannot publish {eventType}: DOTNETCLOUD_CORE_ENDPOINT is not set. " +
                "The module was not launched by ProcessSupervisor, so the core cannot deliver it.");
        }

        var request = new PublishEventRequest
        {
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(@event, typeof(TEvent), PlatformEventContract.JsonOptions)
        };

        try
        {
            var response = await GetClient()
                .PublishEventAsync(
                    request,
                    new CallOptions(
                        headers: ModuleIdentityMetadata.Create(_moduleId),
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (!response.Success)
            {
                throw new PlatformEventPublishException(
                    eventType,
                    $"Core.Server rejected {eventType}: {response.Error}");
            }
        }
        catch (PlatformEventPublishException)
        {
            throw;
        }
        catch (Exception ex) when (ex is RpcException or HttpRequestException or JsonException)
        {
            throw new PlatformEventPublishException(
                eventType,
                $"Failed to publish {eventType} to Core.Server: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gets the raw request the publisher would send, for diagnostics and tests.
    /// </summary>
    /// <typeparam name="TEvent">The platform event type.</typeparam>
    /// <param name="event">The event to encode.</param>
    /// <returns>The protobuf request.</returns>
    internal static PublishEventRequest BuildRequest<TEvent>(TEvent @event)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new PublishEventRequest
        {
            EventType = PlatformEventContract.TypeName<TEvent>(),
            PayloadJson = JsonSerializer.Serialize(@event, typeof(TEvent), PlatformEventContract.JsonOptions)
        };
    }

    private CoreCapabilities.CoreCapabilitiesClient GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is not null)
            return _client;

        lock (_sync)
        {
            if (_client is not null)
                return _client;

            // Convert from the internal URL format used by ProcessSupervisor.
            var address = _coreEndpoint!
                .Replace("unix://", "http://")
                .Replace("net.pipe://", "http://");

            _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false,
                },
                ThrowOperationCanceledOnCancellation = true,
            });

            _client = new CoreCapabilities.CoreCapabilitiesClient(_channel);

            _logger.LogInformation(
                "PlatformEventPublisher: connected to Core.Server at {Endpoint} as module {ModuleId}",
                address, _moduleId);

            return _client;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _channel?.Dispose();
    }
}
