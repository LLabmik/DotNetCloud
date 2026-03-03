namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides real-time broadcasting capabilities to connected clients via SignalR.
/// This is a <b>Public</b> tier capability, automatically granted to all modules.
/// </summary>
/// <remarks>
/// <para>
/// Modules use this interface to push real-time updates to connected users without
/// directly depending on SignalR. The core process owns the SignalR hub; modules
/// request broadcasts through this capability interface.
/// </para>
/// <para>
/// <b>Usage Flow:</b>
/// <code>
/// Module (e.g., Chat) → IRealtimeBroadcaster.BroadcastAsync("channel-123", payload)
///                      → Core SignalR hub broadcasts to all clients in group "channel-123"
/// </code>
/// </para>
/// </remarks>
public interface IRealtimeBroadcaster : ICapabilityInterface
{
    /// <summary>
    /// Broadcasts a message to all clients in the specified group.
    /// </summary>
    /// <param name="group">The group name (e.g., a channel ID, room ID, or topic).</param>
    /// <param name="eventName">The client-side event name to invoke.</param>
    /// <param name="message">The payload to send. Must be JSON-serializable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastAsync(string group, string eventName, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to all connections of a specific user.
    /// </summary>
    /// <param name="userId">The target user's ID.</param>
    /// <param name="eventName">The client-side event name to invoke.</param>
    /// <param name="message">The payload to send. Must be JSON-serializable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendToUserAsync(Guid userId, string eventName, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to all connected users who have the specified role.
    /// </summary>
    /// <param name="role">The role name (e.g., "Administrator").</param>
    /// <param name="eventName">The client-side event name to invoke.</param>
    /// <param name="message">The payload to send. Must be JSON-serializable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendToRoleAsync(string role, string eventName, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user to a broadcast group so they receive future broadcasts for that group.
    /// </summary>
    /// <param name="userId">The user to add.</param>
    /// <param name="group">The group name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddToGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a broadcast group.
    /// </summary>
    /// <param name="userId">The user to remove.</param>
    /// <param name="group">The group name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveFromGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default);
}
