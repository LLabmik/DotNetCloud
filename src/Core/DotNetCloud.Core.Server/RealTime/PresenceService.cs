using System.Collections.Concurrent;
using DotNetCloud.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Tracks user presence (online/offline status and last-seen timestamps).
/// Implements <see cref="IPresenceTracker"/> for module consumption.
/// </summary>
internal sealed class PresenceService : IPresenceTracker
{
    private readonly UserConnectionTracker _connectionTracker;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = new();
    private readonly ILogger<PresenceService> _logger;

    public PresenceService(
        UserConnectionTracker connectionTracker,
        ILogger<PresenceService> logger)
    {
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    /// <summary>
    /// Records that a user has established a connection and is now online.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="connectionId">The SignalR connection ID.</param>
    internal Task UserConnectedAsync(Guid userId, string connectionId)
    {
        _lastSeen[userId] = DateTime.UtcNow;

        _logger.LogInformation("User {UserId} is now online (connection: {ConnectionId})", userId, connectionId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Records that a user's last connection has dropped and they are now offline.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="connectionId">The last connection ID.</param>
    internal Task UserDisconnectedAsync(Guid userId, string connectionId)
    {
        _lastSeen[userId] = DateTime.UtcNow;

        _logger.LogInformation("User {UserId} is now offline (last connection: {ConnectionId})", userId, connectionId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the last-seen timestamp for a user (e.g., from a heartbeat ping).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    internal Task UpdateLastSeenAsync(Guid userId)
    {
        _lastSeen[userId] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsOnlineAsync(Guid userId)
    {
        return Task.FromResult(_connectionTracker.IsOnline(userId));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, bool>> GetOnlineStatusAsync(IEnumerable<Guid> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var result = new Dictionary<Guid, bool>();
        foreach (var userId in userIds)
        {
            result[userId] = _connectionTracker.IsOnline(userId);
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, bool>>(result);
    }

    /// <inheritdoc />
    public Task<DateTime?> GetLastSeenAsync(Guid userId)
    {
        DateTime? result = _lastSeen.TryGetValue(userId, out var lastSeen) ? lastSeen : null;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<Guid>> GetOnlineUsersAsync()
    {
        return Task.FromResult(_connectionTracker.GetOnlineUsers());
    }

    /// <inheritdoc />
    public Task<int> GetActiveConnectionCountAsync()
    {
        return Task.FromResult(_connectionTracker.GetTotalConnectionCount());
    }
}
