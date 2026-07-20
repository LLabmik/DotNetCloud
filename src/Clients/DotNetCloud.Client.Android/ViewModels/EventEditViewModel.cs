using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for creating or editing a calendar event.</summary>
[QueryProperty(nameof(EventIdString), "EventId")]
[QueryProperty(nameof(EditScopeString), "EditScope")]
[QueryProperty(nameof(OriginalStartUtcString), "OriginalStartUtc")]
public sealed partial class EventEditViewModel : ObservableObject
{
    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<EventEditViewModel> _logger;

    // ── Navigation Params (received as strings, parsed to typed fields) ──

    /// <summary>Event ID as string from navigation query.</summary>
    [ObservableProperty]
    private string? _eventIdString;

    /// <summary>Edit scope as string from navigation query.</summary>
    [ObservableProperty]
    private string? _editScopeString;

    /// <summary>Original start UTC as string from navigation query (for recurrence exceptions).</summary>
    [ObservableProperty]
    private string? _originalStartUtcString;

    private Guid? _eventId;
    private EditScope _editScope = EditScope.AllEvents;
    private DateTime? _originalStartUtc;

    /// <summary>Initializes a new <see cref="EventEditViewModel"/>.</summary>
    public EventEditViewModel(
        ICalendarRestClient calendarApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<EventEditViewModel> logger)
    {
        _calendarApi = calendarApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // ── Navigation Param Handlers ──────────────────────────────────

    partial void OnEventIdStringChanged(string? value)
    {
        if (Guid.TryParse(value, out var id))
        {
            _eventId = id;
            IsEdit = true;
            LoadEventForEditCommand.Execute(null);
        }
    }

    partial void OnEditScopeStringChanged(string? value)
    {
        _editScope = value == nameof(EditScope.ThisOccurrence)
            ? EditScope.ThisOccurrence
            : EditScope.AllEvents;
        EditScopeLabel = _editScope == EditScope.ThisOccurrence
            ? "Editing this occurrence only"
            : "Editing all events";
    }

    partial void OnOriginalStartUtcStringChanged(string? value)
    {
        if (DateTime.TryParse(value, out var dt))
            _originalStartUtc = dt;
    }

    // ── Mode ───────────────────────────────────────────────────────

    /// <summary>Whether we're editing an existing event (vs. creating new).</summary>
    [ObservableProperty]
    private bool _isEdit;

    /// <summary>Whether the event being edited is a recurring event (has RRULE or is an exception).</summary>
    [ObservableProperty]
    private bool _isRecurringEvent;

    /// <summary>Label for the edit scope banner (e.g., "Editing this occurrence only").</summary>
    [ObservableProperty]
    private string? _editScopeLabel;

    /// <summary>Page title.</summary>
    public string EditTitle => IsEdit ? "Edit Event" : "New Event";

    // ── Form Fields ────────────────────────────────────────────────

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _startTime = new(9, 0, 0);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _endTime = new(10, 0, 0);

    [ObservableProperty]
    private bool _isAllDay;

    [ObservableProperty]
    private Guid _selectedCalendarId;

    /// <summary>Selected calendar object for the Picker binding. Sets <see cref="SelectedCalendarId"/>.</summary>
    public CalendarDto? SelectedCalendarItem
    {
        get => Calendars.FirstOrDefault(c => c.Id == SelectedCalendarId);
        set
        {
            if (value is not null)
                SelectedCalendarId = value.Id;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private string? _url;

    /// <summary>Available calendars for the picker.</summary>
    public ObservableCollection<CalendarDto> Calendars { get; } = [];

    // ── Reminder Editor ───────────────────────────────────────────

    /// <summary>Minutes before the event to trigger a reminder. 0 means no reminder.
    /// These correspond to the <see cref="EventReminderDto.MinutesBefore"/> value.</summary>
    [ObservableProperty]
    private int _reminderMinutesBefore;

    /// <summary>Index into <see cref="ReminderOptions"/> for the Picker.</summary>
    [ObservableProperty]
    private int _reminderSelectedIndex;

    partial void OnReminderMinutesBeforeChanged(int value)
    {
        // Sync the picker index to match the minutes value
        for (var i = 0; i < ReminderOptions.Length; i++)
        {
            if (ReminderOptions[i].Key == value)
            {
                ReminderSelectedIndex = i;
                return;
            }
        }
        ReminderSelectedIndex = 0; // "None"
    }

    partial void OnReminderSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < ReminderOptions.Length)
            ReminderMinutesBefore = ReminderOptions[value].Key;
    }

    /// <summary>Available reminder presets for the picker.</summary>
    public static KeyValuePair<int, string>[] ReminderOptions { get; } =
    [
        new(0, "None"),
        new(5, "5 minutes before"),
        new(10, "10 minutes before"),
        new(15, "15 minutes before"),
        new(30, "30 minutes before"),
        new(60, "1 hour before"),
        new(120, "2 hours before"),
        new(1440, "1 day before"),
    ];

    // ── Recurrence Editor ──────────────────────────────────────────

    /// <summary>Whether recurrence is enabled.</summary>
    [ObservableProperty]
    private bool _isRecurring;

    /// <summary>0=None, 1=Daily, 2=Weekly, 3=Monthly, 4=Yearly.</summary>
    [ObservableProperty]
    private int _recurrenceFrequency;

    /// <summary>Interval (every N days/weeks/etc).</summary>
    [ObservableProperty]
    private int _recurrenceInterval = 1;

    // Days of week (for weekly recurrence)
    [ObservableProperty] private bool _recurrenceSun;
    [ObservableProperty] private bool _recurrenceMon = true;
    [ObservableProperty] private bool _recurrenceTue = true;
    [ObservableProperty] private bool _recurrenceWed = true;
    [ObservableProperty] private bool _recurrenceThu = true;
    [ObservableProperty] private bool _recurrenceFri = true;
    [ObservableProperty] private bool _recurrenceSat;

    /// <summary>0=Never, 1=AfterCount, 2=OnDate.</summary>
    [ObservableProperty]
    private int _recurrenceEndType;

    /// <summary>Number of occurrences (when EndType=AfterCount).</summary>
    [ObservableProperty]
    private int _recurrenceEndCount = 10;

    /// <summary>End date (when EndType=OnDate).</summary>
    [ObservableProperty]
    private DateTime _recurrenceEndDate = DateTime.Today.AddMonths(3);

    /// <summary>Human-readable recurrence preview (e.g., "Every week on Mon, Wed, Fri").</summary>
    [ObservableProperty]
    private string? _recurrenceDescription;

    /// <summary>Labels for frequency picker.</summary>
    public string[] FrequencyOptions { get; } = ["None", "Daily", "Weekly", "Monthly", "Yearly"];

    /// <summary>Labels for end type picker.</summary>
    public string[] EndTypeOptions { get; } = ["Never", "After N", "On Date"];

    /// <summary>Whether the days-of-week grid is visible.</summary>
    public bool ShowDaysOfWeek => RecurrenceFrequency == 2;

    /// <summary>Whether the end count field is visible.</summary>
    public bool ShowEndCount => RecurrenceEndType == 1;

    /// <summary>Whether the end date field is visible.</summary>
    public bool ShowEndDate => RecurrenceEndType == 2;

    // ── Validation ─────────────────────────────────────────────────

    /// <summary>Error message for the title field.</summary>
    [ObservableProperty]
    private string? _titleError;

    /// <summary>Error message for date/time fields.</summary>
    [ObservableProperty]
    private string? _dateError;

    /// <summary>General form error message.</summary>
    [ObservableProperty]
    private string? _formError;

    /// <summary>Whether the form is currently saving.</summary>
    [ObservableProperty]
    private bool _isSaving;

    // ── Recurrence property change handlers ────────────────────────

    partial void OnRecurrenceFrequencyChanged(int value)
    {
        OnPropertyChanged(nameof(ShowDaysOfWeek));
        UpdateRecurrence();
    }

    partial void OnRecurrenceIntervalChanged(int value) => UpdateRecurrence();
    partial void OnRecurrenceSunChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceMonChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceTueChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceWedChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceThuChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceFriChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceSatChanged(bool value) => UpdateRecurrence();
    partial void OnRecurrenceEndTypeChanged(int value)
    {
        OnPropertyChanged(nameof(ShowEndCount));
        OnPropertyChanged(nameof(ShowEndDate));
        UpdateRecurrence();
    }
    partial void OnRecurrenceEndCountChanged(int value) => UpdateRecurrence();
    partial void OnRecurrenceEndDateChanged(DateTime value) => UpdateRecurrence();
    partial void OnIsRecurringChanged(bool value) => UpdateRecurrence();

    private void UpdateRecurrence()
    {
        if (!IsRecurring || RecurrenceFrequency == 0)
        {
            RecurrenceDescription = null;
            return;
        }

        var rrule = BuildRrule();
        RecurrenceDescription = DescribeRrule(rrule);
    }

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>Loads calendars for the picker. Also loads event data if editing.</summary>
    [RelayCommand]
    private async Task LoadCalendarsAsync(CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var calendars = await _calendarApi.ListCalendarsAsync(serverUrl, token, ct);

            Calendars.Clear();
            foreach (var cal in calendars)
            {
                Calendars.Add(cal);
            }

            // Auto-select default calendar or the first one
            if (SelectedCalendarId == Guid.Empty)
            {
                var defaultCal = calendars.FirstOrDefault(c => c.IsDefault) ?? calendars.FirstOrDefault();
                if (defaultCal is not null)
                    SelectedCalendarId = defaultCal.Id;
            }

            OnPropertyChanged(nameof(SelectedCalendarItem));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load calendars for event edit.");
        }
    }

    /// <summary>Loads event data for editing into the form fields.</summary>
    [RelayCommand]
    private async Task LoadEventForEditAsync(CancellationToken ct)
    {
        if (!_eventId.HasValue)
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var evt = await _calendarApi.GetEventAsync(serverUrl, token, _eventId.Value, ct);

            Title = evt.Title;
            Description = evt.Description;
            Location = evt.Location;
            StartDate = evt.StartUtc.Date;
            StartTime = evt.StartUtc.TimeOfDay;
            EndDate = evt.EndUtc.Date;
            EndTime = evt.EndUtc.TimeOfDay;
            IsAllDay = evt.IsAllDay;
            SelectedCalendarId = evt.CalendarId;
            Url = evt.Url;

            // Parse existing recurrence rule into editor fields
            if (!string.IsNullOrEmpty(evt.RecurrenceRule))
            {
                IsRecurring = true;
                IsRecurringEvent = true;
                ParseRruleIntoFields(evt.RecurrenceRule);
            }

            // Parse existing reminder into the editor
            if (evt.Reminders.Count > 0)
            {
                // Use the first Notification-type reminder as the preset value
                var firstReminder = evt.Reminders
                    .FirstOrDefault(r => r.Method == ReminderMethod.Notification);
                if (firstReminder is not null)
                    ReminderMinutesBefore = firstReminder.MinutesBefore;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load event {EventId} for editing.", _eventId);
            FormError = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Saves the event (creates new or updates existing).</summary>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        // Reset validation
        TitleError = null;
        DateError = null;
        FormError = null;

        // Validate
        if (string.IsNullOrWhiteSpace(Title))
        {
            TitleError = "Title is required.";
            return;
        }

        var startUtc = StartDate.Date + StartTime;
        var endUtc = EndDate.Date + EndTime;

        if (endUtc <= startUtc)
        {
            DateError = "End time must be after start time.";
            return;
        }

        if (SelectedCalendarId == Guid.Empty)
        {
            FormError = "Please select a calendar.";
            return;
        }

        IsSaving = true;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var recurrenceRule = IsRecurring && RecurrenceFrequency > 0 ? BuildRrule() : null;

            if (IsEdit && _eventId.HasValue)
            {
                if (IsRecurringEvent && _editScope == EditScope.ThisOccurrence)
                {
                    // Update the specific occurrence instance directly
                    var updateDto = new UpdateCalendarEventDto
                    {
                        Title = Title,
                        Description = Description,
                        Location = Location,
                        StartUtc = startUtc,
                        EndUtc = endUtc,
                        IsAllDay = IsAllDay,
                        Url = Url,
                        Reminders = BuildReminderDtos(),
                    };
                    await _calendarApi.UpdateEventAsync(serverUrl, token, _eventId.Value, updateDto, ct);
                }
                else
                {
                    // Update the master event
                    var updateDto = new UpdateCalendarEventDto
                    {
                        Title = Title,
                        Description = Description,
                        Location = Location,
                        StartUtc = startUtc,
                        EndUtc = endUtc,
                        IsAllDay = IsAllDay,
                        RecurrenceRule = recurrenceRule,
                        Url = Url,
                        Reminders = BuildReminderDtos(),
                    };
                    await _calendarApi.UpdateEventAsync(serverUrl, token, _eventId.Value, updateDto, ct);
                }
            }
            else
            {
                // Create new event
                var createDto = new CreateCalendarEventDto
                {
                    CalendarId = SelectedCalendarId,
                    Title = Title,
                    Description = Description,
                    Location = Location,
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    IsAllDay = IsAllDay,
                    RecurrenceRule = recurrenceRule,
                    Url = Url,
                    Reminders = BuildReminderDtos(),
                };
                await _calendarApi.CreateEventAsync(serverUrl, token, createDto, ct);
            }

            CalendarViewModel.NeedsRefresh = true;
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save event.");
            FormError = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>Deletes the current event (only available in edit mode).</summary>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken ct)
    {
        if (!_eventId.HasValue)
            return;

        try
        {
            if (IsRecurringEvent)
            {
                var action = await Shell.Current.DisplayActionSheetAsync(
                    "Delete Recurring Event", "Cancel", null,
                    "Delete This Occurrence", "Delete All Events");

                if (action is null || action == "Cancel")
                    return;

                var (serverUrl, token) = await GetCredentialsAsync(ct);
                await _calendarApi.DeleteEventAsync(serverUrl, token, _eventId.Value, ct);
            }
            else
            {
                var confirmed = await Shell.Current.DisplayAlertAsync(
                    "Delete Event", "Delete this event?", "Delete", "Cancel");
                if (!confirmed)
                    return;

                var (serverUrl, token) = await GetCredentialsAsync(ct);
                await _calendarApi.DeleteEventAsync(serverUrl, token, _eventId.Value, ct);
            }

            CalendarViewModel.NeedsRefresh = true;
            await Shell.Current.GoToAsync("../.."); // Go back to calendar
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete event.");
            await Shell.Current.DisplayAlertAsync("Error", ApiExceptionHelper.GetUserFriendlyMessage(ex), "OK");
        }
    }

    /// <summary>Cancels and navigates back without saving.</summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
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

    private string BuildRrule()
    {
        var freq = RecurrenceFrequency switch
        {
            1 => "DAILY",
            2 => "WEEKLY",
            3 => "MONTHLY",
            4 => "YEARLY",
            _ => "DAILY"
        };

        var parts = new List<string> { $"FREQ={freq}" };

        if (RecurrenceInterval > 1)
            parts.Add($"INTERVAL={RecurrenceInterval}");

        if (RecurrenceFrequency == 2)
        {
            var days = new List<string>();
            if (RecurrenceSun)
                days.Add("SU");
            if (RecurrenceMon)
                days.Add("MO");
            if (RecurrenceTue)
                days.Add("TU");
            if (RecurrenceWed)
                days.Add("WE");
            if (RecurrenceThu)
                days.Add("TH");
            if (RecurrenceFri)
                days.Add("FR");
            if (RecurrenceSat)
                days.Add("SA");
            if (days.Count > 0)
                parts.Add($"BYDAY={string.Join(",", days)}");
        }

        if (RecurrenceEndType == 1 && RecurrenceEndCount > 0)
            parts.Add($"COUNT={RecurrenceEndCount}");
        else if (RecurrenceEndType == 2)
            parts.Add($"UNTIL={RecurrenceEndDate:yyyyMMdd}T235959Z");

        return string.Join(";", parts);
    }

    private void ParseRruleIntoFields(string rrule)
    {
        if (string.IsNullOrEmpty(rrule))
            return;

        var parts = rrule.Split(';').Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");

        // Frequency
        RecurrenceFrequency = parts.GetValueOrDefault("FREQ", "") switch
        {
            "DAILY" => 1,
            "WEEKLY" => 2,
            "MONTHLY" => 3,
            "YEARLY" => 4,
            _ => 0
        };

        // Interval
        if (parts.TryGetValue("INTERVAL", out var interval) && int.TryParse(interval, out var iv))
            RecurrenceInterval = iv;

        // Days of week
        if (parts.TryGetValue("BYDAY", out var byDay))
        {
            RecurrenceSun = byDay.Contains("SU");
            RecurrenceMon = byDay.Contains("MO");
            RecurrenceTue = byDay.Contains("TU");
            RecurrenceWed = byDay.Contains("WE");
            RecurrenceThu = byDay.Contains("TH");
            RecurrenceFri = byDay.Contains("FR");
            RecurrenceSat = byDay.Contains("SA");
        }

        // End condition
        if (parts.TryGetValue("COUNT", out var count) && int.TryParse(count, out var cnt))
        {
            RecurrenceEndType = 1;
            RecurrenceEndCount = cnt;
        }
        else if (parts.TryGetValue("UNTIL", out var until) && DateTime.TryParse(until, out var endDate))
        {
            RecurrenceEndType = 2;
            RecurrenceEndDate = endDate;
        }
        else
        {
            RecurrenceEndType = 0;
        }
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

    // ── Reminder Helpers ───────────────────────────────────────────

    private IReadOnlyList<EventReminderDto> BuildReminderDtos()
    {
        if (ReminderMinutesBefore <= 0)
            return [];

        return
        [
            new EventReminderDto
            {
                MinutesBefore = ReminderMinutesBefore,
                Method = ReminderMethod.Notification,
            }
        ];
    }
}
