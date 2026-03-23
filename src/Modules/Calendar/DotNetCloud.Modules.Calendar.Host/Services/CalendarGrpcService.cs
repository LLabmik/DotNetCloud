using System.Globalization;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Host.Protos;
using DotNetCloud.Modules.Calendar.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.Calendar.Host.Services;

/// <summary>
/// gRPC service implementation for the Calendar module.
/// Exposes calendar and event operations over gRPC for the core server to invoke.
/// </summary>
public sealed class CalendarGrpcService : Protos.CalendarGrpcService.CalendarGrpcServiceBase
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventService _eventService;
    private readonly IICalendarService _icalService;
    private readonly ILogger<CalendarGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarGrpcService"/> class.
    /// </summary>
    public CalendarGrpcService(
        ICalendarService calendarService,
        ICalendarEventService eventService,
        IICalendarService icalService,
        ILogger<CalendarGrpcService> logger)
    {
        _calendarService = calendarService;
        _eventService = eventService;
        _icalService = icalService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<CalendarResponse> CreateCalendar(
        CreateCalendarRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new CalendarResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        var dto = new CreateCalendarDto
        {
            Name = request.Name,
            Description = NullIfEmpty(request.Description),
            Color = NullIfEmpty(request.Color),
            Timezone = string.IsNullOrEmpty(request.Timezone) ? "UTC" : request.Timezone
        };

        try
        {
            var result = await _calendarService.CreateCalendarAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new CalendarResponse { Success = true, Calendar = ToCalendarMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new CalendarResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<CalendarResponse> GetCalendar(
        GetCalendarRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CalendarId, out var calendarId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new CalendarResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var result = await _calendarService.GetCalendarAsync(
            calendarId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

        return result is null
            ? new CalendarResponse { Success = false, ErrorMessage = "Calendar not found." }
            : new CalendarResponse { Success = true, Calendar = ToCalendarMessage(result) };
    }

    /// <inheritdoc />
    public override async Task<ListCalendarsResponse> ListCalendars(
        ListCalendarsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListCalendarsResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        var results = await _calendarService.ListCalendarsAsync(
            new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

        var response = new ListCalendarsResponse { Success = true };
        response.Calendars.AddRange(results.Select(ToCalendarMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<EventResponse> CreateEvent(
        CreateEventRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) ||
            !Guid.TryParse(request.CalendarId, out var calendarId))
            return new EventResponse { Success = false, ErrorMessage = "Invalid ID format." };

        if (!DateTime.TryParse(request.StartUtc, CultureInfo.InvariantCulture, out var startUtc) ||
            !DateTime.TryParse(request.EndUtc, CultureInfo.InvariantCulture, out var endUtc))
            return new EventResponse { Success = false, ErrorMessage = "Invalid date format." };

        var dto = new CreateCalendarEventDto
        {
            CalendarId = calendarId,
            Title = request.Title,
            Description = NullIfEmpty(request.Description),
            Location = NullIfEmpty(request.Location),
            StartUtc = startUtc,
            EndUtc = endUtc,
            IsAllDay = request.IsAllDay,
            RecurrenceRule = NullIfEmpty(request.RecurrenceRule),
            Color = NullIfEmpty(request.Color),
            Url = NullIfEmpty(request.Url),
            Attendees = request.Attendees.Select(a => new EventAttendeeDto
            {
                UserId = Guid.TryParse(a.UserId, out var aUserId) ? aUserId : null,
                Email = a.Email,
                DisplayName = NullIfEmpty(a.DisplayName),
                Role = Enum.TryParse<AttendeeRole>(a.Role, true, out var role) ? role : AttendeeRole.Required,
                Status = Enum.TryParse<AttendeeStatus>(a.Status, true, out var status) ? status : AttendeeStatus.NeedsAction
            }).ToList(),
            Reminders = request.Reminders.Select(r => new EventReminderDto
            {
                Method = Enum.TryParse<ReminderMethod>(r.Method, true, out var method) ? method : ReminderMethod.Notification,
                MinutesBefore = r.MinutesBefore
            }).ToList()
        };

        try
        {
            var result = await _eventService.CreateEventAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new EventResponse { Success = true, Event = ToEventMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new EventResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<EventResponse> GetEvent(
        GetEventRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new EventResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var result = await _eventService.GetEventAsync(
            eventId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

        return result is null
            ? new EventResponse { Success = false, ErrorMessage = "Event not found." }
            : new EventResponse { Success = true, Event = ToEventMessage(result) };
    }

    /// <inheritdoc />
    public override async Task<ListEventsResponse> ListEvents(
        ListEventsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CalendarId, out var calendarId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new ListEventsResponse { Success = false, ErrorMessage = "Invalid ID format." };

        DateTime? from = DateTime.TryParse(request.FromUtc, CultureInfo.InvariantCulture, out var f) ? f : null;
        DateTime? to = DateTime.TryParse(request.ToUtc, CultureInfo.InvariantCulture, out var t) ? t : null;
        var take = request.Take > 0 ? request.Take : 50;

        var results = await _eventService.ListEventsAsync(
            calendarId,
            new CallerContext(userId, ["user"], CallerType.User),
            from, to, request.Skip, take,
            context.CancellationToken);

        var response = new ListEventsResponse { Success = true };
        response.Events.AddRange(results.Select(ToEventMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<EventResponse> UpdateEvent(
        UpdateEventRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new EventResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var dto = new UpdateCalendarEventDto
        {
            Title = NullIfEmpty(request.Title),
            Description = NullIfEmpty(request.Description),
            Location = NullIfEmpty(request.Location),
            StartUtc = DateTime.TryParse(request.StartUtc, CultureInfo.InvariantCulture, out var startUtc) ? startUtc : null,
            EndUtc = DateTime.TryParse(request.EndUtc, CultureInfo.InvariantCulture, out var endUtc) ? endUtc : null,
            IsAllDay = bool.TryParse(request.IsAllDay, out var isAllDay) ? isAllDay : null,
            Status = Enum.TryParse<CalendarEventStatus>(request.Status, true, out var status) ? status : null,
            RecurrenceRule = NullIfEmpty(request.RecurrenceRule),
            Color = NullIfEmpty(request.Color),
            Url = NullIfEmpty(request.Url)
        };

        try
        {
            var result = await _eventService.UpdateEventAsync(
                eventId, dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new EventResponse { Success = true, Event = ToEventMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new EventResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<DeleteEventResponse> DeleteEvent(
        DeleteEventRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new DeleteEventResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            await _eventService.DeleteEventAsync(
                eventId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new DeleteEventResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new DeleteEventResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<EventResponse> Rsvp(
        RsvpRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new EventResponse { Success = false, ErrorMessage = "Invalid ID format." };

        if (!Enum.TryParse<AttendeeStatus>(request.Status, true, out var rsvpStatus))
            return new EventResponse { Success = false, ErrorMessage = "Invalid RSVP status." };

        var dto = new EventRsvpDto
        {
            Status = rsvpStatus,
            Comment = NullIfEmpty(request.Comment)
        };

        try
        {
            var result = await _eventService.RsvpAsync(
                eventId, dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new EventResponse { Success = true, Event = ToEventMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new EventResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ExportICalResponse> ExportEventICal(
        ExportEventICalRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EventId, out var eventId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new ExportICalResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            var ical = await _icalService.ExportEventAsync(
                eventId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new ExportICalResponse { Success = true, IcalText = ical };
        }
        catch (Exception ex) when (ex is Core.Errors.ValidationException)
        {
            return new ExportICalResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ImportICalResponse> ImportICal(
        Protos.ImportICalRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) ||
            !Guid.TryParse(request.CalendarId, out var calendarId))
            return new ImportICalResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            var results = await _icalService.ImportEventsAsync(
                calendarId, request.IcalText,
                new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

            var response = new ImportICalResponse { Success = true };
            response.CreatedEventIds.AddRange(results.Select(r => r.Id.ToString()));
            return response;
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ImportICalResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static CalendarMessage ToCalendarMessage(CalendarDto dto)
    {
        return new CalendarMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? "",
            Color = dto.Color ?? "",
            Timezone = dto.Timezone,
            IsDefault = dto.IsDefault,
            IsVisible = dto.IsVisible,
            SyncToken = dto.SyncToken ?? "",
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
    }

    private static EventMessage ToEventMessage(CalendarEventDto dto)
    {
        var msg = new EventMessage
        {
            Id = dto.Id.ToString(),
            CalendarId = dto.CalendarId.ToString(),
            CreatedByUserId = dto.CreatedByUserId.ToString(),
            Title = dto.Title,
            Description = dto.Description ?? "",
            Location = dto.Location ?? "",
            StartUtc = dto.StartUtc.ToString("O"),
            EndUtc = dto.EndUtc.ToString("O"),
            IsAllDay = dto.IsAllDay,
            Status = dto.Status.ToString(),
            RecurrenceRule = dto.RecurrenceRule ?? "",
            RecurringEventId = dto.RecurringEventId?.ToString() ?? "",
            OriginalStartUtc = dto.OriginalStartUtc?.ToString("O") ?? "",
            Color = dto.Color ?? "",
            Url = dto.Url ?? "",
            Etag = dto.ETag ?? "",
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
        msg.Attendees.AddRange(dto.Attendees.Select(a => new AttendeeMessage
        {
            UserId = a.UserId?.ToString() ?? "",
            Email = a.Email,
            DisplayName = a.DisplayName ?? "",
            Role = a.Role.ToString(),
            Status = a.Status.ToString()
        }));
        msg.Reminders.AddRange(dto.Reminders.Select(r => new ReminderMessage
        {
            Method = r.Method.ToString(),
            MinutesBefore = r.MinutesBefore
        }));
        return msg;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
