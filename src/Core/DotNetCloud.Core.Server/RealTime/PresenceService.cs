using System.Collections.Concurrent;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Tracks user presence (online/offline/away/do-not-disturb and last-seen timestamps)
/// and derives the canonical 4-state display value used by presence dots.
/// Implements <see cref="IPresenceTracker"/> for module consumption.
/// </summary>
/// <remarks>
/// The display state is derived server-authoritatively as:
/// <code>
/// state(user):
///   if not online(user)                       -> Offline       (gray)
///   else if DND(user)                         -> DoNotDisturb  (red)
///   else if (UtcNow - lastActivity(user)) &lt;= idle  -> Online  (green)
///   else                                       -> Away         (yellow)
/// </code>
/// Priorities: Offline &gt; DoNotDisturb &gt; Away/Online — a DND user who disconnects shows gray,
/// and an inactive-but-DND user shows red (DND wins over Away while connected).
/// <para>
/// Connection-based on/offline (<see cref="UserConnectionTracker"/>) is the boolean source of
/// truth for push suppression and counts; this class additionally maintains the derived state
/// and raises <see cref="PresenceStateChanged"/> whenever a derived transition occurs so a
/// subscriber can broadcast it to peers (idle sweep, activity reports, DND toggles).
/// </para>
/// </remarks>
internal sealed class PresenceService : IPresenceTracker
{
    /// <summary>
    /// Default idle threshold (how long without real interaction before a connected user
    /// is shown as <see cref="PresenceState.Away"/>). Overridden by the admin setting
    /// <c>PresenceIdleTimeoutMinutes</c> via <see cref="UpdateIdleThreshold"/>.
    /// </summary>
    internal static readonly TimeSpan DefaultIdleThreshold = TimeSpan.FromMinutes(3);

    private readonly UserConnectionTracker _connectionTracker;
    private readonly IDbContextFactory<ChatDbContext>? _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = new();
    private readonly ConcurrentDictionary<Guid, PresenceDto> _presence = new();
    private readonly ConcurrentDictionary<Guid, bool> _doNotDisturb = new();
    private readonly ILogger<PresenceService> _logger;
    private TimeSpan _idleThreshold = DefaultIdleThreshold;

    /// <summary>
    /// Raised when a user's derived display state changes (idle sweep, activity report, or
    /// DND toggle). A subscriber (the presence change publisher) broadcasts the transition
    /// to CoreHub clients and in-process Blazor subscribers.
    /// </summary>
    internal event Action<Guid, PresenceState>? PresenceStateChanged;

    public PresenceService(
        UserConnectionTracker connectionTracker,
        ILogger<PresenceService> logger,
        IDbContextFactory<ChatDbContext>? dbContextFactory = null,
        TimeProvider? timeProvider = null)
    {
        _connectionTracker = connectionTracker;
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>
    /// Updates the idle threshold used to derive <see cref="PresenceState.Online"/> vs
    /// <see cref="PresenceState.Away"/>. Called by the activity monitor on each sweep so
    /// admin changes apply at runtime (no restart).
    /// </summary>
    /// <param name="threshold">The idle threshold.</param>
    internal void UpdateIdleThreshold(TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero)
        {
            threshold = DefaultIdleThreshold;
        }

        _idleThreshold = threshold;
    }

    /// <summary>
    /// Records that a user has established a connection and is now online.
    /// Seeds the persisted do-not-disturb flag (read once at connect) so a DND user appears
    /// red immediately. The resulting display state is returned so the caller can broadcast it.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns>The user's derived display state (<see cref="PresenceState.Online"/> or
    /// <see cref="PresenceState.DoNotDisturb"/>).</returns>
    internal async Task<PresenceState> UserConnectedAsync(Guid userId, string connectionId)
    {
        var now = UtcNow;
        _lastSeen[userId] = now;

        await ReadAndCacheDoNotDisturbAsync(userId).ConfigureAwait(false);
        var state = _doNotDisturb.TryGetValue(userId, out var dnd) && dnd
            ? PresenceState.DoNotDisturb
            : PresenceState.Online;

        SetTrackedState(userId, state, now);

        _logger.LogInformation(
            "User {UserId} is now online with presence {Presence} (connection: {ConnectionId})",
            userId, state, connectionId);

        return state;
    }

    /// <summary>
    /// Records that a user's last connection has dropped and they are now offline.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="connectionId">The last connection ID.</param>
    /// <returns>Always <see cref="PresenceState.Offline"/> (DND is never shown while offline).</returns>
    internal Task<PresenceState> UserDisconnectedAsync(Guid userId, string connectionId)
    {
        var now = UtcNow;
        _lastSeen[userId] = now;
        SetTrackedState(userId, PresenceState.Offline, now);

        _logger.LogInformation(
            "User {UserId} is now offline (last connection: {ConnectionId})",
            userId, connectionId);

        return Task.FromResult(PresenceState.Offline);
    }

    /// <summary>
    /// Updates the cached do-not-disturb flag for a user (e.g. when they connect with DND
    /// already persisted) without touching their tracked state.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="enabled">Whether DND is enabled.</param>
    internal void CacheDoNotDisturb(Guid userId, bool enabled)
    {
        _doNotDisturb[userId] = enabled;
    }

    /// <summary>
    /// Applies a do-not-disturb toggle to a user's presence: caches the flag and, when the
    /// user is online, immediately recomputes their display state (DND wins over Away/Online;
    /// gray still wins while offline). Raises <see cref="PresenceStateChanged"/> on change so
    /// peers are notified right away (no waiting for the sweep).
    /// </summary>
    /// <param name="userId">The user whose DND changed.</param>
    /// <param name="enabled">The new DND value.</param>
    internal void SetDoNotDisturb(Guid userId, bool enabled)
    {
        _doNotDisturb[userId] = enabled;

        if (!_connectionTracker.IsOnline(userId))
        {
            _logger.LogDebug("User {UserId} toggled DND to {Enabled} while offline; presence unchanged", userId, enabled);
            return;
        }

        var now = UtcNow;
        var state = enabled ? PresenceState.DoNotDisturb : DeriveActivityState(userId, now, idle: _idleThreshold);
        ApplyStateIfChanged(userId, state, now);
    }

    /// <summary>
    /// Reports genuine user activity, resetting the user's idle clock. When the user is online
    /// this flips an idle (<see cref="PresenceState.Away"/>) user back to green immediately and
    /// raises <see cref="PresenceStateChanged"/>; reports while offline are recorded (last seen)
    /// but produce no broadcast.
    /// </summary>
    /// <param name="userId">The active user's ID.</param>
    /// <returns>A task representing the asynchronous report operation.</returns>
    public async Task ReportActivityAsync(Guid userId)
    {
        var now = UtcNow;
        _lastSeen[userId] = now;

        if (!_connectionTracker.IsOnline(userId))
        {
            return;
        }

        // Activity is fresh by definition — DND still wins, otherwise green.
        var state = _doNotDisturb.TryGetValue(userId, out var dnd) && dnd
            ? PresenceState.DoNotDisturb
            : PresenceState.Online;

        ApplyStateIfChanged(userId, state, now);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the last-seen timestamp for a user without touching their derived state.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    internal Task UpdateLastSeenAsync(Guid userId)
    {
        _lastSeen[userId] = UtcNow;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Recomputes a user's display state from the current connection/DND/activity inputs and,
    /// when it changed from the tracked state, applies + raises <see cref="PresenceStateChanged"/>.
    /// Used by the idle sweep monitor to flip Online → Away at the threshold and Away → Online
    /// after activity.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <returns>The user's current display state after recompute.</returns>
    internal Task<PresenceState> RecomputeAsync(Guid userId)
    {
        var now = UtcNow;

        if (!_connectionTracker.IsOnline(userId))
        {
            SetTrackedState(userId, PresenceState.Offline, now);
            return Task.FromResult(PresenceState.Offline);
        }

        var state = DeriveDisplayState(userId, now, idle: _idleThreshold);
        ApplyStateIfChanged(userId, state, now);
        return Task.FromResult(state);
    }

    /// <summary>
    /// Sets the user's chat presence state and optional custom status message.
    /// </summary>
    /// <remarks>
    /// This is the dormant custom-status entry point (kept for a future "set my status"
    /// feature). The derived 4-state display can still override the status on the next
    /// recompute/activity/DND event.
    /// </remarks>
    /// <param name="userId">The user ID.</param>
    /// <param name="status">Presence status ("Online", "Away", "DoNotDisturb", "Offline").</param>
    /// <param name="statusMessage">Optional custom status message.</param>
    /// <returns>The updated presence state.</returns>
    internal Task<PresenceDto> SetPresenceAsync(Guid userId, string status, string? statusMessage)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Presence status is required.", nameof(status));

        if (!Enum.TryParse<PresenceState>(status, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Unsupported presence status '{status}'.", nameof(status));

        var now = UtcNow;
        _lastSeen[userId] = now;

        var presence = new PresenceDto
        {
            UserId = userId,
            Status = parsed.ToString(),
            StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? null : statusMessage.Trim(),
            LastSeenAt = now
        };

        _presence[userId] = presence;
        return Task.FromResult(presence);
    }

    /// <summary>
    /// Gets the tracked chat presence state for a user (the dormant custom-status DTO).
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
            Status = _connectionTracker.IsOnline(userId) ? PresenceState.Online.ToString() : PresenceState.Offline.ToString(),
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
    public Task<IReadOnlyDictionary<Guid, PresenceState>> GetOnlineStatusAsync(IEnumerable<Guid> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var now = UtcNow;
        var result = new Dictionary<Guid, PresenceState>();
        foreach (var userId in userIds)
        {
            result[userId] = _connectionTracker.IsOnline(userId)
                ? DeriveDisplayState(userId, now, idle: _idleThreshold)
                : PresenceState.Offline;
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, PresenceState>>(result);
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

    /// <summary>
    /// Gets the current tracked display state for a user (defaults to <see cref="PresenceState.Offline"/>).
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <returns>The tracked display state.</returns>
    internal PresenceState GetDisplayState(Guid userId)
    {
        if (_presence.TryGetValue(userId, out var presence)
            && Enum.TryParse<PresenceState>(presence.Status, ignoreCase: true, out var state))
        {
            return state;
        }

        return PresenceState.Offline;
    }

    /// <summary>
    /// The current UTC time, from the injectable <see cref="TimeProvider"/> (defaults to the system clock).
    /// </summary>
    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Derives the display state for a user from current inputs without mutating tracked state.
    /// </summary>
    private PresenceState DeriveDisplayState(Guid userId, DateTime now, TimeSpan idle)
    {
        var online = _connectionTracker.IsOnline(userId);
        var dnd = _doNotDisturb.TryGetValue(userId, out var flag) && flag;
        var lastSeen = _lastSeen.TryGetValue(userId, out var seenAt) ? seenAt : (DateTime?)null;
        return DeriveState(online, dnd, lastSeen, now, idle);
    }

    /// <summary>
    /// Derives the activity-based state (Online vs Away vs DoNotDisturb) for an online user.
    /// </summary>
    private PresenceState DeriveActivityState(Guid userId, DateTime now, TimeSpan idle)
    {
        var dnd = _doNotDisturb.TryGetValue(userId, out var flag) && flag;
        var lastSeen = _lastSeen.TryGetValue(userId, out var seenAt) ? seenAt : (DateTime?)null;
        return DeriveState(online: true, dnd, lastSeen, now, idle);
    }

    private static PresenceState DeriveState(bool online, bool dnd, DateTime? lastSeen, DateTime now, TimeSpan idle)
    {
        if (!online)
        {
            return PresenceState.Offline;
        }

        if (dnd)
        {
            return PresenceState.DoNotDisturb;
        }

        if (lastSeen is null || now - lastSeen.Value <= idle)
        {
            return PresenceState.Online;
        }

        return PresenceState.Away;
    }

    /// <summary>
    /// Writes the tracked presence state without raising the change event (used on explicit
    /// connect/disconnect paths where the caller performs the broadcast).
    /// </summary>
    private void SetTrackedState(Guid userId, PresenceState state, DateTime lastSeenAt)
    {
        var statusMessage = _presence.TryGetValue(userId, out var existing) ? existing.StatusMessage : null;
        _presence[userId] = new PresenceDto
        {
            UserId = userId,
            Status = state.ToString(),
            StatusMessage = statusMessage,
            LastSeenAt = lastSeenAt
        };
    }

    /// <summary>
    /// Applies a derived state change and raises <see cref="PresenceStateChanged"/> when the
    /// state actually differs from the currently tracked value.
    /// </summary>
    private void ApplyStateIfChanged(Guid userId, PresenceState state, DateTime lastSeenAt)
    {
        var previous = GetDisplayState(userId);
        SetTrackedState(userId, state, lastSeenAt);

        if (previous == state)
        {
            return;
        }

        _logger.LogDebug("User {UserId} presence transitioned {Previous} -> {Current}", userId, previous, state);
        PresenceStateChanged?.Invoke(userId, state);
    }

    /// <summary>
    /// Reads the user's persisted do-not-disturb preference once and caches it. Falls back to
    /// the existing cache (or <c>false</c>) if the DB read fails so presence never crashes on a
    /// transient DB issue.
    /// </summary>
    private async Task ReadAndCacheDoNotDisturbAsync(Guid userId)
    {
        if (_dbContextFactory is null)
        {
            return;
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var entity = await db.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId)
                .ConfigureAwait(false);

            _doNotDisturb[userId] = entity?.DoNotDisturb ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read DND preference for user {UserId}; using cached/default", userId);
        }
    }
}
