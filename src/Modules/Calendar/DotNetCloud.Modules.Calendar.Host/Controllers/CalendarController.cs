using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Calendar.Host.Controllers;

/// <summary>
/// REST API controller for calendar and event CRUD, RSVP, search, sharing, and iCalendar import/export.
/// </summary>
[Route("api/v1/calendars")]
public class CalendarController : CalendarControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventService _eventService;
    private readonly ICalendarShareService _shareService;
    private readonly IICalendarService _icalService;
    private readonly ILogger<CalendarController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarController"/> class.
    /// </summary>
    public CalendarController(
        ICalendarService calendarService,
        ICalendarEventService eventService,
        ICalendarShareService shareService,
        IICalendarService icalService,
        ILogger<CalendarController> logger)
    {
        _calendarService = calendarService;
        _eventService = eventService;
        _shareService = shareService;
        _icalService = icalService;
        _logger = logger;
    }

    // ─── Calendar CRUD ────────────────────────────────────────────────────

    /// <summary>Lists calendars for the authenticated user. Optionally filter by organization.</summary>
    [HttpGet]
    public async Task<IActionResult> ListCalendarsAsync([FromQuery] Guid? organizationId = null)
    {
        var caller = GetAuthenticatedCaller();
        var calendars = await _calendarService.ListCalendarsAsync(caller);

        // Filter by organization if requested
        if (organizationId.HasValue)
        {
            calendars = calendars.Where(c => c.OrganizationId == organizationId.Value).ToList();
        }

        return Ok(Envelope(calendars));
    }

    /// <summary>Gets a calendar by ID.</summary>
    [HttpGet("{calendarId:guid}")]
    public async Task<IActionResult> GetCalendarAsync(Guid calendarId)
    {
        var caller = GetAuthenticatedCaller();
        var calendar = await _calendarService.GetCalendarAsync(calendarId, caller);
        return calendar is null
            ? NotFound(ErrorEnvelope(ErrorCodes.CalendarNotFound, "Calendar not found."))
            : Ok(Envelope(calendar));
    }

    /// <summary>Creates a new calendar.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateCalendarAsync([FromBody] CreateCalendarDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var calendar = await _calendarService.CreateCalendarAsync(dto, caller);
        return Created($"/api/v1/calendars/{calendar.Id}", Envelope(calendar));
    }

    /// <summary>Updates an existing calendar.</summary>
    [HttpPut("{calendarId:guid}")]
    public async Task<IActionResult> UpdateCalendarAsync(Guid calendarId, [FromBody] UpdateCalendarDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var calendar = await _calendarService.UpdateCalendarAsync(calendarId, dto, caller);
            return Ok(Envelope(calendar));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.CalendarNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Soft-deletes a calendar.</summary>
    [HttpDelete("{calendarId:guid}")]
    public async Task<IActionResult> DeleteCalendarAsync(Guid calendarId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _calendarService.DeleteCalendarAsync(calendarId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Event CRUD ───────────────────────────────────────────────────────

    /// <summary>Lists events for a calendar with optional date range filter.</summary>
    [HttpGet("{calendarId:guid}/events")]
    public async Task<IActionResult> ListEventsAsync(
        Guid calendarId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var events = await _eventService.ListEventsAsync(calendarId, caller, from, to, skip, take);
        return Ok(Envelope(events));
    }

    /// <summary>Gets an event by ID.</summary>
    [HttpGet("events/{eventId:guid}")]
    public async Task<IActionResult> GetEventAsync(Guid eventId)
    {
        var caller = GetAuthenticatedCaller();
        var calendarEvent = await _eventService.GetEventAsync(eventId, caller);
        return calendarEvent is null
            ? NotFound(ErrorEnvelope(ErrorCodes.CalendarEventNotFound, "Event not found."))
            : Ok(Envelope(calendarEvent));
    }

    /// <summary>Creates a new calendar event.</summary>
    [HttpPost("events")]
    public async Task<IActionResult> CreateEventAsync([FromBody] CreateCalendarEventDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var calendarEvent = await _eventService.CreateEventAsync(dto, caller);
            return Created($"/api/v1/calendars/events/{calendarEvent.Id}", Envelope(calendarEvent));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an existing calendar event.</summary>
    [HttpPut("events/{eventId:guid}")]
    public async Task<IActionResult> UpdateEventAsync(Guid eventId, [FromBody] UpdateCalendarEventDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var calendarEvent = await _eventService.UpdateEventAsync(eventId, dto, caller);
            return Ok(Envelope(calendarEvent));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.CalendarEventNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Soft-deletes a calendar event.</summary>
    [HttpDelete("events/{eventId:guid}")]
    public async Task<IActionResult> DeleteEventAsync(Guid eventId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _eventService.DeleteEventAsync(eventId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── RSVP ─────────────────────────────────────────────────────────────

    /// <summary>Responds to an event invitation (RSVP).</summary>
    [HttpPost("events/{eventId:guid}/rsvp")]
    public async Task<IActionResult> RsvpAsync(Guid eventId, [FromBody] EventRsvpDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var calendarEvent = await _eventService.RsvpAsync(eventId, dto, caller);
            return Ok(Envelope(calendarEvent));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.CalendarEventNotFound || ex.ErrorCode == ErrorCodes.AttendeeNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Search ───────────────────────────────────────────────────────────

    /// <summary>Searches events across all of the user's calendars.</summary>
    [HttpGet("events/search")]
    public async Task<IActionResult> SearchEventsAsync(
        [FromQuery] string? q = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var events = await _eventService.SearchEventsAsync(caller, q, from, to, skip, take);
        return Ok(Envelope(events));
    }

    // ─── Sharing ──────────────────────────────────────────────────────────

    /// <summary>Lists shares for a calendar.</summary>
    [HttpGet("{calendarId:guid}/shares")]
    public async Task<IActionResult> ListSharesAsync(Guid calendarId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var shares = await _shareService.ListSharesAsync(calendarId, caller);
            return Ok(Envelope(shares));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Shares a calendar with a user or team.</summary>
    [HttpPost("{calendarId:guid}/shares")]
    public async Task<IActionResult> ShareCalendarAsync(Guid calendarId, [FromBody] ShareCalendarRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var share = await _shareService.ShareCalendarAsync(
                calendarId, request.UserId, request.TeamId, request.Permission, caller);
            return Created($"/api/v1/calendars/{calendarId}/shares", Envelope(share));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.CalendarNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a calendar share.</summary>
    [HttpDelete("shares/{shareId:guid}")]
    public async Task<IActionResult> RemoveShareAsync(Guid shareId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _shareService.RemoveShareAsync(shareId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── iCalendar Import/Export ──────────────────────────────────────────

    /// <summary>Exports a single event as iCalendar text.</summary>
    [HttpGet("events/{eventId:guid}/ical")]
    public async Task<IActionResult> ExportEventICalAsync(Guid eventId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var ical = await _icalService.ExportEventAsync(eventId, caller);
            return Content(ical, "text/calendar");
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Exports all events in a calendar as iCalendar text.</summary>
    [HttpGet("{calendarId:guid}/export")]
    public async Task<IActionResult> ExportCalendarICalAsync(Guid calendarId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var ical = await _icalService.ExportCalendarAsync(calendarId, caller);
            return Content(ical, "text/calendar");
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Imports events from iCalendar text into a calendar.</summary>
    [HttpPost("{calendarId:guid}/import")]
    public async Task<IActionResult> ImportICalAsync(Guid calendarId, [FromBody] ImportICalRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var events = await _icalService.ImportEventsAsync(calendarId, request.ICalText, caller);
            return Ok(Envelope(new { imported = events.Count, events }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}

/// <summary>Request body for sharing a calendar.</summary>
public sealed record ShareCalendarRequest
{
    /// <summary>User ID to share with (null if sharing with a team).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Team ID to share with (null if sharing with a user).</summary>
    public Guid? TeamId { get; init; }

    /// <summary>Permission level to grant.</summary>
    public CalendarSharePermission Permission { get; init; }
}

/// <summary>Request body for importing iCalendar data.</summary>
public sealed record ImportICalRequest
{
    /// <summary>The iCalendar text to import.</summary>
    public required string ICalText { get; init; }
}
