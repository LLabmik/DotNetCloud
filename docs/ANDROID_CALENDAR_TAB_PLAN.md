# Android Calendar Tab — Implementation Plan

> **Branch:** `feature/android-calendar-tab`
> **Target:** Add a full-featured Calendar tab to the DotNetCloud Android MAUI app.
> **Assumption:** Calendar is a required server module (always installed). No module-probing logic needed.

---

## Architecture Overview

The Calendar tab follows the exact same MVVM + REST pattern as the existing Files, Music, and Chat tabs:

```
CalendarPage.xaml(.cs)          ← MAUI ContentPage (the tab)
    ↓ binds to
CalendarViewModel.cs            ← CommunityToolkit.Mvvm ObservableObject
    ↓ calls
ICalendarRestClient             ← interface (per-call credential pattern)
    ↓ implemented by
HttpCalendarRestClient.cs       ← HttpClient + envelope unwrapping
    ↓ talks to
Server: /api/v1/calendars/*     ← existing REST API (fully implemented server-side)
```

**Shared DTOs** from `DotNetCloud.Core/DTOs/CalendarDtos.cs` are reused directly — no local DTO duplication.

### Navigation

```
TabBar
  CalendarPage (tab) ──GoToAsync──→ EventDetailPage
                      ──GoToAsync──→ EventEditPage (create new)
  EventDetailPage     ──GoToAsync──→ EventEditPage (edit existing)
```

---

## Files to Create (12 new files)

| # | File | Purpose |
|---|------|---------|
| 1 | `src/Clients/DotNetCloud.Client.Android/Calendar/ICalendarRestClient.cs` | REST interface |
| 2 | `src/Clients/DotNetCloud.Client.Android/Calendar/HttpCalendarRestClient.cs` | HTTP implementation |
| 3 | `src/Clients/DotNetCloud.Client.Android/ViewModels/CalendarViewModel.cs` | Main tab ViewModel |
| 4 | `src/Clients/DotNetCloud.Client.Android/ViewModels/EventDetailViewModel.cs` | Event detail ViewModel |
| 5 | `src/Clients/DotNetCloud.Client.Android/ViewModels/EventEditViewModel.cs` | Create/edit ViewModel |
| 6 | `src/Clients/DotNetCloud.Client.Android/Views/CalendarPage.xaml` | Main tab UI |
| 7 | `src/Clients/DotNetCloud.Client.Android/Views/CalendarPage.xaml.cs` | Main tab code-behind |
| 8 | `src/Clients/DotNetCloud.Client.Android/Views/EventDetailPage.xaml` | Event detail UI |
| 9 | `src/Clients/DotNetCloud.Client.Android/Views/EventDetailPage.xaml.cs` | Event detail code-behind |
| 10 | `src/Clients/DotNetCloud.Client.Android/Views/EventEditPage.xaml` | Create/edit form UI |
| 11 | `src/Clients/DotNetCloud.Client.Android/Views/EventEditPage.xaml.cs` | Create/edit code-behind |
| 12 | `src/Clients/DotNetCloud.Client.Android/Resources/Images/calendar_icon.svg` | Tab bar icon |

## Files to Modify (3 existing files)

| # | File | Change |
|---|------|--------|
| 13 | `src/Clients/DotNetCloud.Client.Android/AppShell.xaml` | Add Calendar `<ShellContent>` inside `<TabBar>` |
| 14 | `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs` | Register `EventDetail` and `EventEdit` routes |
| 15 | `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs` | DI registrations for REST client, ViewModels, Pages |

---

## Step-by-Step Implementation

### Step 1: Calendar Icon (file #12)

**File:** `src/Clients/DotNetCloud.Client.Android/Resources/Images/calendar_icon.svg`

Create an SVG icon matching the existing tab icon style. The existing icons (`chat_icon.svg`, `files_icon.svg`, `music_icon.svg`, `settings_icon.svg`) use a consistent visual language. Use a simple calendar outline icon.

**Design spec:**
- `viewBox="0 0 24 24"`, fill none, stroke currentColor
- Typically: a rectangle with a top header bar and two small "bumps" for month/day indicators
- The icon is referenced in XAML as `calendar_icon.png` (MAUI converts SVGs to PNGs at build time)

---

### Step 2: REST Client Interface (file #1)

**File:** `src/Clients/DotNetCloud.Client.Android/Calendar/ICalendarRestClient.cs`

**Pattern to follow:** `src/Clients/DotNetCloud.Client.Android/Files/IFileRestClient.cs`

**Template:** Every method takes `(string serverBaseUrl, string accessToken, ..., CancellationToken ct = default)`. This is the "per-call credential" pattern — the client doesn't hold credentials, the caller passes them every time.

```csharp
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Calendar;

/// <summary>REST client for the Calendar module API.</summary>
public interface ICalendarRestClient
{
    // ── Calendars ──────────────────────────────────────────────────
    Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    Task<CalendarDto> GetCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, CancellationToken ct = default);

    Task<CalendarDto> CreateCalendarAsync(
        string serverBaseUrl, string accessToken,
        CreateCalendarDto dto, CancellationToken ct = default);

    Task<CalendarDto> UpdateCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, UpdateCalendarDto dto, CancellationToken ct = default);

    Task DeleteCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, CancellationToken ct = default);

    // ── Events ─────────────────────────────────────────────────────
    Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, DateTime? from = null, DateTime? to = null,
        int skip = 0, int take = 200, CancellationToken ct = default);

    Task<CalendarEventDto> GetEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, CancellationToken ct = default);

    Task<CalendarEventDto> CreateEventAsync(
        string serverBaseUrl, string accessToken,
        CreateCalendarEventDto dto, CancellationToken ct = default);

    Task<CalendarEventDto> UpdateEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, UpdateCalendarEventDto dto, CancellationToken ct = default);

    Task DeleteEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, CancellationToken ct = default);

    Task<CalendarEventDto> RsvpAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, EventRsvpDto dto, CancellationToken ct = default);

    // ── Search ─────────────────────────────────────────────────────
    Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(
        string serverBaseUrl, string accessToken,
        string query, DateTime? from = null, DateTime? to = null,
        int skip = 0, int take = 200, CancellationToken ct = default);
}
```

---

### Step 3: REST Client Implementation (file #2)

**File:** `src/Clients/DotNetCloud.Client.Android/Calendar/HttpCalendarRestClient.cs`

**Pattern to follow EXACTLY:** `src/Clients/DotNetCloud.Client.Android/Files/HttpFileRestClient.cs`

Copy the exact helper methods from `HttpFileRestClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Calendar;

internal sealed class HttpCalendarRestClient : ICalendarRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public HttpCalendarRestClient(HttpClient http)
    {
        _http = http;
    }

    // ── Helpers (COPY FROM HttpFileRestClient.cs) ──────────────────

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string Url(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T?> ReadEnvelopeDataAsync<T>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return dataProp.Deserialize<T>(JsonOpts);
        }

        return doc.RootElement.Deserialize<T>(JsonOpts);
    }
```

**API Endpoint Mapping:**

| Method | HTTP | URL Pattern |
|--------|------|-------------|
| `ListCalendarsAsync` | GET | `/api/v1/calendars` |
| `GetCalendarAsync` | GET | `/api/v1/calendars/{calendarId}` |
| `CreateCalendarAsync` | POST | `/api/v1/calendars` (body: `CreateCalendarDto`) |
| `UpdateCalendarAsync` | PUT | `/api/v1/calendars/{calendarId}` (body: `UpdateCalendarDto`) |
| `DeleteCalendarAsync` | DELETE | `/api/v1/calendars/{calendarId}` |
| `ListEventsAsync` | GET | `/api/v1/calendars/{calendarId}/events?from=&to=&skip=&take=` |
| `GetEventAsync` | GET | `/api/v1/calendars/events/{eventId}` |
| `CreateEventAsync` | POST | `/api/v1/calendars/events` (body: `CreateCalendarEventDto`) |
| `UpdateEventAsync` | PUT | `/api/v1/calendars/events/{eventId}` (body: `UpdateCalendarEventDto`) |
| `DeleteEventAsync` | DELETE | `/api/v1/calendars/events/{eventId}` |
| `RsvpAsync` | POST | `/api/v1/calendars/events/{eventId}/rsvp` (body: `EventRsvpDto`) |
| `SearchEventsAsync` | GET | `/api/v1/calendars/events/search?q=&from=&to=&skip=&take=` |

**Implementation pattern (use this for every method):**

```csharp
public async Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(
    string serverBaseUrl, string accessToken,
    Guid calendarId, DateTime? from = null, DateTime? to = null,
    int skip = 0, int take = 200, CancellationToken ct = default)
{
    SetAuth(accessToken);
    var query = $"?skip={skip}&take={take}";
    if (from.HasValue) query += $"&from={from.Value:O}";
    if (to.HasValue) query += $"&to={to.Value:O}";
    var result = await GetEnvelopeDataAsync<List<CalendarEventDto>>(
        $"{Url(serverBaseUrl)}/api/v1/calendars/{calendarId}/events{query}", ct)
        .ConfigureAwait(false);
    return result ?? [];
}
```

For POST/PUT methods, use `PostAsJsonAsync` / `PutAsJsonAsync` with `ReadEnvelopeDataAsync`:

```csharp
public async Task<CalendarEventDto> CreateEventAsync(
    string serverBaseUrl, string accessToken,
    CreateCalendarEventDto dto, CancellationToken ct = default)
{
    SetAuth(accessToken);
    using var response = await _http.PostAsJsonAsync(
        $"{Url(serverBaseUrl)}/api/v1/calendars/events", dto, ct)
        .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    return await ReadEnvelopeDataAsync<CalendarEventDto>(response, ct).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Server returned null for created event.");
}
```

For DELETE methods, just ensure success:

```csharp
public async Task DeleteEventAsync(
    string serverBaseUrl, string accessToken,
    Guid eventId, CancellationToken ct = default)
{
    SetAuth(accessToken);
    using var response = await _http.DeleteAsync(
        $"{Url(serverBaseUrl)}/api/v1/calendars/events/{eventId}", ct)
        .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
}
```

**⚠️ IMPORTANT:** Use `using` for `HttpResponseMessage` on POST/PUT/DELETE. For GET with `GetEnvelopeDataAsync`, the `using` is inside the helper.

---

### Step 4: CalendarViewModel (file #3)

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/CalendarViewModel.cs`

**Pattern to follow:** `src/Clients/DotNetCloud.Client.Android/ViewModels/FileBrowserViewModel.cs`

**Key aspects:**
- Inherits `ObservableObject` (CommunityToolkit.Mvvm)
- Is `sealed partial class` (source generators)
- Uses `[ObservableProperty]` for bindable properties
- Uses `[RelayCommand]` for commands
- Injects `ICalendarRestClient`, `IServerConnectionStore`, `ISecureTokenStore`, `ILogger<CalendarViewModel>`

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

public enum CalendarViewType { Month, Week, Day }
public enum EditScope { ThisOccurrence, AllEvents }

/// <summary>Wrapper for calendar list items with visibility toggle.</summary>
public sealed partial class CalendarItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public bool IsDefault { get; init; }

    [ObservableProperty]
    private bool _isVisible = true;
}

/// <summary>Represents a single day cell in the month/week grid.</summary>
public sealed class CalendarDayItem
{
    public DateTime Date { get; init; }
    public bool IsCurrentMonth { get; init; }
    public bool IsToday { get; init; }
    public int DayNumber => Date.Day;
    public IReadOnlyList<CalendarEventDto> Events { get; init; } = [];
}

public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<CalendarViewModel> _logger;

    // ── View State ─────────────────────────────────────────────────
    [ObservableProperty] private CalendarViewType _currentView = CalendarViewType.Month;
    [ObservableProperty] private DateTime _currentDate = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private CalendarEventDto? _selectedEvent;

    // ── Data ───────────────────────────────────────────────────────
    public ObservableCollection<CalendarItem> Calendars { get; } = [];
    public ObservableCollection<CalendarEventDto> Events { get; } = [];
    public ObservableCollection<CalendarDayItem> MonthDays { get; } = [];
    public ObservableCollection<CalendarDayItem> WeekDays { get; } = [];
    // DayHours can be added as a collection of time-slot items for the Day view

    // ── Date Labels (computed) ─────────────────────────────────────
    public string DateLabel => CurrentView switch
    {
        CalendarViewType.Month => CurrentDate.ToString("MMMM yyyy"),
        CalendarViewType.Week => GetWeekLabel(),
        CalendarViewType.Day => CurrentDate.ToString("dddd, MMMM d, yyyy"),
        _ => ""
    };

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

    // ── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadCalendarsAsync(CancellationToken ct)
    {
        // 1. Get server URL + token from _serverStore / _tokenStore
        // 2. Call _calendarApi.ListCalendarsAsync(serverUrl, token, ct)
        // 3. Populate Calendars collection with CalendarItem wrappers
        // 4. After loading calendars, call LoadEventsCommand
    }

    [RelayCommand]
    private async Task LoadEventsAsync(CancellationToken ct)
    {
        // 1. Determine date range from CurrentView + CurrentDate:
        //    - Month: first day of month → last day of month (± buffer days for grid)
        //    - Week: Sunday of current week → Saturday of current week
        //    - Day: midnight → midnight next day
        // 2. Get visible calendar IDs from Calendars.Where(c => c.IsVisible)
        // 3. For each visible calendar, call ListEventsAsync with the date range
        //    (Consider calling SearchEventsAsync with blank query to get all visible
        //     calendar events in one call if the API supports it, or loop calendars)
        // 4. Merge all events, populate Events collection
        // 5. Compute MonthDays / WeekDays using BuildMonthDays / BuildWeekDays
    }

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

    [RelayCommand]
    private void Today()
    {
        CurrentDate = DateTime.Today;
        LoadEventsCommand.Execute(null);
    }

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

    [RelayCommand]
    private void ToggleCalendar(CalendarItem calendar)
    {
        calendar.IsVisible = !calendar.IsVisible;
        LoadEventsCommand.Execute(null);
    }

    [RelayCommand]
    private async Task SelectEventAsync(CalendarEventDto evt)
    {
        SelectedEvent = evt;
        await Shell.Current.GoToAsync("EventDetail", new Dictionary<string, object>
        {
            ["EventId"] = evt.Id
        });
    }

    [RelayCommand]
    private async Task CreateEventAsync()
    {
        await Shell.Current.GoToAsync("EventEdit");
    }

    // ── Grid Computation Helpers ───────────────────────────────────

    // BuildMonthDays: Creates 42 CalendarDayItem cells (6 weeks × 7 days)
    // covering the visible month. Days outside the current month get IsCurrentMonth = false.
    // Events are distributed into the corresponding day cells.
    //
    // Algorithm:
    //   1. Get first day of the month: new DateTime(year, month, 1)
    //   2. Find the Sunday on or before that day: firstDay.AddDays(-(int)firstDay.DayOfWeek)
    //   3. Generate 42 days starting from that Sunday
    //   4. For each day, filter events where event.StartUtc.Date <= day && event.EndUtc.Date >= day
    //      (convert event UTC to local timezone for comparison)
    //   5. Mark IsToday = (day.Date == DateTime.Today)

    // BuildWeekDays: Creates 7 CalendarDayItem cells for Sun-Sat of the current week.

    // BuildDayHours: Creates time-slot items for the Day view (24 hours, events positioned by time).

    // ── Helpers ────────────────────────────────────────────────────

    private string GetWeekLabel()
    {
        // Find Sunday of the current week
        var diff = (7 + (int)CurrentDate.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
        var sunday = CurrentDate.AddDays(-diff);
        var saturday = sunday.AddDays(6);
        return $"{sunday:MMM d} – {saturday:MMM d, yyyy}";
    }
}
```

---

### Step 5: EventDetailViewModel (file #4)

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/EventDetailViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.ViewModels;

[QueryProperty(nameof(EventId), "EventId")]
public sealed partial class EventDetailViewModel : ObservableObject
{
    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;

    [ObservableProperty] private Guid _eventId;
    [ObservableProperty] private CalendarEventDto? _event;
    [ObservableProperty] private CalendarDto? _calendar;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isRecurring;
    [ObservableProperty] private string? _recurrenceDescription;

    public EventDetailViewModel(
        ICalendarRestClient calendarApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore)
    {
        _calendarApi = calendarApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
    }

    // Called when EventId is set via navigation query parameter
    partial void OnEventIdChanged(Guid value) => LoadEventCommand.Execute(null);

    [RelayCommand]
    private async Task LoadEventAsync(CancellationToken ct)
    {
        // 1. Get server URL + token
        // 2. Call _calendarApi.GetEventAsync(serverUrl, token, EventId, ct)
        // 3. Set Event = result
        // 4. If Event.CalendarId has value, load calendar for name/color
        // 5. IsRecurring = Event.RecurrenceRule is not null || Event.RecurringEventId is not null
        // 6. RecurrenceDescription = human-readable (parse RRULE or show "Recurring event")
    }

    [RelayCommand]
    private async Task RsvpAsync(string status)
    {
        // status is string: "Accepted", "Declined", "Tentative"
        // Parse to Enum.Parse<AttendeeStatus>(status)
        // Call _calendarApi.RsvpAsync(...)
        // Reload event
    }

    [RelayCommand]
    private async Task EditEventAsync()
    {
        if (IsRecurring)
        {
            // Show action sheet: "Edit This Occurrence" / "Edit All Events" / "Cancel"
            var result = await Shell.Current.DisplayActionSheet(
                "Edit Recurring Event", "Cancel", null,
                "Edit This Occurrence", "Edit All Events");
            if (result == "Cancel" || result is null) return;

            var scope = result == "Edit This Occurrence"
                ? nameof(EditScope.ThisOccurrence) : nameof(EditScope.AllEvents);

            await Shell.Current.GoToAsync("EventEdit", new Dictionary<string, object>
            {
                ["EventId"] = EventId,
                ["EditScope"] = scope,
                ["OriginalStartUtc"] = Event?.OriginalStartUtc
            });
        }
        else
        {
            await Shell.Current.GoToAsync("EventEdit", new Dictionary<string, object>
            {
                ["EventId"] = EventId
            });
        }
    }

    [RelayCommand]
    private async Task DeleteEventAsync()
    {
        if (IsRecurring)
        {
            // Same action sheet pattern: "Delete This Occurrence" / "Delete All Events" / "Cancel"
            // Call appropriate API and navigate back
        }
        else
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Delete Event", "Are you sure you want to delete this event?", "Delete", "Cancel");
            if (!confirm) return;

            // Call _calendarApi.DeleteEventAsync(...)
            await Shell.Current.GoToAsync("..");
        }
    }
}
```

---

### Step 6: EventEditViewModel (file #5)

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/EventEditViewModel.cs`

This is the most complex ViewModel. It handles both create and edit modes, with full recurrence rule building.

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.ViewModels;

[QueryProperty(nameof(EventId), "EventId")]
[QueryProperty(nameof(EditScopeString), "EditScope")]
[QueryProperty(nameof(OriginalStartUtcString), "OriginalStartUtc")]
public sealed partial class EventEditViewModel : ObservableObject
{
    private readonly ICalendarRestClient _calendarApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;

    // ── Navigation params ──────────────────────────────────────────
    [ObservableProperty] private string? _eventIdString;
    [ObservableProperty] private string? _editScopeString;
    [ObservableProperty] private string? _originalStartUtcString;

    private Guid? _eventId;
    private EditScope _editScope = EditScope.AllEvents;
    private DateTime? _originalStartUtc;

    // ── Mode ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _isEdit;       // true when editing existing event
    [ObservableProperty] private bool _isRecurringEvent; // true when editing a recurring event
    [ObservableProperty] private string? _editScopeLabel;

    // ── Form Fields ────────────────────────────────────────────────
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _location;
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _startTime = TimeSpan.FromHours(9);
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _endTime = TimeSpan.FromHours(10);
    [ObservableProperty] private bool _isAllDay;
    [ObservableProperty] private Guid _selectedCalendarId;
    [ObservableProperty] private string? _url;
    public ObservableCollection<CalendarDto> Calendars { get; } = [];

    // ── Recurrence Fields ──────────────────────────────────────────
    [ObservableProperty] private bool _isRecurring;           // recurrence ON/OFF
    [ObservableProperty] private int _recurrenceFrequency;    // 0=None, 1=Daily, 2=Weekly, 3=Monthly, 4=Yearly
    [ObservableProperty] private int _recurrenceInterval = 1; // every N days/weeks/etc
    [ObservableProperty] private bool _recurrenceDayMon = true;
    [ObservableProperty] private bool _recurrenceDayTue = true;
    [ObservableProperty] private bool _recurrenceDayWed = true;
    [ObservableProperty] private bool _recurrenceDayThu = true;
    [ObservableProperty] private bool _recurrenceDayFri = true;
    [ObservableProperty] private bool _recurrenceDaySat;
    [ObservableProperty] private bool _recurrenceDaySun;
    [ObservableProperty] private int _recurrenceEndType;      // 0=Never, 1=AfterCount, 2=OnDate
    [ObservableProperty] private int _recurrenceEndCount = 10;
    [ObservableProperty] private DateTime _recurrenceEndDate = DateTime.Today.AddMonths(3);
    [ObservableProperty] private string? _recurrenceDescription;

    public string[] FrequencyOptions { get; } = ["None", "Daily", "Weekly", "Monthly", "Yearly"];
    public string[] EndTypeOptions { get; } = ["Never", "After", "On Date"];

    public EventEditViewModel(
        ICalendarRestClient calendarApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore)
    {
        _calendarApi = calendarApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
    }

    // Called when navigation params arrive
    partial void OnEventIdStringChanged(string? value)
    {
        if (Guid.TryParse(value, out var id))
        {
            _eventId = id;
            IsEdit = true;
        }
    }

    partial void OnEditScopeStringChanged(string? value)
    {
        _editScope = value == nameof(EditScope.ThisOccurrence)
            ? EditScope.ThisOccurrence : EditScope.AllEvents;
        EditScopeLabel = _editScope == EditScope.ThisOccurrence
            ? "Editing this occurrence only" : "Editing all events";
    }

    partial void OnOriginalStartUtcStringChanged(string? value)
    {
        if (DateTime.TryParse(value, out var dt))
            _originalStartUtc = dt;
    }

    // When recurrence fields change, recompute RRULE + description
    partial void OnRecurrenceFrequencyChanged(int value) => UpdateRecurrence();
    partial void OnRecurrenceIntervalChanged(int value) => UpdateRecurrence();
    partial void OnRecurrenceDayMonChanged(bool value) => UpdateRecurrence();
    // ... same for all day bools, end type, end count, end date

    private void UpdateRecurrence()
    {
        if (RecurrenceFrequency == 0) // None
        {
            IsRecurring = false;
            RecurrenceDescription = null;
            return;
        }
        IsRecurring = true;
        var rrule = BuildRrule();
        RecurrenceDescription = DescribeRrule(rrule);
    }

    private string BuildRrule()
    {
        // Build RFC 5545 RRULE string from the recurrence fields.
        // Example: "FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE,FR"
        // Example: "FREQ=DAILY;INTERVAL=1;COUNT=10"
        // Example: "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=15;UNTIL=20270101T000000Z"
        var freq = RecurrenceFrequency switch
        {
            1 => "DAILY", 2 => "WEEKLY", 3 => "MONTHLY", 4 => "YEARLY", _ => "DAILY"
        };
        var parts = new List<string> { $"FREQ={freq}" };
        if (RecurrenceInterval > 1)
            parts.Add($"INTERVAL={RecurrenceInterval}");

        // Add BYDAY for weekly
        if (RecurrenceFrequency == 2)
        {
            var days = new List<string>();
            if (RecurrenceDaySun) days.Add("SU");
            if (RecurrenceDayMon) days.Add("MO");
            if (RecurrenceDayTue) days.Add("TU");
            if (RecurrenceDayWed) days.Add("WE");
            if (RecurrenceDayThu) days.Add("TH");
            if (RecurrenceDayFri) days.Add("FR");
            if (RecurrenceDaySat) days.Add("SA");
            if (days.Count > 0)
                parts.Add($"BYDAY={string.Join(",", days)}");
        }

        // Add end condition
        if (RecurrenceEndType == 1) // After count
            parts.Add($"COUNT={RecurrenceEndCount}");
        else if (RecurrenceEndType == 2) // On date
            parts.Add($"UNTIL={RecurrenceEndDate:yyyyMMdd}T000000Z");

        return string.Join(";", parts);
    }

    private static string DescribeRrule(string rrule)
    {
        // Parse RRULE into human-readable description
        // e.g., "FREQ=WEEKLY;BYDAY=MO,WE,FR" → "Every week on Mon, Wed, Fri"
        // This can be a simple parser or a dictionary-based approach.
        // For a lesser LLM: implement a basic parser that reads FREQ, INTERVAL, BYDAY, COUNT, UNTIL.
        return rrule; // Placeholder — expand with real description logic
    }

    [RelayCommand]
    private async Task LoadCalendarsAsync(CancellationToken ct)
    {
        // Load calendars for the picker dropdown
        // Populate Calendars collection
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        // Validate: Title required, Start < End
        if (string.IsNullOrWhiteSpace(Title)) { ErrorMessage = "Title is required."; return; }
        var start = StartDate.Date + StartTime;
        var end = EndDate.Date + EndTime;
        if (end <= start) { ErrorMessage = "End must be after start."; return; }

        // Get server URL + token
        // Determine RRULE: if IsRecurring, BuildRrule(); else null

        if (IsEdit && _eventId.HasValue)
        {
            if (IsRecurringEvent && _editScope == EditScope.ThisOccurrence)
            {
                // Create recurrence exception
                var dto = new CreateCalendarEventDto
                {
                    CalendarId = SelectedCalendarId,
                    Title = Title, Description = Description, Location = Location,
                    StartUtc = start, EndUtc = end, IsAllDay = IsAllDay,
                    RecurringEventId = _eventId,
                    OriginalStartUtc = _originalStartUtc,
                    Url = Url
                };
                await _calendarApi.CreateEventAsync(serverUrl, token, dto, ct);
            }
            else
            {
                // Update the master event (or the exception if it already is one)
                var dto = new UpdateCalendarEventDto
                {
                    Title = Title, Description = Description, Location = Location,
                    StartUtc = start, EndUtc = end, IsAllDay = IsAllDay,
                    RecurrenceRule = IsRecurring ? BuildRrule() : null,
                    Url = Url
                };
                await _calendarApi.UpdateEventAsync(serverUrl, token, _eventId.Value, dto, ct);
            }
        }
        else
        {
            // Create new event
            var dto = new CreateCalendarEventDto
            {
                CalendarId = SelectedCalendarId,
                Title = Title, Description = Description, Location = Location,
                StartUtc = start, EndUtc = end, IsAllDay = IsAllDay,
                RecurrenceRule = IsRecurring ? BuildRrule() : null,
                Url = Url
            };
            await _calendarApi.CreateEventAsync(serverUrl, token, dto, ct);
        }

        await Shell.Current.GoToAsync(".."); // Navigate back
    }

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken ct)
    {
        // Show confirmation
        // If recurring, action sheet for "This occurrence" vs "All events"
        // Call appropriate delete API
        await Shell.Current.GoToAsync("..");
    }
}
```

---

### Step 7: CalendarPage View (files #6, #7)

**File:** `src/Clients/DotNetCloud.Client.Android/Views/CalendarPage.xaml`

**Pattern to follow:** `src/Clients/DotNetCloud.Client.Android/Views/FileBrowserPage.xaml`

**Layout structure (top to bottom):**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:DotNetCloud.Client.Android.ViewModels"
             x:Class="DotNetCloud.Client.Android.Views.CalendarPage"
             x:DataType="vm:CalendarViewModel"
             Title="Calendar"
             BackgroundColor="#0F172A"
             Shell.NavBarIsVisible="True">

    <!-- Use Shell.TitleView for the top bar with logo + title -->
    <Shell.TitleView>
        <HorizontalStackLayout Spacing="8" VerticalOptions="Center">
            <Image Source="logo.png" HeightRequest="36" WidthRequest="36"
                   VerticalOptions="Center"/>
            <Label Text="Calendar" FontSize="20" FontAttributes="Bold"
                   TextColor="#F1F5F9" VerticalOptions="Center"/>
        </HorizontalStackLayout>
    </Shell.TitleView>

    <Grid RowDefinitions="Auto,Auto,*,Auto,Auto" BackgroundColor="#0F172A">
        <!-- Row 0: View Toggle (Month | Week | Day) -->
        <!-- Use a horizontal stack of Buttons or a segmented-style control -->

        <!-- Row 1: Date Navigation (< Today [Date Label] >) -->
        <!-- HorizontalStackLayout with arrow buttons + label -->

        <!-- Row 2: Main content area -->
        <!-- Switch between MonthGrid, WeekGrid, DayGrid based on CurrentView -->
        <!-- Month view: Grid with 7 columns (Sun-Sat header + 6 rows of day cells) -->
        <!-- Week view: 7-column display -->
        <!-- Day view: Time grid -->

        <!-- Row 3: Calendar filter chips -->
        <!-- Horizontal CollectionView/StackLayout of chips with colored dot + name + toggle -->

        <!-- Row 4: FAB / Action bar -->
        <!-- "+" button to create new event -->
    </Grid>
</ContentPage>
```

**File:** `src/Clients/DotNetCloud.Client.Android/Views/CalendarPage.xaml.cs`

```csharp
using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _vm;

    public CalendarPage(CalendarViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.ErrorMessage = null;
        if (_vm.LoadCalendarsCommand.CanExecute(null))
            _vm.LoadCalendarsCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.ErrorMessage = null;
    }
}
```

---

### Step 8: EventDetailPage View (files #8, #9)

**File:** `src/Clients/DotNetCloud.Client.Android/Views/EventDetailPage.xaml`

Layout: ScrollView containing:
- Event title (large, bold)
- Date/time range (formatted nicely, e.g., "Mon, March 24 · 2:00 PM – 3:30 PM")
- Calendar name + colored dot
- Location (if present)
- Description (Label with Markdown-ish display or plain text)
- "Joining Info" section: URL if present
- Attendees section: list of attendees with RSVP status badges
- RSVP action buttons (if current user is an attendee)
- Bottom toolbar: Edit (pencil), Delete (trash)

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:DotNetCloud.Client.Android.ViewModels"
             x:Class="DotNetCloud.Client.Android.Views.EventDetailPage"
             x:DataType="vm:EventDetailViewModel"
             Title="Event Details"
             BackgroundColor="#0F172A">
    <!-- Content -->
</ContentPage>
```

**File:** `src/Clients/DotNetCloud.Client.Android/Views/EventDetailPage.xaml.cs`

Same pattern as `CalendarPage.xaml.cs` — inject ViewModel, set BindingContext, load on appearing.

---

### Step 9: EventEditPage View (files #10, #11)

**File:** `src/Clients/DotNetCloud.Client.Android/Views/EventEditPage.xaml`

Layout: ScrollView containing a form:
- Edit scope banner (visible when `IsRecurringEvent` is true): "Editing this occurrence only" label with "Change" button
- Title Entry
- Description Editor
- Location Entry
- Start/End: DatePicker + TimePicker pairs
- All-day Switch (when on, hides TimePickers)
- Calendar Picker (select from `Calendars`)
- URL Entry
- **Recurrence section** (expandable):
  - Frequency segmented control: None | Daily | Weekly | Monthly | Yearly
  - Interval: Label "Every" + Stepper/Entry for interval + frequency label
  - Days of week (visible when Weekly selected): 7 checkboxes/styled buttons
  - End condition: Picker (Never/After/On Date) + conditional fields
  - Human-readable preview label
- Save / Cancel buttons
- Delete button (only when editing)

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:DotNetCloud.Client.Android.ViewModels"
             x:Class="DotNetCloud.Client.Android.Views.EventEditPage"
             x:DataType="vm:EventEditViewModel"
             Title="{Binding EditTitle}"
             BackgroundColor="#0F172A">
    <!-- Content -->
</ContentPage>
```

**File:** `src/Clients/DotNetCloud.Client.Android/Views/EventEditPage.xaml.cs`

Same pattern as above. `EditTitle` should show "New Event" vs "Edit Event".

---

### Step 10: AppShell.xaml — Add Calendar Tab (file #13)

**File:** `src/Clients/DotNetCloud.Client.Android/AppShell.xaml`

Add a new `<ShellContent>` inside the existing `<TabBar Route="Main">`. Insert it after the Files tab and before the Settings tab:

```xml
        <ShellContent
            Route="Calendar"
            Title="Calendar"
            Icon="calendar_icon.png"
            ContentTemplate="{DataTemplate views:CalendarPage}"/>
```

**No `IsVisible="False"`** — Calendar is always visible (required module).

---

### Step 11: AppShell.xaml.cs — Register Detail Routes (file #14)

**File:** `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs`

Add two route registrations in the constructor, after the existing ones:

```csharp
Routing.RegisterRoute("EventDetail", typeof(EventDetailPage));
Routing.RegisterRoute("EventEdit", typeof(EventEditPage));
```

---

### Step 12: MauiProgram.cs — DI Registration (file #15)

**File:** `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`

Add three registrations following the existing pattern:

**a) Add using statement at top:**
```csharp
using DotNetCloud.Client.Android.Calendar;
```

**b) Add REST client registration (in the REST clients section, alongside the Music one):**
```csharp
// ── Calendar ──────────────────────────────────────────────────────
builder.Services.AddHttpClient<ICalendarRestClient, HttpCalendarRestClient>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
```

**c) Add ViewModels (in the ViewModels section):**
```csharp
builder.Services.AddTransient<CalendarViewModel>();
builder.Services.AddTransient<EventDetailViewModel>();
builder.Services.AddTransient<EventEditViewModel>();
```

**d) Add Pages (in the Pages section):**
```csharp
builder.Services.AddTransient<CalendarPage>();
builder.Services.AddTransient<EventDetailPage>();
builder.Services.AddTransient<EventEditPage>();
```

---

## Server API Reference

### Response Envelope Format

All API responses use the standard envelope:
```json
{
  "success": true,
  "data": { ... },
  "error": null
}
```

The `ReadEnvelopeDataAsync<T>` helper in `HttpCalendarRestClient` already handles unwrapping this.

### DTO Reference (from `DotNetCloud.Core/DTOs/CalendarDtos.cs`)

**`CalendarDto`:** `Id`, `OwnerId`, `Name`, `Description`, `Color`, `Timezone`, `IsDefault`, `IsVisible`, `IsDeleted`, `OrganizationId`, `CreatedAt`, `UpdatedAt`, `SyncToken`

**`CalendarEventDto`:** `Id`, `CalendarId`, `CreatedByUserId`, `Title`, `Description`, `Location`, `StartUtc`, `EndUtc`, `IsAllDay`, `Status` (enum: Tentative/Confirmed/Cancelled), `RecurrenceRule`, `RecurringEventId`, `OriginalStartUtc`, `Color`, `Url`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, `Attendees` (List of EventAttendeeDto), `Reminders` (List of EventReminderDto)

**`EventAttendeeDto`:** `UserId`, `Email`, `DisplayName`, `Role` (Required/Optional/Informational), `Status` (NeedsAction/Accepted/Declined/Tentative)

**`EventReminderDto`:** `Method` (Notification/Email), `MinutesBefore`

**`EventRsvpDto`:** `Status` (Accepted/Declined/Tentative), `Comment`

**`CreateCalendarEventDto`:** `CalendarId`, `Title`, `Description`, `Location`, `StartUtc`, `EndUtc`, `IsAllDay`, `RecurrenceRule`, `RecurringEventId`, `OriginalStartUtc`, `Color`, `Url`, `Attendees`, `Reminders`

**`UpdateCalendarEventDto`:** All fields optional (patch semantics) — `Title`, `Description`, `Location`, `StartUtc`, `EndUtc`, `IsAllDay`, `RecurrenceRule`, `Color`, `Url`, `Status`

### API Endpoints Quick Reference

| Method | URL | Purpose |
|--------|-----|---------|
| `GET` | `/api/v1/calendars` | List user's calendars |
| `GET` | `/api/v1/calendars/{id}` | Get calendar |
| `POST` | `/api/v1/calendars` | Create calendar |
| `PUT` | `/api/v1/calendars/{id}` | Update calendar |
| `DELETE` | `/api/v1/calendars/{id}` | Delete calendar |
| `GET` | `/api/v1/calendars/{id}/events?from=&to=&skip=&take=` | List events |
| `GET` | `/api/v1/calendars/events/{id}` | Get event |
| `POST` | `/api/v1/calendars/events` | Create event |
| `PUT` | `/api/v1/calendars/events/{id}` | Update event |
| `DELETE` | `/api/v1/calendars/events/{id}` | Delete event |
| `POST` | `/api/v1/calendars/events/{id}/rsvp` | RSVP to event |
| `GET` | `/api/v1/calendars/events/search?q=&from=&to=&skip=&take=` | Search events |

Full documentation: `docs/api/CALENDAR.md`

---

## Build Verification

After implementing all files, run:

```powershell
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj
```

Expected: Build succeeds with zero errors. Warnings for XML doc comments on public members should be addressed.

---

## Implementation Order (dependency-aware)

1. **calendar_icon.svg** — no dependencies
2. **ICalendarRestClient.cs** — no dependencies (references shared DTOs)
3. **HttpCalendarRestClient.cs** — depends on ICalendarRestClient
4. **CalendarViewModel.cs** — depends on ICalendarRestClient
5. **EventDetailViewModel.cs** — depends on ICalendarRestClient, EditScope enum
6. **EventEditViewModel.cs** — depends on ICalendarRestClient, EditScope enum
7. **CalendarPage.xaml + .cs** — depends on CalendarViewModel
8. **EventDetailPage.xaml + .cs** — depends on EventDetailViewModel
9. **EventEditPage.xaml + .cs** — depends on EventEditViewModel
10. **AppShell.xaml** — depends on CalendarPage
11. **AppShell.xaml.cs** — depends on EventDetailPage, EventEditPage
12. **MauiProgram.cs** — depends on everything above

Files 2–6 can be written in any order after file 1. Files 7–9 need their ViewModels. Files 10–12 are integration — do them last.
