using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Calendar;

/// <summary>
/// Manages a SignalR connection to the DotNetCloud CoreHub to receive real-time
/// calendar event notifications (deleted/updated) so the client can cancel or
/// reschedule Android alarm reminders.
/// </summary>
public interface ICalendarSignalRClient
{
    /// <summary>
    /// Raised when the server notifies us of a calendar event change (created/deleted/updated).
    /// Consumers can listen and refresh their data.
    /// </summary>
    event Action? CalendarsChanged;

    /// <summary>
    /// Connects to the server's CoreHub and starts listening for calendar events.
    /// If already connected, disconnects and reconnects to the new URL.
    /// </summary>
    Task ConnectAsync(string serverBaseUrl, string? accessToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the hub.
    /// </summary>
    Task DisconnectAsync();
}
