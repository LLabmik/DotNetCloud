using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DotNetCloud.Client.Android.Calendar;

/// <summary>
/// <see cref="ICalendarSignalRClient"/> implementation that maintains a SignalR
/// connection to the CoreHub to receive real-time calendar event notifications.
/// On <c>CalendarEventDeleted</c>, cancels the corresponding Android alarm.
/// On <c>CalendarEventUpdated</c>, triggers a full alarm reschedule on reconnect.
/// On initial connect/reconnect, triggers <see cref="ICalendarReminderScheduler.RescheduleAllAsync"/>
/// to sync alarms for events deleted while offline.
/// </summary>
internal sealed class CalendarSignalRClient : ICalendarSignalRClient, IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ICalendarReminderScheduler _reminderScheduler;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<CalendarSignalRClient> _logger;
    private string? _serverBaseUrl;

    /// <summary>Initializes a new <see cref="CalendarSignalRClient"/>.</summary>
    public CalendarSignalRClient(
        ICalendarReminderScheduler reminderScheduler,
        ISecureTokenStore tokenStore,
        ILogger<CalendarSignalRClient> logger)
    {
        _reminderScheduler = reminderScheduler;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(string serverBaseUrl, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);

        _serverBaseUrl = serverBaseUrl;

        var hubUrl = $"{serverBaseUrl.TrimEnd('/')}/hubs/core";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                    await _tokenStore.GetAccessTokenAsync(serverBaseUrl).ConfigureAwait(false);
                options.HttpMessageHandlerFactory = static _ => OAuthHttpClientHandlerFactory.CreateHandler();
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)])
            .Build();

        // ── Wire up handlers ──

        _hub.On<JsonElement>("CalendarEventDeleted", payload =>
        {
            try
            {
                var eventIdStr = payload.GetProperty("eventId").GetString();
                if (Guid.TryParse(eventIdStr, out var eventId))
                {
                    _logger.LogInformation(
                        "CalendarSignalR: event {EventId} deleted — cancelling alarms.", eventId);
                    _reminderScheduler.CancelReminders(eventId);
                    CalendarsChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CalendarSignalR: failed to handle CalendarEventDeleted.");
            }
        });

        _hub.On<JsonElement>("CalendarEventCreated", payload =>
        {
            try
            {
                _logger.LogInformation(
                    "CalendarSignalR: event created — will refresh on next sync.");
                CalendarsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CalendarSignalR: failed to handle CalendarEventCreated.");
            }
        });

        _hub.On<JsonElement>("CalendarEventUpdated", payload =>
        {
            try
            {
                _logger.LogInformation(
                    "CalendarSignalR: event updated — will reschedule on next sync.");
                CalendarsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CalendarSignalR: failed to handle CalendarEventUpdated.");
            }
        });

        // ── Reconnect handler — reschedule all alarms to sync state ──
        _hub.Reconnected += async _ =>
        {
            _logger.LogInformation("CalendarSignalR: reconnected — resyncing alarms.");
            try
            {
                await _reminderScheduler.RescheduleAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CalendarSignalR: alarm resync after reconnect failed.");
            }
        };

        // ── Closed handler — reconnection will trigger Reconnected above ──
        _hub.Closed += async _ =>
        {
            _logger.LogInformation("CalendarSignalR: connection closed — will auto-reconnect.");
            await Task.CompletedTask;
        };

        // ── Start the connection ──
        try
        {
            await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("CalendarSignalR: connected to {HubUrl}.", hubUrl);

            // Sync alarms on initial connect (catches events deleted while offline)
            await _reminderScheduler.RescheduleAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CalendarSignalR: failed to connect to {HubUrl}.", hubUrl);
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (_hub is not null)
        {
            try
            {
                await _hub.StopAsync().ConfigureAwait(false);
            }
            catch { /* best-effort */ }
            await _hub.DisposeAsync().ConfigureAwait(false);
            _hub = null;
        }
    }

    /// <summary>
    /// Raised when the server notifies us of a calendar event change (created/deleted/updated).
    /// Consumers (e.g., CalendarViewModel) can listen and refresh their data.
    /// </summary>
    public event Action? CalendarsChanged;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
