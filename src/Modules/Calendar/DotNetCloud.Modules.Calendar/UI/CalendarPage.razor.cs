using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace DotNetCloud.Modules.Calendar.UI;

/// <summary>Code-behind for the <see cref="CalendarPage"/> component.</summary>
public partial class CalendarPage : ComponentBase
{
    private readonly List<CalendarDto> _calendars = [];
    private readonly List<CalendarEventDto> _events = [];
    private string _activeView = "month";
    private CalendarDto? _selectedCalendar;
    private DateTime _anchorDate = DateTime.UtcNow.Date;
    private CalendarEditorModel? _calendarEditor;
    private EventEditorModel? _eventEditor;
    private bool _showShareDialog;
    private bool _showImportPanel;
    private Guid? _currentUserId;
    private string? _operationError;
    private string _importText = string.Empty;
    private bool _sidebarCollapsed;
    private List<DncShareEntry> _shareDialogShares = [];
    private bool _isLoadingShareDialogShares;

    /// <summary>Permission choices for calendar shares (ReadOnly / ReadWrite).</summary>
    private static readonly IReadOnlyList<DncSharePermissionOption> CalendarPermissionOptions =
    [
        new() { Value = "Read", Label = "View only" },
        new() { Value = "ReadWrite", Label = "Can edit" },
    ];

    // ── Contact search state ──
    private string _contactSearchQuery = string.Empty;
    private IReadOnlyList<ContactSearchResultDto> _contactSearchResults = [];
    private bool _contactSearchVisible;
    private int _contactSearchActiveIndex = -1;
    private CancellationTokenSource? _contactSearchCts;

    // ── Modal state ──
    private bool _showEventModal;
    private bool _showCalendarModal;
    private bool _showDayModal;
    private bool _showDeleteModal;
    private bool _showDeleteCalendarModal;
    private DateTime _dayModalDate;
    private List<CalendarEventDto> _dayModalEvents = [];

    // ── Organization state ──
    private List<OrgInfo> _organizations = [];
    private Guid? _selectedOrgId;
    private bool _showOrgDropdown;
    private bool _canCreateOrgCalendar = true;

    private string SidebarHeading => _selectedOrgId is null
        ? "Personal"
        : (_organizations.FirstOrDefault(o => o.Id == _selectedOrgId)?.Name ?? "Org") + " Calendars";

    private string RangeLabel => _activeView switch
    {
        "day" => _anchorDate.ToLocalTime().ToString("D"),
        "week" => $"Week of {_anchorDate.ToLocalTime():d}",
        _ => _anchorDate.ToLocalTime().ToString("Y")
    };

    private static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private sealed record OrgInfo(Guid Id, string Name);

    private List<DateTime> GetMonthGridDays()
    {
        var firstOfMonth = new DateTime(_anchorDate.Year, _anchorDate.Month, 1);
        var startOffset = (int)firstOfMonth.DayOfWeek;
        var startDate = firstOfMonth.AddDays(-startOffset);
        var days = new List<DateTime>(42);
        for (var i = 0; i < 42; i++)
        {
            days.Add(startDate.AddDays(i));
        }
        return days;
    }

    private List<DateTime> GetWeekDays()
    {
        var startOfWeek = _anchorDate.Date.AddDays(-(int)_anchorDate.DayOfWeek);
        var days = new List<DateTime>(7);
        for (var i = 0; i < 7; i++)
        {
            days.Add(startOfWeek.AddDays(i));
        }
        return days;
    }

    private List<CalendarEventDto> GetEventsForDate(DateTime date)
    {
        return _events
            .Where(e =>
            {
                var localStart = e.StartUtc.ToLocalTime();
                var localEnd = e.EndUtc.ToLocalTime();
                // Event starts on this date, OR spans across it (multi-day)
                // End date is inclusive — an all-day event April 27-30 shows on all four days
                return localStart.Date <= date.Date && localEnd.Date >= date.Date;
            })
            .OrderBy(e => e.IsAllDay ? 0 : 1)
            .ThenBy(e => e.StartUtc)
            .ToList();
    }

    private List<CalendarEventDto> GetEventsForHour(DateTime date, int hour)
    {
        return _events
            .Where(e =>
            {
                var local = e.StartUtc.ToLocalTime();
                return local.Date == date.Date && local.Hour == hour;
            })
            .OrderBy(e => e.StartUtc)
            .ToList();
    }

    private string GetEventColor(CalendarEventDto evt)
    {
        var color = evt.Color
            ?? _calendars.FirstOrDefault(c => c.Id == evt.CalendarId)?.Color
            ?? "#3b82f6";
        return $"color-mix(in srgb, {color} 20%, transparent)";
    }

    private void CreateEventAtHour(int hour)
    {
        if (_selectedCalendar is null)
        {
            return;
        }

        _eventEditor = new EventEditorModel
        {
            CalendarId = _selectedCalendar.Id,
            StartLocal = _anchorDate.Date.AddHours(hour),
            EndLocal = _anchorDate.Date.AddHours(hour + 1)
        };
        _showEventModal = true;
    }

    private async Task ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        await SaveSidebarCollapsedStateAsync();
    }

    private async Task SaveSidebarCollapsedStateAsync()
    {
        try
        {
            await JSRuntime.InvokeAsync<object?>("localStorage.setItem", new object?[] { "dotnetcloud.sidebar:calendar",
_sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch
        {
            // localStorage unavailable
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var collapsed = await JSRuntime.InvokeAsync<string>("localStorage.getItem", new object?[] {
"dotnetcloud.sidebar:calendar" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
            {
                _sidebarCollapsed = parsed;
            }
        }
        catch
        {
            // localStorage unavailable
        }

        await ResolveCurrentUserAsync();
        await LoadOrganizationsAsync();
        await ReloadCalendarsAsync();
        await TryOpenEventFromQueryAsync();
        await TryOpenCalendarFromQueryAsync();
    }

    private async Task TryOpenEventFromQueryAsync()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        if (!QueryHelpers.ParseQuery(uri.Query).TryGetValue("eventId", out var eventIdValue))
            return;
        if (!Guid.TryParse(eventIdValue, out var eventId))
            return;

        try
        {
            var evt = await ApiClient.GetEventAsync(eventId);
            if (evt is null) return;

            // Select the calendar that owns this event
            var calendar = _calendars.FirstOrDefault(c => c.Id == evt.CalendarId);
            if (calendar is not null)
            {
                _selectedCalendar = calendar;
                _anchorDate = evt.StartUtc.ToLocalTime();
                await ReloadEventsAsync();
            }

            // Navigate to month view only — do not open the edit modal for search result clicks.
        }
        catch
        {
            // Event not found or access denied — ignore
        }
    }

    private async Task TryOpenCalendarFromQueryAsync()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        if (!QueryHelpers.ParseQuery(uri.Query).TryGetValue("calendarId", out var calendarIdValue))
            return;
        if (!Guid.TryParse(calendarIdValue, out var calendarId))
            return;

        // Select the shared/owned calendar by id when present in the visible calendar list.
        var calendar = _calendars.FirstOrDefault(c => c.Id == calendarId);
        if (calendar is null) return;

        _selectedCalendar = calendar;
        _anchorDate = DateTime.Today;
        await ReloadEventsAsync();
    }

    private async Task ResolveCurrentUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;

        var claimValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (Guid.TryParse(claimValue, out var userId))
        {
            _currentUserId = userId;
        }
    }

    private bool CanCurrentUserRsvp(CalendarEventDto eventDto)
    {
        return _currentUserId.HasValue && eventDto.Attendees.Any(a => a.UserId == _currentUserId.Value);
    }

    private async Task LoadOrganizationsAsync()
    {
        if (!_currentUserId.HasValue)
            return;

        try
        {
            _organizations = await GetUserOrganizationsAsync(_currentUserId.Value);
        }
        catch
        {
            _organizations = [];
        }
    }

    private async Task<List<OrgInfo>> GetUserOrganizationsAsync(Guid userId)
    {
        try
        {
            var orgs = await OrgDirectory.GetUserOrganizationsAsync(userId);
            return orgs.Select(o => new OrgInfo(o.Id, o.Name)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void ToggleOrgDropdown()
    {
        _showOrgDropdown = !_showOrgDropdown;
    }

    private async Task SelectOrganizationAsync(Guid? orgId)
    {
        _selectedOrgId = orgId;
        _showOrgDropdown = false;

        if (orgId.HasValue && _currentUserId.HasValue)
        {
            _canCreateOrgCalendar = await OrgDirectory.HasManagerOrAboveRoleAsync(orgId.Value, _currentUserId.Value);
        }
        else
        {
            _canCreateOrgCalendar = true;
        }

        await ReloadCalendarsAsync();
    }

    private async Task ReloadCalendarsAsync()
    {
        _calendars.Clear();
        var allCalendars = await ApiClient.ListCalendarsAsync();

        // Filter by selected org (null = user-owned, specific GUID = org-owned)
        if (_selectedOrgId is null)
        {
            // Show user-owned + shared calendars (org calendars excluded)
            _calendars.AddRange(allCalendars.Where(c => c.OrganizationId is null));
        }
        else
        {
            // Show only calendars for the selected org
            _calendars.AddRange(allCalendars.Where(c => c.OrganizationId == _selectedOrgId.Value));
        }

        // Auto-create a default "Personal" calendar on first access (user-owned only)
        if (_calendars.Count == 0 && _selectedOrgId is null)
        {
            var created = await ApiClient.CreateCalendarAsync(new CreateCalendarDto
            {
                Name = "Personal",
                Color = "#3b82f6",
                Description = "Default personal calendar"
            });

            if (created is not null)
            {
                _calendars.Add(created);
            }
        }

        _selectedCalendar ??= _calendars.FirstOrDefault();
        if (_selectedCalendar is not null)
        {
            await ReloadEventsAsync();
        }
    }

    private async Task ReloadEventsAsync()
    {
        if (_selectedCalendar is null)
        {
            _events.Clear();
            return;
        }

        var (startUtc, endUtc) = GetRange();
        _events.Clear();
        _events.AddRange(await ApiClient.ListEventsAsync(_selectedCalendar.Id, startUtc, endUtc));
    }

    private (DateTime startUtc, DateTime endUtc) GetRange()
    {
        return _activeView switch
        {
            "day" => (_anchorDate.Date, _anchorDate.Date.AddDays(1)),
            "week" => (_anchorDate.Date, _anchorDate.Date.AddDays(7)),
            "agenda" => (_anchorDate.Date, _anchorDate.Date.AddDays(30)),
            _ => (new DateTime(_anchorDate.Year, _anchorDate.Month, 1), new DateTime(_anchorDate.Year, _anchorDate.Month,
1).AddMonths(1))
        };
    }

    private async Task SelectCalendarAsync(Guid calendarId)
    {
        _selectedCalendar = _calendars.FirstOrDefault(c => c.Id == calendarId);
        await ReloadEventsAsync();
    }

    private async Task SetView(string view)
    {
        _activeView = view;
        await ReloadEventsAsync();
    }

    private async Task PrevRange()
    {
        _anchorDate = _activeView switch
        {
            "day" => _anchorDate.AddDays(-1),
            "week" => _anchorDate.AddDays(-7),
            "agenda" => _anchorDate.AddDays(-30),
            _ => _anchorDate.AddMonths(-1)
        };

        await ReloadEventsAsync();
    }

    private async Task NextRange()
    {
        _anchorDate = _activeView switch
        {
            "day" => _anchorDate.AddDays(1),
            "week" => _anchorDate.AddDays(7),
            "agenda" => _anchorDate.AddDays(30),
            _ => _anchorDate.AddMonths(1)
        };

        await ReloadEventsAsync();
    }

    private async Task GoToToday()
    {
        _anchorDate = DateTime.UtcNow.Date;
        await ReloadEventsAsync();
    }

    private void BeginCreateCalendar()
    {
        _calendarEditor = new CalendarEditorModel
        {
            OrganizationId = _selectedOrgId
        };
        _showCalendarModal = true;
    }

    private void BeginEditCalendar()
    {
        if (_selectedCalendar is null) return;

        _calendarEditor = new CalendarEditorModel
        {
            Id = _selectedCalendar.Id,
            Name = _selectedCalendar.Name,
            Description = _selectedCalendar.Description,
            Color = _selectedCalendar.Color,
            OrganizationId = _selectedCalendar.OrganizationId
        };
        _showCalendarModal = true;
    }

    private async Task SaveCalendarAsync()
    {
        if (_calendarEditor is null)
        {
            return;
        }

        if (_calendarEditor.Id is null)
        {
            await ApiClient.CreateCalendarAsync(_calendarEditor.ToCreateDto());
        }
        else
        {
            await ApiClient.UpdateCalendarAsync(_calendarEditor.Id.Value, _calendarEditor.ToUpdateDto());
        }

        _calendarEditor = null;
        _showCalendarModal = false;
        await ReloadCalendarsAsync();
    }

    private void CancelCalendarEdit()
    {
        _calendarEditor = null;
        _showCalendarModal = false;
    }

    private void DeleteCalendarAsync()
    {
        if (_selectedCalendar is null) return;
        _showDeleteCalendarModal = true;
    }

    private async Task ConfirmDeleteCalendarAsync()
    {
        if (_selectedCalendar is null) return;

        await ApiClient.DeleteCalendarAsync(_selectedCalendar.Id);
        _selectedCalendar = null;
        _showDeleteCalendarModal = false;
        await ReloadCalendarsAsync();
    }

    private void CancelDeleteCalendar()
    {
        _showDeleteCalendarModal = false;
    }

    private void BeginCreateEvent()
    {
        if (_selectedCalendar is null)
        {
            return;
        }

        _eventEditor = new EventEditorModel
        {
            CalendarId = _selectedCalendar.Id,
            StartLocal = DateTime.UtcNow,
            EndLocal = DateTime.UtcNow.AddHours(1)
        };

        _showEventModal = true;
    }

    private void BeginEditEvent(CalendarEventDto dto)
    {
        _eventEditor = EventEditorModel.From(dto);
        _showEventModal = true;
    }

    private async Task SaveEventAsync()
    {
        if (_eventEditor is null)
        {
            return;
        }

        if (_eventEditor.Id is null)
        {
            await ApiClient.CreateEventAsync(_eventEditor.ToCreateDto());
        }
        else
        {
            await ApiClient.UpdateEventAsync(_eventEditor.Id.Value, _eventEditor.ToUpdateDto());
        }

        _eventEditor = null;
        _showEventModal = false;
        await ReloadEventsAsync();
    }

    private void CancelEventEdit()
    {
        _eventEditor = null;
        _showEventModal = false;
    }

    // ── Contact search ──

    private async Task SearchContactsAsync(string query, int attendeeIndex)
    {
        _contactSearchCts?.Cancel();
        _contactSearchCts = new CancellationTokenSource();
        var ct = _contactSearchCts.Token;

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            _contactSearchResults = [];
            _contactSearchVisible = false;
            return;
        }

        try
        {
            await Task.Delay(300, ct);
            if (ct.IsCancellationRequested) return;

            _contactSearchResults = await ApiClient.SearchContactsAsync(query, 10, ct);
            _contactSearchActiveIndex = attendeeIndex;
            _contactSearchVisible = _contactSearchResults.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Expected on debounce
        }
        catch
        {
            _contactSearchResults = [];
            _contactSearchVisible = false;
        }
    }

    private void SelectContactForAttendee(ContactSearchResultDto contact, AttendeeEditorModel attendee)
    {
        attendee.ContactId = contact.ContactId;
        attendee.DisplayName = contact.DisplayName;
        var primaryEmail = contact.Emails.FirstOrDefault(e => e.Label == "work" || e.Label == "home");
        if (string.IsNullOrEmpty(primaryEmail.Address))
            primaryEmail = contact.Emails.FirstOrDefault();
        attendee.Email = primaryEmail.Address ?? string.Empty;
        _contactSearchVisible = false;
        _contactSearchQuery = string.Empty;
        _contactSearchResults = [];
    }

    private void StartManualAttendeeEntry(AttendeeEditorModel attendee)
    {
        attendee.ContactId = null;
        _contactSearchVisible = false;
    }

    private void DeleteEventAsync()
    {
        // Show delete confirmation modal instead of deleting immediately
        _showEventModal = false;
        _showDeleteModal = true;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_eventEditor?.Id is not Guid eventId)
        {
            return;
        }

        await ApiClient.DeleteEventAsync(eventId);
        _eventEditor = null;
        _showDeleteModal = false;
        await ReloadEventsAsync();
    }

    private void CancelDelete()
    {
        _showDeleteModal = false;
        // Re-open event modal so user can continue editing
        _showEventModal = true;
    }

    private void ShowDayDetails(DateTime date, List<CalendarEventDto> events)
    {
        _dayModalDate = date;
        _dayModalEvents = events;
        _showDayModal = true;
    }

    private void CloseDayModal()
    {
        _showDayModal = false;
    }

    private void CreateEventOnDay()
    {
        if (_selectedCalendar is null)
        {
            return;
        }

        _showDayModal = false;
        _eventEditor = new EventEditorModel
        {
            CalendarId = _selectedCalendar.Id,
            StartLocal = _dayModalDate.Date.AddHours(9),
            EndLocal = _dayModalDate.Date.AddHours(10)
        };
        _showEventModal = true;
    }

    private void DayClicked(DateTime day)
    {
        if (_selectedCalendar is null)
        {
            return;
        }

        var dayEvents = GetEventsForDate(day);
        ShowDayDetails(day.Date, dayEvents);
    }

    private async Task RsvpAsync(Guid eventId, AttendeeStatus status)
    {
        _operationError = null;

        try
        {
            await ApiClient.RsvpAsync(eventId, new EventRsvpDto { Status = status });
            await ReloadEventsAsync();
        }
        catch (HttpRequestException)
        {
            _operationError = "RSVP failed for this event. You may not be listed as an attendee.";
        }
    }

    private async Task OpenShareDialogAsync()
    {
        if (_selectedCalendar is null)
            return;

        _showShareDialog = true;
        _shareDialogShares = [];
        _isLoadingShareDialogShares = true;
        StateHasChanged();

        try
        {
            await LoadShareDialogEntriesAsync();
        }
        finally
        {
            _isLoadingShareDialogShares = false;
        }
    }

    private void CloseShareDialogAsync()
    {
        _showShareDialog = false;
    }

    private async Task LoadShareDialogEntriesAsync()
    {
        if (_selectedCalendar is null)
            return;

        var shares = await ApiClient.ListSharesAsync(_selectedCalendar.Id);
        _shareDialogShares = await MapShareDtosToEntriesAsync(shares);
    }

    private async Task HandleShareCreatedAsync(DncShareCreatedEventArgs args)
    {
        if (_selectedCalendar is null)
            return;

        var perm = args.Permission == "ReadWrite" ? CalendarSharePermission.ReadWrite : CalendarSharePermission.ReadOnly;
        Guid? targetUserId = args.ShareType == "Team" ? null : args.TargetId;
        Guid? targetTeamId = args.ShareType == "Team" ? args.TargetId : null;

        var result = await ApiClient.ShareCalendarAsync(_selectedCalendar.Id, targetUserId, targetTeamId, perm.ToString());
        if (result is not null)
        {
            await LoadShareDialogEntriesAsync();
        }
    }

    private async Task HandleShareRemovedAsync(Guid shareId)
    {
        await ApiClient.RevokeShareAsync(shareId);
        _shareDialogShares = [.. _shareDialogShares.Where(e => e.ShareId != shareId)];
    }

    private async Task<List<DncShareEntry>> MapShareDtosToEntriesAsync(IReadOnlyList<CalendarShareResponse> shares)
    {
        var userNames = await ResolveUserNamesAsync(shares);
        var entries = new List<DncShareEntry>(shares.Count);

        foreach (var share in shares)
        {
            var isTeam = share.SharedWithTeamId is not null;
            string recipientName;

            if (isTeam)
            {
                recipientName = await ResolveTeamNameAsync(share.SharedWithTeamId!.Value)
                    ?? $"{share.SharedWithTeamId.Value:N}"[..8];
            }
            else if (share.SharedWithUserId is { } sharedWithUserId)
            {
                recipientName = userNames.TryGetValue(sharedWithUserId, out var name)
                    ? name
                    : $"{sharedWithUserId:N}"[..8];
            }
            else
            {
                recipientName = "Unknown";
            }

            entries.Add(new DncShareEntry
            {
                ShareId = share.Id,
                RecipientName = recipientName,
                RecipientType = isTeam ? "Team" : "User",
                Permission = share.Permission == CalendarSharePermission.ReadWrite ? "ReadWrite" : "Read",
                CreatedAt = share.CreatedAt
            });
        }

        return entries;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(IReadOnlyList<CalendarShareResponse> shares)
    {
        var ids = shares
            .Where(s => s.SharedWithTeamId is null && s.SharedWithUserId is not null)
            .Select(s => s.SharedWithUserId!.Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await UserDirectory.GetDisplayNamesAsync(ids);
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private async Task<string?> ResolveTeamNameAsync(Guid teamId)
    {
        try
        {
            var team = await TeamDirectory.GetTeamAsync(teamId);
            return team?.Name;
        }
        catch
        {
            return null;
        }
    }

    private async Task ExportCalendarAsync()
    {
        if (_selectedCalendar is null)
        {
            return;
        }

        _exportedIcal = await ApiClient.ExportCalendarICalAsync(_selectedCalendar.Id);
    }

    private async Task ImportCalendarAsync()
    {
        if (_selectedCalendar is null || string.IsNullOrWhiteSpace(_importText))
        {
            return;
        }

        await ApiClient.ImportICalAsync(_selectedCalendar.Id, _importText);
        _importText = string.Empty;
        _showImportPanel = false;
        await ReloadEventsAsync();
    }

    private string? _exportedIcal;

    private sealed class CalendarEditorModel
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public Guid? OrganizationId { get; set; }

        public CreateCalendarDto ToCreateDto() => new()
        {
            Name = Name,
            Description = Description,
            Color = Color,
            OrganizationId = OrganizationId
        };

        public UpdateCalendarDto ToUpdateDto() => new()
        {
            Name = Name,
            Description = Description,
            Color = Color
        };
    }

    private sealed class EventEditorModel
    {
        public Guid? Id { get; set; }
        public Guid CalendarId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }

        // Separate date/time bindings so the time picker doesn't clobber the date,
        // and vice versa. InputDate of type Time always produces DateTime.Today +
        // the selected time-of-day, so we must preserve the date portion ourselves.
        public DateTime StartDate { get => StartLocal.Date; set => StartLocal = value.Date + StartLocal.TimeOfDay; }
        public DateTime StartTime { get => StartLocal; set => StartLocal = StartLocal.Date + value.TimeOfDay; }
        public DateTime EndDate { get => EndLocal.Date; set => EndLocal = value.Date + EndLocal.TimeOfDay; }
        public DateTime EndTime { get => EndLocal; set => EndLocal = EndLocal.Date + value.TimeOfDay; }

        public bool IsAllDay { get; set; }
        public string? Color { get; set; }
        public string? Url { get; set; }
        public string? RecurrenceRule { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
        public string RecurrenceType { get; set; } = "None";
        public List<AttendeeEditorModel> Attendees { get; set; } = [];
        public List<ReminderEditorModel> Reminders { get; set; } = [];
        // Advanced recurrence
        public bool RecurOnMon { get; set; }
        public bool RecurOnTue { get; set; }
        public bool RecurOnWed { get; set; }
        public bool RecurOnThu { get; set; }
        public bool RecurOnFri { get; set; }
        public bool RecurOnSat { get; set; }
        public bool RecurOnSun { get; set; }
        // Monthly BYDAY position: null=no preference, 1=first, 2=second, 3=third, 4=fourth, -1=last
        public int? MonthlyByDayPosition { get; set; }

        public static EventEditorModel From(CalendarEventDto dto) => new()
        {
            Id = dto.Id,
            CalendarId = dto.CalendarId,
            Title = dto.Title,
            Description = dto.Description,
            Location = dto.Location,
            StartLocal = dto.StartUtc.ToLocalTime(),
            EndLocal = dto.EndUtc.ToLocalTime(),
            IsAllDay = dto.IsAllDay,
            Color = dto.Color,
            Url = dto.Url,
            RecurrenceRule = dto.RecurrenceRule,
            RecurrenceType = ParseRecurrenceType(dto.RecurrenceRule),
            Attendees = dto.Attendees.Select(a => new AttendeeEditorModel
            {
                ContactId = a.UserId,
                Email = a.Email,
                DisplayName = a.DisplayName ?? "",
                Role = a.Role.ToString(),
                Status = a.Status.ToString()
            }).ToList(),
            Reminders = dto.Reminders.Select(r => new ReminderEditorModel
            {
                Method = r.Method.ToString(),
                MinutesBefore = r.MinutesBefore
            }).ToList(),
            RecurOnMon = HasByDay(dto.RecurrenceRule, "MO"),
            RecurOnTue = HasByDay(dto.RecurrenceRule, "TU"),
            RecurOnWed = HasByDay(dto.RecurrenceRule, "WE"),
            RecurOnThu = HasByDay(dto.RecurrenceRule, "TH"),
            RecurOnFri = HasByDay(dto.RecurrenceRule, "FR"),
            RecurOnSat = HasByDay(dto.RecurrenceRule, "SA"),
            RecurOnSun = HasByDay(dto.RecurrenceRule, "SU"),
            MonthlyByDayPosition = ParseMonthlyByDayPosition(dto.RecurrenceRule)
        };

        public CreateCalendarEventDto ToCreateDto() => new()
        {
            CalendarId = CalendarId,
            Title = Title,
            Description = Description,
            Location = Location,
            StartUtc = StartLocal.ToUniversalTime(),
            EndUtc = EndLocal.ToUniversalTime(),
            IsAllDay = IsAllDay,
            Color = Color,
            Url = Url,
            RecurrenceRule = BuildRrule(),
            Attendees = Attendees.Select(a => new EventAttendeeDto
            {
                UserId = a.ContactId,
                Email = a.Email,
                DisplayName = string.IsNullOrWhiteSpace(a.DisplayName) ? null : a.DisplayName,
                Role = Enum.TryParse<AttendeeRole>(a.Role, out var role) ? role : AttendeeRole.Required,
                Status = Enum.TryParse<AttendeeStatus>(a.Status, out var st) ? st : AttendeeStatus.NeedsAction
            }).ToList(),
            Reminders = Reminders.Select(r => new EventReminderDto
            {
                Method = Enum.TryParse<ReminderMethod>(r.Method, out var m) ? m : ReminderMethod.Notification,
                MinutesBefore = r.MinutesBefore
            }).ToList()
        };

        public UpdateCalendarEventDto ToUpdateDto() => new()
        {
            Title = Title,
            Description = Description,
            Location = Location,
            StartUtc = StartLocal.ToUniversalTime(),
            EndUtc = EndLocal.ToUniversalTime(),
            IsAllDay = IsAllDay,
            Color = Color,
            Url = Url,
            RecurrenceRule = BuildRrule(),
            Attendees = Attendees.Select(a => new EventAttendeeDto
            {
                UserId = a.ContactId,
                Email = a.Email,
                DisplayName = string.IsNullOrWhiteSpace(a.DisplayName) ? null : a.DisplayName,
                Role = Enum.TryParse<AttendeeRole>(a.Role, out var role) ? role : AttendeeRole.Required,
                Status = Enum.TryParse<AttendeeStatus>(a.Status, out var st) ? st : AttendeeStatus.NeedsAction
            }).ToList(),
            Reminders = Reminders.Select(r => new EventReminderDto
            {
                Method = Enum.TryParse<ReminderMethod>(r.Method, out var m) ? m : ReminderMethod.Notification,
                MinutesBefore = r.MinutesBefore
            }).ToList()
        };

        private string? BuildRrule()
        {
            if (string.IsNullOrEmpty(RecurrenceType) || RecurrenceType == "None")
            {
                return null;
            }

            var freq = RecurrenceType switch
            {
                "Daily" => "DAILY",
                "Weekly" => "WEEKLY",
                "Biweekly" => "WEEKLY",
                "Monthly" => "MONTHLY",
                "Yearly" => "YEARLY",
                _ => RecurrenceType.ToUpperInvariant()
            };

            var rule = $"FREQ={freq}";

            if (RecurrenceType == "Biweekly")
            {
                rule += ";INTERVAL=2";
            }

            // Add BYDAY for weekly/biweekly with specific day selections
            var days = GetSelectedDays();
            if (days.Count > 0 && days.Count < 7)
            {
                if (RecurrenceType == "Monthly" && MonthlyByDayPosition.HasValue)
                {
                    // Monthly BYDAY with position: e.g., "1MO" = first Monday, "-1FR" = last Friday
                    var pos = MonthlyByDayPosition.Value;
                    var posStr = pos == -1 ? "-1" : pos.ToString();
                    rule += $";BYDAY={posStr}{string.Join($",{posStr}", days)}";
                }
                else
                {
                    rule += $";BYDAY={string.Join(",", days)}";
                }
            }

            if (RecurrenceEndDate.HasValue)
            {
                rule += $";UNTIL={RecurrenceEndDate.Value.ToUniversalTime():yyyyMMddTHHmmssZ}";
            }

            return rule;
        }

        private List<string> GetSelectedDays()
        {
            var days = new List<string>();
            if (RecurOnMon) days.Add("MO");
            if (RecurOnTue) days.Add("TU");
            if (RecurOnWed) days.Add("WE");
            if (RecurOnThu) days.Add("TH");
            if (RecurOnFri) days.Add("FR");
            if (RecurOnSat) days.Add("SA");
            if (RecurOnSun) days.Add("SU");
            return days;
        }

        private static string ParseRecurrenceType(string? rrule)
        {
            if (string.IsNullOrWhiteSpace(rrule))
            {
                return "None";
            }

            var upper = rrule.ToUpperInvariant();
            var hasInterval2 = upper.Contains("INTERVAL=2") || upper.Contains("INTERVAL=2;");

            if (upper.StartsWith("FREQ=WEEKLY") && hasInterval2)
            {
                return "Biweekly";
            }

            if (upper.StartsWith("FREQ=DAILY")) return "Daily";
            if (upper.StartsWith("FREQ=WEEKLY")) return "Weekly";
            if (upper.StartsWith("FREQ=MONTHLY")) return "Monthly";
            if (upper.StartsWith("FREQ=YEARLY")) return "Yearly";

            return "None";
        }

        private static bool HasByDay(string? rrule, string day)
        {
            if (string.IsNullOrWhiteSpace(rrule))
                return false;

            var upper = rrule.ToUpperInvariant();
            // BYDAY values can be bare (MO) or position-prefixed (2TU, -1FR)
            return upper.Contains(day) && (
                upper.Contains($"BYDAY={day}") ||
                upper.Contains($",{day}") ||
                upper.EndsWith(day) ||
                // Covers position-prefixed: BYDAY=2TU or ,2TU
                System.Text.RegularExpressions.Regex.IsMatch(upper, $@"[=,]-?\d*{day}(?:,|;|$)")
            );
        }

        private static int? ParseMonthlyByDayPosition(string? rrule)
        {
            if (string.IsNullOrWhiteSpace(rrule))
                return null;

            // Look for BYDAY with position prefix: 1MO, 2TU, -1FR, etc.
            var upper = rrule.ToUpperInvariant();
            var byDayIdx = upper.IndexOf("BYDAY=", StringComparison.Ordinal);
            if (byDayIdx < 0)
                return null;

            var byDayPart = upper[(byDayIdx + 6)..];
            var semiIdx = byDayPart.IndexOf(';', StringComparison.Ordinal);
            if (semiIdx >= 0)
                byDayPart = byDayPart[..semiIdx];

            // Check first day value for position prefix
            var firstDay = byDayPart.Split(',')[0].Trim();
            if (firstDay.Length >= 3 && int.TryParse(firstDay[..^2], out var pos))
            {
                return pos; // 1, 2, 3, 4, or -1
            }

            return null;
        }
    }

    private void EnsureMonthlyByDayPosition()
    {
        if (_eventEditor is not null && !_eventEditor.MonthlyByDayPosition.HasValue)
        {
            _eventEditor.MonthlyByDayPosition = 1; // Default to "First"
        }
    }

    private sealed class AttendeeEditorModel
    {
        public Guid? ContactId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = "Required";
        public string Status { get; set; } = "NeedsAction";
    }

    private sealed class ReminderEditorModel
    {
        public string Method { get; set; } = "Notification";
        public int MinutesBefore { get; set; } = 15;
    }

    private static IReadOnlyList<CrossModuleLinkRequest> BuildAttendeeContactRequests(CalendarEventDto eventDto)
    {
        return eventDto.Attendees
            .Where(a => a.UserId.HasValue)
            .Select(a => new CrossModuleLinkRequest
            {
                LinkType = CrossModuleLinkType.Contact,
                TargetId = a.UserId!.Value
            })
            .DistinctBy(r => r.TargetId)
            .ToList();
    }
}
