using System.Collections.Concurrent;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Tracks user presence (online/offline status and last-seen timestamps).
/// Implements <see cref="IPresenceTracker"/> for module consumption.
/// </summary>
internal sealed class PresenceService : IPresenceTracker
{
    private static readonly HashSet<string> AllowedStatuses =
    [
        "Online",
        "Away",
        "DoNotDisturb",
        "Offline"
    ];

    private readonly UserConnectionTracker _connectionTracker;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = new();
    private readonly ConcurrentDictionary<Guid, PresenceDto> _presence = new();
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
        var now = DateTime.UtcNow;
        _lastSeen[userId] = now;

        var statusMessage = _presence.TryGetValue(userId, out var existing)
            ? existing.StatusMessage
            : null;
        _presence[userId] = new PresenceDto
        {
            UserId = userId,
            Status = "Online",
            StatusMessage = statusMessage,
            LastSeenAt = now
        };

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
        var now = DateTime.UtcNow;
        _lastSeen[userId] = now;

        var statusMessage = _presence.TryGetValue(userId, out var existing)
            ? existing.StatusMessage
            : null;
        _presence[userId] = new PresenceDto
        {
            UserId = userId,
            Status = "Offline",
            StatusMessage = statusMessage,
            LastSeenAt = now
        };

        _logger.LogInformation("User {UserId} is now offline (last connection: {ConnectionId})", userId, connectionId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the last-seen timestamp for a user (e.g., from a heartbeat ping).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    internal Task UpdateLastSeenAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        _lastSeen[userId] = now;

        if (_presence.TryGetValue(userId, out var current))
        {
            _presence[userId] = current with { LastSeenAt = now };
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the user's chat presence state and optional custom status message.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="status">Presence status (Online, Away, DoNotDisturb, Offline).</param>
    /// <param name="statusMessage">Optional custom status message.</param>
    /// <returns>The updated presence state.</returns>
    internal Task<PresenceDto> SetPresenceAsync(Guid userId, string status, string? statusMessage)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Presence status is required.", nameof(status));

        if (!AllowedStatuses.Contains(status))
            throw new ArgumentException($"Unsupported presence status '{status}'.", nameof(status));

        var now = DateTime.UtcNow;
        _lastSeen[userId] = now;

        var presence = new PresenceDto
        {
            UserId = userId,
            Status = status,
            StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? null : statusMessage.Trim(),
            LastSeenAt = now
        };

        _presence[userId] = presence;
        return Task.FromResult(presence);
    }

    /// <summary>
    /// Gets the tracked chat presence state for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The latest tracked presence state.</returns>
    internal Task<PresenceDto> GetPresenceAsync(Guid userId)
    {
        if (_presence.TryGetValue(userId, out var presence))
        {
            return Task.FromResult(presence);
        }

        var lastSeen = _lastSeen.TryGetValue(userId, out var seenAt) ? seenAt : (DateTime?)null;
        var derived = new PresenceDto
        {
            UserId = userId,
            Status = _connectionTracker.IsOnline(userId) ? "Online" : "Offline",
            StatusMessage = null,
            LastSeenAt = lastSeen
        };

        return Task.FromResult(derived);
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
