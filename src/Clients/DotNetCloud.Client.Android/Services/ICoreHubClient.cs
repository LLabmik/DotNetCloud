using DotNetCloud.Client.Core;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Manages a single SignalR connection to the DotNetCloud CoreHub for all real-time events.
/// Consolidates chat, calendar, and future module events into one WebSocket connection.
/// </summary>
public interface ICoreHubClient : IChatSignalRClient
{
    /// <summary>
    /// Raised when the server notifies us of a calendar event change (created/deleted/updated).
    /// Consumers can listen and refresh their data.
    /// </summary>
    event Action? CalendarsChanged;

    /// <summary>
    /// Raised when a user in a joined channel is typing (a heartbeat). The UI hides
    /// the indicator after a short timeout or when that user's message arrives.
    /// </summary>
    event EventHandler<ChatTypingEventArgs>? OnChatTyping;

    /// <summary>
    /// Raised when a user's presence (online/offline) changes over the CoreHub connection.
    /// The server broadcasts this when a peer's first connection opens or last connection
    /// closes — regardless of whether that peer is on the web (Blazor circuit) or native.
    /// </summary>
    event EventHandler<UserPresenceChangedEventArgs>? OnUserPresenceChanged;

    /// <summary>
    /// Raised when the underlying CoreHub connection has re-established after a drop.
    /// A visible page can use this to re-query state (e.g. re-seed DM presence dots).
    /// </summary>
    event EventHandler? Reconnected;

    /// <summary>
    /// Returns online/offline presence for the given user IDs (a current snapshot).
    /// Presence is global: a user is online with any active connection (Blazor circuit
    /// or CoreHub). Returns an empty dictionary when disconnected — callers treat
    /// absent users as offline.
    /// </summary>
    /// <param name="userIds">The user IDs to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary keyed by user ID indicating whether each is online.</returns>
    Task<IReadOnlyDictionary<Guid, bool>> GetPresenceStatusAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct = default);
}
