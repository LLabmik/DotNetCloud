using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>Scope selection when editing/deleting a recurring event.</summary>
public enum EditScope { ThisOccurrence, AllEvents }

/// <summary>ViewModel for the event detail page.</summary>
[QueryProperty(nameof(EventIdString), "EventId")]
public sealed partial class EventDetailViewModel : ObservableObject
{
    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<EventDetailViewModel> _logger;

    private Guid _eventId;

    /// <summary>Initializes a new <see cref="EventDetailViewModel"/>.</summary>
    public EventDetailViewModel(
        ICalendarRestClient calendarApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<EventDetailViewModel> logger)
    {
        _calendarApi = calendarApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // ── Navigation Params ─────────────────────────────────────────

    /// <summary>Event ID received from navigation query string.</summary>
    [ObservableProperty]
    private string? _eventIdString;

    partial void OnEventIdStringChanged(string? value)
    {
        if (Guid.TryParse(value, out var id))
        {
            _eventId = id;
            LoadEventCommand.Execute(null);
        }
    }

    // ── State ──────────────────────────────────────────────────────

    /// <summary>The loaded event.</summary>
    [ObservableProperty]
    private CalendarEventDto? _event;

    /// <summary>The calendar this event belongs to.</summary>
    [ObservableProperty]
    private CalendarDto? _calendar;

    /// <summary>Whether data is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Error message to display, or null.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Whether this event is recurring (has RRULE or is an exception).</summary>
    [ObservableProperty]
    private bool _isRecurring;

    /// <summary>Human-readable recurrence description (e.g., "Every week on Mon, Wed, Fri").</summary>
    [ObservableProperty]
    private string? _recurrenceDescription;

    /// <summary>Formatted date/time range for display.</summary>
    [ObservableProperty]
    private string? _dateTimeDisplay;

    /// <summary>Attendee list as display strings.</summary>
    public ObservableCollection<string> AttendeeDisplayList { get; } = [];

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>Loads event details from the server.</summary>
    [RelayCommand]
    private async Task LoadEventAsync(CancellationToken ct)
    {
        if (_eventId == Guid.Empty)
            return;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);

            var evt = await _calendarApi.GetEventAsync(serverUrl, token, _eventId, ct);
            Event = evt;

            // Load the parent calendar for name/color display
            if (evt.CalendarId != Guid.Empty)
            {
                try
                {
                    Calendar = await _calendarApi.GetCalendarAsync(serverUrl, token, evt.CalendarId, ct);
                }
                catch
                {
                    // Calendar lookup is best-effort for display
                }
            }

            // Recurrence detection
            IsRecurring = !string.IsNullOrEmpty(evt.RecurrenceRule) || evt.RecurringEventId.HasValue;
            if (!string.IsNullOrEmpty(evt.RecurrenceRule))
                RecurrenceDescription = DescribeRrule(evt.RecurrenceRule);
            else if (evt.RecurringEventId.HasValue)
                RecurrenceDescription = "Recurring event exception";

            // Format date/time display
            DateFormatHelper.FormatEventDateTime(evt, out var dateDisplay);
            DateTimeDisplay = dateDisplay;

            // Build attendee display
            AttendeeDisplayList.Clear();
            foreach (var attendee in evt.Attendees)
            {
                var statusIcon = attendee.Status switch
                {
                    AttendeeStatus.Accepted => "✓",
                    AttendeeStatus.Declined => "✗",
                    AttendeeStatus.Tentative => "?",
                    _ => "○"
                };
                AttendeeDisplayList.Add($"{statusIcon} {attendee.DisplayName ?? attendee.Email} ({attendee.Role})");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load event {EventId}.", _eventId);
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>RSVPs to the event with the given status.</summary>
    [RelayCommand]
    private async Task RsvpAsync(string status, CancellationToken ct)
    {
        if (_eventId == Guid.Empty)
            return;

        try
        {
            if (!Enum.TryParse<AttendeeStatus>(status, out var attendeeStatus))
                return;

            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var dto = new EventRsvpDto { Status = attendeeStatus };
            await _calendarApi.RsvpAsync(serverUrl, token, _eventId, dto, ct);
            await LoadEventAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to RSVP to event {EventId}.", _eventId);
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
    }

    /// <summary>Navigates to the event edit page. Shows recurrence scope choice for recurring events.</summary>
    [RelayCommand]
    private async Task EditEventAsync()
    {
        if (Event is null)
            return;

        if (IsRecurring)
        {
            var action = await Shell.Current.DisplayActionSheetAsync(
                "Edit Recurring Event", "Cancel", null,
                "Edit This Occurrence", "Edit All Events");

            if (action is null || action == "Cancel")
                return;

            var scope = action == "Edit This Occurrence"
                ? nameof(EditScope.ThisOccurrence)
                : nameof(EditScope.AllEvents);

            var parameters = new Dictionary<string, object>
            {
                ["EventId"] = Event.Id.ToString(),
                ["EditScope"] = scope,
            };

            if (Event.OriginalStartUtc.HasValue)
                parameters["OriginalStartUtc"] = Event.OriginalStartUtc.Value.ToString("O");

            await Shell.Current.GoToAsync("EventEdit", parameters);
        }
        else
        {
            await Shell.Current.GoToAsync("EventEdit", new Dictionary<string, object>
            {
                ["EventId"] = Event.Id.ToString()
            });
        }
    }

    /// <summary>Deletes the event after confirmation. Shows recurrence scope choice for recurring events.</summary>
    [RelayCommand]
    private async Task DeleteEventAsync(CancellationToken ct)
    {
        if (Event is null || _eventId == Guid.Empty)
            return;

        try
        {
            if (IsRecurring)
            {
                var action = await Shell.Current.DisplayActionSheetAsync(
                    "Delete Recurring Event", "Cancel", null,
                    "Delete This Occurrence", "Delete All Events");

                if (action is null || action == "Cancel")
                    return;

                if (action == "Delete This Occurrence")
                {
                    // Create a deleted exception by posting an event with same RecurringEventId + OriginalStartUtc
                    // The server interprets a deleted exception when IsDeleted is set or via a specific API.
                    // For now, fall through to regular delete which soft-deletes the occurrence instance.
                }

                var (serverUrl, token) = await GetCredentialsAsync(ct);
                await _calendarApi.DeleteEventAsync(serverUrl, token, _eventId, ct);
            }
            else
            {
                var confirmed = await Shell.Current.DisplayAlertAsync(
                    "Delete Event", $"Delete \"{Event.Title}\"?", "Delete", "Cancel");

                if (!confirmed)
                    return;

                var (serverUrl, token) = await GetCredentialsAsync(ct);
                await _calendarApi.DeleteEventAsync(serverUrl, token, _eventId, ct);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete event {EventId}.", _eventId);
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
    }

    // ── Private Helpers ────────────────────────────────────────────

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }

    private static string DescribeRrule(string rrule)
    {
        if (string.IsNullOrEmpty(rrule))
            return string.Empty;

        var parts = rrule.Split(';').Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");

        var freq = parts.GetValueOrDefault("FREQ", "");
        var interval = parts.GetValueOrDefault("INTERVAL", "1");
        var byDay = parts.GetValueOrDefault("BYDAY", "");
        var count = parts.GetValueOrDefault("COUNT", "");
        var until = parts.GetValueOrDefault("UNTIL", "");

        var freqName = freq switch
        {
            "DAILY" => "day",
            "WEEKLY" => "week",
            "MONTHLY" => "month",
            "YEARLY" => "year",
            _ => freq.ToLower()
        };

        var prefix = interval == "1" ? $"Every {freqName}" : $"Every {interval} {freqName}s";

        if (!string.IsNullOrEmpty(byDay))
        {
            var dayNames = byDay.Split(',').Select(d => d switch
            {
                "MO" => "Mon",
                "TU" => "Tue",
                "WE" => "Wed",
                "TH" => "Thu",
                "FR" => "Fri",
                "SA" => "Sat",
                "SU" => "Sun",
                _ => d
            });
            prefix += $" on {string.Join(", ", dayNames)}";
        }

        if (!string.IsNullOrEmpty(count))
            prefix += $", ending after {count} occurrences";
        else if (!string.IsNullOrEmpty(until))
            prefix += $", ending on {until}";

        return prefix;
    }
}

/// <summary>Helper for formatting event date/times for display.</summary>
internal static class DateFormatHelper
{
    /// <summary>Formats an event's date/time range as a human-readable string.</summary>
    public static void FormatEventDateTime(CalendarEventDto evt, out string display)
    {
        if (evt.IsAllDay)
        {
            var start = evt.StartUtc.ToString("ddd, MMM d");
            var end = evt.EndUtc.Date > evt.StartUtc.Date
                ? evt.EndUtc.AddDays(-1).ToString("ddd, MMM d")
                : null;
            display = end is not null ? $"{start} – {end} (All day)" : $"{start} (All day)";
            return;
        }

        var startStr = evt.StartUtc.ToString("ddd, MMM d · h:mm tt");
        var endStr = evt.EndUtc.Date == evt.StartUtc.Date
            ? evt.EndUtc.ToString("h:mm tt")
            : evt.EndUtc.ToString("ddd, MMM d · h:mm tt");
        display = $"{startStr} – {endStr}";
    }
}
