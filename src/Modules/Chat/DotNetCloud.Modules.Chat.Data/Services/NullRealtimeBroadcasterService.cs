using DotNetCloud.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Null-object implementation of <see cref="IRealtimeBroadcaster"/> for use in
/// process-isolated module hosts where the in-process SignalR broadcaster is
/// not available. Silently discards all broadcast requests.
/// </summary>
internal sealed class NullRealtimeBroadcasterService : IRealtimeBroadcaster
{
    private readonly ILogger<NullRealtimeBroadcasterService> _logger;

    public NullRealtimeBroadcasterService(ILogger<NullRealtimeBroadcasterService> logger)
    {
        _logger = logger;
    }

    public Task BroadcastAsync(string group, string eventName, object message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NullRealtimeBroadcaster: BroadcastAsync({Group}, {Event}) discarded (no in-process SignalR available)", group, eventName);
        return Task.CompletedTask;
    }

    public Task SendToUserAsync(Guid userId, string eventName, object message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NullRealtimeBroadcaster: SendToUserAsync({UserId}, {Event}) discarded", userId, eventName);
        return Task.CompletedTask;
    }

    public Task SendToRoleAsync(string role, string eventName, object message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NullRealtimeBroadcaster: SendToRoleAsync({Role}, {Event}) discarded", role, eventName);
        return Task.CompletedTask;
    }

    public Task AddToGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NullRealtimeBroadcaster: AddToGroupAsync({UserId}, {Group}) discarded", userId, group);
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NullRealtimeBroadcaster: RemoveFromGroupAsync({UserId}, {Group}) discarded", userId, group);
        return Task.CompletedTask;
    }
}
