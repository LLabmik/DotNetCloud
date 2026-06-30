using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>Which calendar view is currently displayed.</summary>
public enum CalendarViewType { Month, Week, Day }

/// <summary>Wrapper for a calendar with visibility toggle.</summary>
public sealed partial class CalendarItem : ObservableObject
{
    /// <summary>Calendar unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Calendar display name.</summary>
    public required string Name { get; init; }

    /// <summary>Calendar hex color (e.g., "#3B82F6").</summary>
    public required string Color { get; init; }

    /// <summary>Whether this is the user's default calendar.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Whether events from this calendar are shown in the UI. Default true.</summary>
    [ObservableProperty]
    private bool _isVisible = true;
}

/// <summary>Represents a single day cell in a month/week calendar grid.</summary>
public sealed class CalendarDayItem
{
    /// <summary>Date of this cell.</summary>
    public required DateTime Date { get; init; }

    /// <summary>Whether this day falls within the current month (vs. padding days).</summary>
    public bool IsCurrentMonth { get; init; }

    /// <summary>Whether this day is today.</summary>
    public bool IsToday { get; init; }

    /// <summary>Day of month number (1-31).</summary>
    public int DayNumber => Date.Day;

    /// <summary>Events occurring on this day.</summary>
    public IReadOnlyList<CalendarEventDto> Events { get; init; } = [];
}

/// <summary>Main ViewModel for the Calendar tab.</summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<CalendarViewModel> _logger;

    /// <summary>Initializes a new <see cref="CalendarViewModel"/>.</summary>
    public CalendarViewModel(
        ICalendarRestClient calendarApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<CalendarViewModel> logger)
    {
        _calendarApi = calendarApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // ── View State ─────────────────────────────────────────────────

    /// <summary>Current view mode.</summary>
    [ObservableProperty]
    private CalendarViewType _currentView = CalendarViewType.Month;

    /// <summary>Reference date for the current view (the month/week/day being shown).</summary>
    [ObservableProperty]
    private DateTime _currentDate = DateTime.Today;

    /// <summary>Whether data is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Error message to display, or null.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>The currently selected event for detail navigation.</summary>
    [ObservableProperty]
    private CalendarEventDto? _selectedEvent;

    /// <summary>Whether the page is currently visible. Prevents background load errors from showing after navigating away.</summary>
    internal bool IsActive { get; set; }

    // ── Data Collections ───────────────────────────────────────────

    /// <summary>User's calendars with visibility toggle.</summary>
    public ObservableCollection<CalendarItem> Calendars { get; } = [];

    /// <summary>All events loaded for the current view range.</summary>
    public ObservableCollection<CalendarEventDto> Events { get; } = [];

    /// <summary>Month grid cells (42 cells: 6 weeks × 7 days).</summary>
    public ObservableCollection<CalendarDayItem> MonthDays { get; } = [];

    /// <summary>Week view cells (7 days).</summary>
    public ObservableCollection<CalendarDayItem> WeekDays { get; } = [];

    // ── Computed Labels ────────────────────────────────────────────

    /// <summary>Date label for the navigation bar.</summary>
    public string DateLabel => CurrentView switch
    {
        CalendarViewType.Month => CurrentDate.ToString("MMMM yyyy"),
        CalendarViewType.Week => GetWeekLabel(),
        CalendarViewType.Day => CurrentDate.ToString("dddd, MMMM d, yyyy"),
        _ => ""
    };

    /// <summary>Raised when the date label changes.</summary>
    public event Action? DateLabelChanged;

    partial void OnCurrentDateChanged(DateTime value) => RaiseDateLabelChanged();
    partial void OnCurrentViewChanged(CalendarViewType value) => RaiseDateLabelChanged();

    private void RaiseDateLabelChanged()
    {
        OnPropertyChanged(nameof(DateLabel));
        DateLabelChanged?.Invoke();
    }

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>Loads calendars from the server, then loads events.</summary>
    [RelayCommand]
    private async Task LoadCalendarsAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var calendars = await _calendarApi.ListCalendarsAsync(serverUrl, token, ct);

            Calendars.Clear();
            foreach (var cal in calendars)
            {
                Calendars.Add(new CalendarItem
                {
                    Id = cal.Id,
                    Name = cal.Name,
                    Color = cal.Color ?? "#3B82F6",
                    IsDefault = cal.IsDefault,
                    IsVisible = true
                });
            }

            await LoadEventsAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load calendars.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Loads events for the visible date range from all visible calendars.</summary>
    [RelayCommand]
    private async Task LoadEventsAsync(CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var visibleCalendars = Calendars.Where(c => c.IsVisible).ToList();
            if (visibleCalendars.Count == 0)
            {
                Events.Clear();
                RebuildGrids([]);
                return;
            }

            var (from, to) = GetViewDateRange();
            var allEvents = new List<CalendarEventDto>();

            foreach (var cal in visibleCalendars)
            {
                var events = await _calendarApi.ListEventsAsync(
                    serverUrl, token, cal.Id, from, to, ct: ct);
                allEvents.AddRange(events);
            }

            Events.Clear();
            foreach (var evt in allEvents)
                Events.Add(evt);

            RebuildGrids(allEvents);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load events.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Navigates to the previous month/week/day.</summary>
    [RelayCommand]
    private void PreviousPeriod()
    {
        CurrentDate = CurrentView switch
        {
            CalendarViewType.Month => CurrentDate.AddMonths(-1),
            CalendarViewType.Week => CurrentDate.AddDays(-7),
            CalendarViewType.Day => CurrentDate.AddDays(-1),
            _ => CurrentDate
        };
        LoadEventsCommand.Execute(null);
    }

    /// <summary>Navigates to the next month/week/day.</summary>
    [RelayCommand]
    private void NextPeriod()
    {
        CurrentDate = CurrentView switch
        {
            CalendarViewType.Month => CurrentDate.AddMonths(1),
            CalendarViewType.Week => CurrentDate.AddDays(7),
            CalendarViewType.Day => CurrentDate.AddDays(1),
            _ => CurrentDate
        };
        LoadEventsCommand.Execute(null);
    }

    /// <summary>Jumps to today's date.</summary>
    [RelayCommand]
    private void Today()
    {
        CurrentDate = DateTime.Today;
        LoadEventsCommand.Execute(null);
    }

    /// <summary>Switches between Month/Week/Day view.</summary>
    [RelayCommand]
    private void SetView(string viewName)
    {
        CurrentView = viewName switch
        {
            "Week" => CalendarViewType.Week,
            "Day" => CalendarViewType.Day,
            _ => CalendarViewType.Month
        };
        LoadEventsCommand.Execute(null);
    }

    /// <summary>Toggles a calendar's visibility and reloads events.</summary>
    [RelayCommand]
    private void ToggleCalendar(CalendarItem calendar)
    {
        calendar.IsVisible = !calendar.IsVisible;
        OnPropertyChanged(nameof(Calendars));
        LoadEventsCommand.Execute(null);
    }

    /// <summary>Navigates to the event detail page for the selected event.</summary>
    [RelayCommand]
    private async Task SelectEventAsync(CalendarEventDto evt)
    {
        try
        {
            SelectedEvent = evt;
            await Shell.Current.GoToAsync("EventDetail", new Dictionary<string, object>
            {
                ["EventId"] = evt.Id.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to event detail for {EventId}.", evt.Id);
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Navigates to the create-event page.</summary>
    [RelayCommand]
    private async Task CreateEventAsync()
    {
        await Shell.Current.GoToAsync("EventEdit");
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

    private (DateTime From, DateTime To) GetViewDateRange()
    {
        return CurrentView switch
        {
            CalendarViewType.Month => GetMonthDateRange(CurrentDate),
            CalendarViewType.Week => GetWeekDateRange(CurrentDate),
            CalendarViewType.Day => (CurrentDate.Date, CurrentDate.Date.AddDays(1)),
            _ => (CurrentDate.Date, CurrentDate.Date.AddDays(1))
        };
    }

    private static (DateTime From, DateTime To) GetMonthDateRange(DateTime date)
    {
        var firstOfMonth = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek); // Sunday before
        var end = start.AddDays(42); // 6 weeks forward
        return (start, end);
    }

    private static (DateTime From, DateTime To) GetWeekDateRange(DateTime date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
        var sunday = date.Date.AddDays(-diff);
        return (sunday, sunday.AddDays(7));
    }

    private void RebuildGrids(IReadOnlyList<CalendarEventDto> allEvents)
    {
        MonthDays.Clear();
        WeekDays.Clear();

        var (from, to) = GetViewDateRange();

        if (CurrentView == CalendarViewType.Month || CurrentView == CalendarViewType.Week)
        {
            var isWeek = CurrentView == CalendarViewType.Week;
            var dayCount = isWeek ? 7 : 42;
            var startDate = isWeek ? from : from;
            var today = DateTime.Today;

            for (var i = 0; i < dayCount; i++)
            {
                var day = startDate.AddDays(i);
                var dayEvents = allEvents
                    .Where(e => e.StartUtc.Date <= day.Date && e.EndUtc.Date >= day.Date)
                    .OrderBy(e => e.StartUtc)
                    .ToList();

                var item = new CalendarDayItem
                {
                    Date = day,
                    IsCurrentMonth = !isWeek && day.Month == CurrentDate.Month,
                    IsToday = day.Date == today,
                    Events = dayEvents
                };

                if (isWeek)
                    WeekDays.Add(item);
                else
                    MonthDays.Add(item);
            }
        }
    }

    private string GetWeekLabel()
    {
        var diff = (7 + (int)CurrentDate.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
        var sunday = CurrentDate.AddDays(-diff);
        var saturday = sunday.AddDays(6);
        return $"{sunday:MMM d} – {saturday:MMM d, yyyy}";
    }
}
