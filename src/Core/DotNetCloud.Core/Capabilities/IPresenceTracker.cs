using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides user presence tracking capabilities (online/offline status, last-seen timestamps).
/// This is a <b>Public</b> tier capability, automatically granted to all modules.
/// </summary>
/// <remarks>
/// <para>
/// Modules use this interface to query whether users are currently online and when they
/// were last seen. Presence is determined by active SignalR connections plus the derived
/// 4-state display value (see <see cref="PresenceState"/>). Connection-based on/offline
/// semantics are preserved via <see cref="IsOnlineAsync"/> for push-suppression and other
/// boolean consumers; the richer 4-state value is exposed via <see cref="GetOnlineStatusAsync"/>.
/// </para>
/// </remarks>
public interface IPresenceTracker : ICapabilityInterface
{
    /// <summary>
    /// Checks whether a user is currently online (has at least one active connection).
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <returns><c>true</c> if the user has at least one active connection; otherwise <c>false</c>.</returns>
    Task<bool> IsOnlineAsync(Guid userId);

    /// <summary>
    /// Gets the derived 4-state presence value for multiple users at once.
    /// </summary>
    /// <param name="userIds">The user IDs to check.</param>
    /// <returns>
    /// A dictionary mapping each user ID to their presence state. Only connection-derived
    /// states are reported — <see cref="PresenceState.Offline"/> when the user has no active
    /// connection, and <see cref="PresenceState.Online"/> / <see cref="PresenceState.Away"/> /
    /// <see cref="PresenceState.DoNotDisturb"/> when they do.
    /// </returns>
    Task<IReadOnlyDictionary<Guid, PresenceState>> GetOnlineStatusAsync(IEnumerable<Guid> userIds);

    /// <summary>
    /// Reports genuine user activity for the given user, resetting their idle clock.
    /// </summary>
    /// <param name="userId">The active user's ID.</param>
    /// <returns>A task representing the asynchronous report operation.</returns>
    /// <remarks>
    /// Only real client interaction (click/tap/key/scroll/nav/send) should call this —
    /// never transport keepalives. Reporting activity flips an idle (<see cref="PresenceState.Away"/>)
    /// user back to <see cref="PresenceState.Online"/> and is ignored while the user is offline.
    /// </remarks>
    Task ReportActivityAsync(Guid userId);

    /// <summary>
    /// Gets the last-seen timestamp for a user, or <c>null</c> if the user has never connected.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The UTC timestamp of the user's most recent activity, or <c>null</c>.</returns>
    Task<DateTime?> GetLastSeenAsync(Guid userId);

    /// <summary>
    /// Gets the IDs of all currently online users.
    /// </summary>
    /// <returns>A read-only set of user IDs with active connections.</returns>
    Task<IReadOnlySet<Guid>> GetOnlineUsersAsync();

    /// <summary>
    /// Gets the number of currently active connections across all users.
    /// </summary>
    /// <returns>The total active connection count.</returns>
    Task<int> GetActiveConnectionCountAsync();
}
