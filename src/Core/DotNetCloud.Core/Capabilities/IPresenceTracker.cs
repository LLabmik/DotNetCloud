namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides user presence tracking capabilities (online/offline status, last-seen timestamps).
/// This is a <b>Public</b> tier capability, automatically granted to all modules.
/// </summary>
/// <remarks>
/// <para>
/// Modules use this interface to query whether users are currently online and when they
/// were last seen. Presence is determined by active SignalR connections.
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
    /// Gets the online/offline status for multiple users at once.
    /// </summary>
    /// <param name="userIds">The user IDs to check.</param>
    /// <returns>A dictionary mapping each user ID to their online status.</returns>
    Task<IReadOnlyDictionary<Guid, bool>> GetOnlineStatusAsync(IEnumerable<Guid> userIds);

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
