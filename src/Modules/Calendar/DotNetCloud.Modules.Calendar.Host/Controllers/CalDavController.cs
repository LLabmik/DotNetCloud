using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using System.Text;

namespace DotNetCloud.Modules.Calendar.Host.Controllers;

/// <summary>
/// CalDAV endpoint for calendar discovery, iCalendar get/put/delete, and sync-token based change tracking.
/// Implements a subset of RFC 4791 (CalDAV) and RFC 6764 (CalDAV discovery).
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class CalDavController : ControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventService _eventService;
    private readonly IICalendarService _icalService;
    private readonly ILogger<CalDavController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalDavController"/> class.
    /// </summary>
    public CalDavController(
        ICalendarService calendarService,
        ICalendarEventService eventService,
        IICalendarService icalService,
        ILogger<CalDavController> logger)
    {
        _calendarService = calendarService;
        _eventService = eventService;
        _icalService = icalService;
        _logger = logger;
    }

    /// <summary>
    /// RFC 6764 well-known CalDAV discovery endpoint.
    /// </summary>
    [HttpOptions("/.well-known/caldav")]
    [AllowAnonymous]
    public IActionResult WellKnown()
    {
        Response.Headers["DAV"] = "1, calendar-access";
        Response.Headers.Allow = "OPTIONS, PROPFIND, REPORT, GET, PUT, DELETE";
        return Ok();
    }

    /// <summary>
    /// PROPFIND on a user's calendar collection. Returns available calendars.
    /// </summary>
    [HttpMethod("PROPFIND", "/caldav/{userId}/calendars")]
    [Route("/caldav/{userId}/calendars")]
    public async Task<IActionResult> PropFindCalendars(string userId)
    {
        var caller = GetCaller();
        var calendars = await _calendarService.ListCalendarsAsync(caller);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:caldav\">");

        foreach (var cal in calendars)
        {
            sb.AppendLine("  <D:response>");
            sb.AppendLine($"    <D:href>/caldav/{userId}/calendars/{cal.Id}/</D:href>");
            sb.AppendLine("    <D:propstat>");
            sb.AppendLine("      <D:prop>");
            sb.AppendLine($"        <D:displayname>{System.Security.SecurityElement.Escape(cal.Name)}</D:displayname>");
            sb.AppendLine("        <D:resourcetype><D:collection/><C:calendar/></D:resourcetype>");
            if (cal.Color is not null)
                sb.AppendLine($"        <A:calendar-color xmlns:A=\"http://apple.com/ns/ical/\">{cal.Color}</A:calendar-color>");
            if (cal.Description is not null)
                sb.AppendLine($"        <C:calendar-description>{System.Security.SecurityElement.Escape(cal.Description)}</C:calendar-description>");
            sb.AppendLine($"        <D:getctag>{cal.SyncToken}</D:getctag>");
            sb.AppendLine("      </D:prop>");
            sb.AppendLine("      <D:status>HTTP/1.1 200 OK</D:status>");
            sb.AppendLine("    </D:propstat>");
            sb.AppendLine("  </D:response>");
        }

        sb.AppendLine("</D:multistatus>");

        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }

    /// <summary>
    /// PROPFIND on a specific calendar. Returns events in the calendar.
    /// </summary>
    [HttpMethod("PROPFIND", "/caldav/{userId}/calendars/{calendarId}")]
    [Route("/caldav/{userId}/calendars/{calendarId}")]
    public async Task<IActionResult> PropFindCalendarEvents(string userId, Guid calendarId)
    {
        var caller = GetCaller();
        var events = await _eventService.ListEventsAsync(calendarId, caller, take: 1000);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:caldav\">");

        foreach (var evt in events)
        {
            sb.AppendLine("  <D:response>");
            sb.AppendLine($"    <D:href>/caldav/{userId}/calendars/{calendarId}/{evt.Id}.ics</D:href>");
            sb.AppendLine("    <D:propstat>");
            sb.AppendLine("      <D:prop>");
            sb.AppendLine($"        <D:getetag>\"{evt.ETag}\"</D:getetag>");
            sb.AppendLine("        <D:getcontenttype>text/calendar; charset=utf-8</D:getcontenttype>");
            sb.AppendLine("      </D:prop>");
            sb.AppendLine("      <D:status>HTTP/1.1 200 OK</D:status>");
            sb.AppendLine("    </D:propstat>");
            sb.AppendLine("  </D:response>");
        }

        sb.AppendLine("</D:multistatus>");

        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }

    /// <summary>
    /// GET a specific event as iCalendar text.
    /// </summary>
    [HttpGet("/caldav/{userId}/calendars/{calendarId}/{eventId}.ics")]
    public async Task<IActionResult> GetEvent(string userId, Guid calendarId, Guid eventId)
    {
        var caller = GetCaller();

        try
        {
            var ical = await _icalService.ExportEventAsync(eventId, caller);
            return Content(ical, "text/calendar; charset=utf-8");
        }
        catch (Core.Errors.ValidationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// PUT a new or updated event via iCalendar text.
    /// </summary>
    [HttpPut("/caldav/{userId}/calendars/{calendarId}/{eventId}.ics")]
    public async Task<IActionResult> PutEvent(string userId, Guid calendarId, Guid eventId)
    {
        var caller = GetCaller();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var icalText = await reader.ReadToEndAsync();

        try
        {
            var imported = await _icalService.ImportEventsAsync(calendarId, icalText, caller);
            if (imported.Count > 0)
            {
                Response.Headers.ETag = $"\"{imported[0].ETag}\"";
                return Created($"/caldav/{userId}/calendars/{calendarId}/{imported[0].Id}.ics", null);
            }
            return BadRequest();
        }
        catch (Core.Errors.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// DELETE a specific event.
    /// </summary>
    [HttpDelete("/caldav/{userId}/calendars/{calendarId}/{eventId}.ics")]
    public async Task<IActionResult> DeleteEvent(string userId, Guid calendarId, Guid eventId)
    {
        var caller = GetCaller();

        try
        {
            await _eventService.DeleteEventAsync(eventId, caller);
            return NoContent();
        }
        catch (Core.Errors.ValidationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// REPORT for sync-token based change tracking.
    /// </summary>
    [HttpMethod("REPORT", "/caldav/{userId}/calendars/{calendarId}")]
    public async Task<IActionResult> Report(string userId, Guid calendarId)
    {
        var caller = GetCaller();
        var calendar = await _calendarService.GetCalendarAsync(calendarId, caller);

        if (calendar is null) return NotFound();

        var events = await _eventService.ListEventsAsync(calendarId, caller, take: 1000);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:caldav\">");

        foreach (var evt in events)
        {
            sb.AppendLine("  <D:response>");
            sb.AppendLine($"    <D:href>/caldav/{userId}/calendars/{calendarId}/{evt.Id}.ics</D:href>");
            sb.AppendLine("    <D:propstat>");
            sb.AppendLine("      <D:prop>");
            sb.AppendLine($"        <D:getetag>\"{evt.ETag}\"</D:getetag>");
            sb.AppendLine("      </D:prop>");
            sb.AppendLine("      <D:status>HTTP/1.1 200 OK</D:status>");
            sb.AppendLine("    </D:propstat>");
            sb.AppendLine("  </D:response>");
        }

        sb.AppendLine($"  <D:sync-token>{calendar.SyncToken}</D:sync-token>");
        sb.AppendLine("</D:multistatus>");

        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }

    private CallerContext GetCaller()
    {
        if (User?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claimValue, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CallerContext(userId, roles, CallerType.User);
    }
}

/// <summary>
/// Custom HTTP method attribute for WebDAV methods (PROPFIND, REPORT, etc.).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class HttpMethodAttribute : Attribute, IActionHttpMethodProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpMethodAttribute"/> class.
    /// </summary>
    public HttpMethodAttribute(string method, string? template = null)
    {
        HttpMethods = [method];
        Template = template;
    }

    /// <inheritdoc />
    public IEnumerable<string> HttpMethods { get; }

    /// <summary>Gets the route template.</summary>
    public string? Template { get; }
}
