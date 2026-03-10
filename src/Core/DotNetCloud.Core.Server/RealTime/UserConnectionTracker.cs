using System.Collections.Concurrent;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Thread-safe service that maps user IDs to their active SignalR connection IDs.
/// Supports one user having multiple concurrent connections (e.g., multiple devices or tabs).
/// </summary>
internal sealed class UserConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToUser = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userGroups = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers a new connection for a user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns><c>true</c> if this is the user's first connection (they just came online); <c>false</c> if they already had connections.</returns>
    public bool AddConnection(Guid userId, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        _connectionToUser[connectionId] = userId;

        lock (_lock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Add(connectionId);
                return false;
            }

            _userConnections[userId] = [connectionId];
            return true;
        }
    }

    /// <summary>
    /// Removes a connection for a user.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID to remove.</param>
    /// <returns>
    /// A tuple of (userId, isLastConnection). If isLastConnection is <c>true</c>,
    /// the user has gone fully offline. Returns <c>null</c> if the connection was not tracked.
    /// </returns>
    public (Guid UserId, bool IsLastConnection)? RemoveConnection(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        if (!_connectionToUser.TryRemove(connectionId, out var userId))
        {
            return null;
        }

        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var connections))
            {
                return (userId, true);
            }

            connections.Remove(connectionId);

            if (connections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);
                return (userId, true);
            }

            return (userId, false);
        }
    }

    /// <summary>
    /// Gets all active connection IDs for a user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <returns>A read-only list of connection IDs, or an empty list if the user is offline.</returns>
    public IReadOnlyList<string> GetConnections(Guid userId)
    {
        lock (_lock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                return [.. connections];
            }

            return [];
        }
    }

    /// <summary>
    /// Gets the user ID associated with a connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns>The user ID, or <c>null</c> if the connection is not tracked.</returns>
    public Guid? GetUserId(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        return _connectionToUser.TryGetValue(connectionId, out var userId) ? userId : null;
    }

    /// <summary>
    /// Checks whether a user has at least one active connection.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <returns><c>true</c> if the user has active connections; otherwise <c>false</c>.</returns>
    public bool IsOnline(Guid userId)
    {
        return _userConnections.ContainsKey(userId);
    }

    /// <summary>
    /// Gets all currently online user IDs.
    /// </summary>
    /// <returns>A read-only set of user IDs with active connections.</returns>
    public IReadOnlySet<Guid> GetOnlineUsers()
    {
        return _userConnections.Keys.ToHashSet();
    }

    /// <summary>
    /// Gets the total number of active connections across all users.
    /// </summary>
    /// <returns>The total active connection count.</returns>
    public int GetTotalConnectionCount()
    {
        return _connectionToUser.Count;
    }

    /// <summary>
    /// Gets the number of currently online users.
    /// </summary>
    /// <returns>The number of users with at least one active connection.</returns>
    public int GetOnlineUserCount()
    {
        return _userConnections.Count;
    }

    /// <summary>
    /// Adds a persistent group membership for the specified user.
    /// Membership is retained while offline and re-applied on reconnect.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="groupName">The SignalR group name.</param>
    public void AddGroupMembership(Guid userId, string groupName)
    {
        ArgumentNullException.ThrowIfNull(groupName);

        lock (_lock)
        {
            if (_userGroups.TryGetValue(userId, out var groups))
            {
                groups.Add(groupName);
                return;
            }

            _userGroups[userId] = [groupName];
        }
    }

    /// <summary>
    /// Removes a persistent group membership for the specified user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="groupName">The SignalR group name.</param>
    public void RemoveGroupMembership(Guid userId, string groupName)
    {
        ArgumentNullException.ThrowIfNull(groupName);

        lock (_lock)
        {
            if (!_userGroups.TryGetValue(userId, out var groups))
            {
                return;
            }

            groups.Remove(groupName);
            if (groups.Count == 0)
            {
                _userGroups.TryRemove(userId, out _);
            }
        }
    }

    /// <summary>
    /// Gets the persistent group memberships for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user group names, or an empty list when none are tracked.</returns>
    public IReadOnlyList<string> GetGroups(Guid userId)
    {
        lock (_lock)
        {
            if (_userGroups.TryGetValue(userId, out var groups))
            {
                return [.. groups];
            }

            return [];
        }
    }
}
