using DotNetCloud.Client.Android.Calendar;
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
}
