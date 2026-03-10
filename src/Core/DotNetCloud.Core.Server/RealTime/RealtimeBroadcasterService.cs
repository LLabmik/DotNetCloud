using DotNetCloud.Core.Capabilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Implements <see cref="IRealtimeBroadcaster"/> by delegating to the SignalR <see cref="CoreHub"/>.
/// Modules call this service to push real-time messages without depending on SignalR directly.
/// </summary>
internal sealed class RealtimeBroadcasterService : IRealtimeBroadcaster
{
    private readonly IHubContext<CoreHub> _hubContext;
    private readonly UserConnectionTracker _connectionTracker;
    private readonly ILogger<RealtimeBroadcasterService> _logger;

    public RealtimeBroadcasterService(
        IHubContext<CoreHub> hubContext,
        UserConnectionTracker connectionTracker,
        ILogger<RealtimeBroadcasterService> logger)
    {
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(string group, string eventName, object message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(group))
            throw new ArgumentException("Group name cannot be null or empty.", nameof(group));
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name cannot be null or empty.", nameof(eventName));
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug("Broadcasting event {EventName} to group {Group}", eventName, group);

        await _hubContext.Clients.Group(group).SendAsync(eventName, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendToUserAsync(Guid userId, string eventName, object message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name cannot be null or empty.", nameof(eventName));
        ArgumentNullException.ThrowIfNull(message);

        var connections = _connectionTracker.GetConnections(userId);
        if (connections.Count == 0)
        {
            _logger.LogDebug("User {UserId} has no active connections; skipping send for {EventName}", userId, eventName);
            return;
        }

        _logger.LogDebug("Sending event {EventName} to user {UserId} ({Count} connections)", eventName, userId, connections.Count);

        await _hubContext.Clients.Clients(connections).SendAsync(eventName, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendToRoleAsync(string role, string eventName, object message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty.", nameof(role));
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name cannot be null or empty.", nameof(eventName));
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug("Sending event {EventName} to all users with role {Role}", eventName, role);

        // SignalR doesn't natively support role-based groups, so we use a convention:
        // users are added to a group named "role:{roleName}" during connection.
        // This is handled by CoreHub.OnConnectedAsync.
        var roleGroup = $"role:{role}";
        await _hubContext.Clients.Group(roleGroup).SendAsync(eventName, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddToGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(group))
            throw new ArgumentException("Group name cannot be null or empty.", nameof(group));

        _connectionTracker.AddGroupMembership(userId, group);

        var connections = _connectionTracker.GetConnections(userId);

        foreach (var connectionId in connections)
        {
            await _hubContext.Groups.AddToGroupAsync(connectionId, group, cancellationToken);
        }

        _logger.LogDebug("Added user {UserId} to group {Group} ({Count} connections)", userId, group, connections.Count);
    }

    /// <inheritdoc />
    public async Task RemoveFromGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(group))
            throw new ArgumentException("Group name cannot be null or empty.", nameof(group));

        _connectionTracker.RemoveGroupMembership(userId, group);

        var connections = _connectionTracker.GetConnections(userId);

        foreach (var connectionId in connections)
        {
            await _hubContext.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
        }

        _logger.LogDebug("Removed user {UserId} from group {Group} ({Count} connections)", userId, group, connections.Count);
    }
}
