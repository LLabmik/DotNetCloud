using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Host.Services;

/// <summary>
/// gRPC-based implementation of <see cref="IRealtimeBroadcaster"/> for use in
/// process-isolated module hosts. Forwards broadcast requests to Core.Server's
/// SignalR infrastructure via the CoreCapabilities gRPC service.
/// </summary>
internal sealed class GrpcRealtimeBroadcaster : IRealtimeBroadcaster, IDisposable
{
    private readonly ILogger<GrpcRealtimeBroadcaster> _logger;
    private readonly GrpcChannel _channel;
    private readonly CoreCapabilities.CoreCapabilitiesClient _client;
    private readonly string _moduleId;
    private bool _disposed;

    public GrpcRealtimeBroadcaster(ILogger<GrpcRealtimeBroadcaster> logger)
    {
        _logger = logger;

        var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(coreEndpoint))
        {
            _logger.LogWarning("DOTNETCLOUD_CORE_ENDPOINT not set — broadcasts will be discarded");
            _channel = null!;
            _client = null!;
            _moduleId = "unknown";
            return;
        }

        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";
        var address = coreEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://");

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
            "GrpcRealtimeBroadcaster: connected to Core.Server at {Endpoint} (module: {ModuleId})",
            address, _moduleId);
    }

    public async Task BroadcastAsync(string group, string eventName, object message, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return;

        try
        {
            var payloadJson = JsonSerializer.Serialize(message);
            var metadata = new Metadata { { "module-id", _moduleId } };

            await _client.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Group = group,
                EventName = eventName,
                PayloadJson = payloadJson,
            }, metadata, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BroadcastRealtimeEvent failed: group={Group}, event={Event}", group, eventName);
        }
    }

    public async Task SendToUserAsync(Guid userId, string eventName, object message, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return;

        try
        {
            var payloadJson = JsonSerializer.Serialize(message);
            var metadata = new Metadata { { "module-id", _moduleId } };

            await _client.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                TargetUserId = userId.ToString(),
                EventName = eventName,
                PayloadJson = payloadJson,
            }, metadata, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BroadcastRealtimeEvent.SendToUser failed: userId={UserId}, event={Event}", userId, eventName);
        }
    }

    public async Task SendToRoleAsync(string role, string eventName, object message, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return;

        try
        {
            var payloadJson = JsonSerializer.Serialize(message);
            var metadata = new Metadata { { "module-id", _moduleId } };

            await _client.BroadcastRealtimeEventAsync(new BroadcastRealtimeEventRequest
            {
                Group = $"role:{role}",
                EventName = eventName,
                PayloadJson = payloadJson,
            }, metadata, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BroadcastRealtimeEvent.SendToRole failed: role={Role}, event={Event}", role, eventName);
        }
    }

    public Task AddToGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("AddToGroupAsync({UserId}, {Group}) — no-op (managed by Core.Server)", userId, group);
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("RemoveFromGroupAsync({UserId}, {Group}) — no-op (managed by Core.Server)", userId, group);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _channel?.Dispose();
    }
}
