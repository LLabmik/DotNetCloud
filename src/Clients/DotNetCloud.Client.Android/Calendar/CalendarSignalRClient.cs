using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Calendar;

/// <summary>
/// Thin wrapper around <see cref="ICoreHubClient"/> for calendar real-time notifications.
/// Instead of owning its own HubConnection, it subscribes to the shared
/// <see cref="ICoreHubClient.CalendarsChanged"/> event. This consolidates chat
/// and calendar events onto a single SignalR WebSocket.
/// </summary>
internal sealed class CalendarSignalRClient : ICalendarSignalRClient
{
    private readonly ICoreHubClient _coreHub;
    private readonly ICalendarReminderScheduler _reminderScheduler;
    private readonly ILogger<CalendarSignalRClient> _logger;

    /// <summary>Initializes a new <see cref="CalendarSignalRClient"/>.</summary>
    public CalendarSignalRClient(
        ICoreHubClient coreHub,
        ICalendarReminderScheduler reminderScheduler,
        ILogger<CalendarSignalRClient> logger)
    {
        _coreHub = coreHub;
        _reminderScheduler = reminderScheduler;
        _logger = logger;

        _coreHub.CalendarsChanged += OnCalendarsChanged;
    }

    /// <inheritdoc />
    public event Action? CalendarsChanged;

    /// <inheritdoc />
    public async Task ConnectAsync(string serverBaseUrl, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        // The shared ICoreHubClient already handles the connection.
        // If it's not yet connected, forward the call.
        await _coreHub.ConnectAsync(serverBaseUrl, accessToken, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DisconnectAsync()
    {
        // The shared ICoreHubClient manages its own lifecycle.
        // Calendar-specific disconnect is a no-op — disconnecting the shared
        // connection would break chat. Individual disconnects are handled
        // at the application level.
        return Task.CompletedTask;
    }

    private void OnCalendarsChanged()
    {
        _logger.LogDebug("CalendarSignalR: CalendarsChanged forwarded from CoreHub.");
        CalendarsChanged?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _coreHub.CalendarsChanged -= OnCalendarsChanged;
        await Task.CompletedTask;
    }
}
