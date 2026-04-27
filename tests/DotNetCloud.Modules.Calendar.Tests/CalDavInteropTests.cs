using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// CalDAV interoperability tests validating iCalendar output format and RFC 5545 compliance.
/// Ensures compatibility with common CalDAV clients (Thunderbird, DAVx5, iOS/macOS Calendar).
/// </summary>
[TestClass]
public class CalDavInteropTests
{
    private CalendarDbContext _db = null!;
    private ICalService _icalService = null!;
    private CalendarEventService _eventService = null!;
    private CalendarService _calendarService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;
    private CalendarDto _calendar = null!;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _eventService = new CalendarEventService(_db, _eventBusMock.Object, Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(), NullLogger<CalendarEventService>.Instance);
        _calendarService = new CalendarService(_db, _eventBusMock.Object, Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(), NullLogger<CalendarService>.Instance);
        _icalService = new ICalService(_db, _eventService, NullLogger<ICalService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "CalDAV Test" }, _caller);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── RFC 5545 Output Compliance ──────────────────────────────────

    [TestMethod]
    public async Task ExportEvent_ContainsVCalendarWrapper()
    {
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Wrapper Test",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        }, _caller);

        var ical = await _icalService.ExportEventAsync(evt.Id, _caller);

        Assert.IsTrue(ical.Contains("BEGIN:VCALENDAR"), "Missing BEGIN:VCALENDAR");
        Assert.IsTrue(ical.Contains("END:VCALENDAR"), "Missing END:VCALENDAR");
        Assert.IsTrue(ical.Contains("BEGIN:VEVENT"), "Missing BEGIN:VEVENT");
        Assert.IsTrue(ical.Contains("END:VEVENT"), "Missing END:VEVENT");
    }

    [TestMethod]
    public async Task ExportEvent_ContainsVersion2()
    {
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Version Test",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        }, _caller);

        var ical = await _icalService.ExportEventAsync(evt.Id, _caller);

        Assert.IsTrue(ical.Contains("VERSION:2.0"), "iCalendar must declare VERSION:2.0");
    }

    [TestMethod]
    public async Task ExportEvent_ContainsSummary()
    {
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Team Standup",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        }, _caller);

        var ical = await _icalService.ExportEventAsync(evt.Id, _caller);

        Assert.IsTrue(ical.Contains("SUMMARY:Team Standup"), "SUMMARY must match event title");
    }

    [TestMethod]
    public async Task ExportEvent_ContainsDtStartAndDtEnd()
    {
        var start = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Timed Event",
            StartUtc = start,
            EndUtc = end
        }, _caller);

        var ical = await _icalService.ExportEventAsync(evt.Id, _caller);

        Assert.IsTrue(ical.Contains("DTSTART"), "Must contain DTSTART");
        Assert.IsTrue(ical.Contains("DTEND"), "Must contain DTEND");
    }

    [TestMethod]
    public async Task ExportEvent_WithDescription_ContainsDescription()
    {
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Described Event",
            Description = "This is a detailed description of the event.",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        }, _caller);

        var ical = await _icalService.ExportEventAsync(evt.Id, _caller);

        Assert.IsTrue(ical.Contains("DESCRIPTION:"), "DESCRIPTION field must be present");
    }

    [TestMethod]
    public async Task ExportEvent_WithLocation_ContainsLocation()
    {
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Located Event",
            Location = "Conference Room B",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        }, _caller);

        var ical = await _icalService.ExportEventAsync(evt.Id, _caller);

        Assert.IsTrue(ical.Contains("LOCATION:Conference Room B"), "LOCATION must match event location");
    }

    // ─── Round-Trip Compliance ───────────────────────────────────────

    [TestMethod]
    public async Task ImportExport_RoundTrip_PreservesTitle()
    {
        var ical = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//EN\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:Round Trip Event\r\n" +
                   "DTSTART:20260615T100000Z\r\nDTEND:20260615T110000Z\r\n" +
                   "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var imported = await _icalService.ImportEventsAsync(_calendar.Id, ical, _caller);
        Assert.AreEqual(1, imported.Count);

        var exported = await _icalService.ExportEventAsync(imported[0].Id, _caller);
        Assert.IsTrue(exported.Contains("Round Trip Event"), "Title must survive round-trip");
    }

    [TestMethod]
    public async Task ImportExport_MultipleEvents_AllImported()
    {
        var ical = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//EN\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:Event One\r\nDTSTART:20260615T100000Z\r\nDTEND:20260615T110000Z\r\nEND:VEVENT\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:Event Two\r\nDTSTART:20260616T100000Z\r\nDTEND:20260616T110000Z\r\nEND:VEVENT\r\n" +
                   "END:VCALENDAR\r\n";

        var imported = await _icalService.ImportEventsAsync(_calendar.Id, ical, _caller);

        Assert.AreEqual(2, imported.Count);
    }

    [TestMethod]
    public async Task ExportCalendar_ReturnsAllEvents()
    {
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id, Title = "Event 1",
            StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1)
        }, _caller);
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id, Title = "Event 2",
            StartUtc = DateTime.UtcNow.AddDays(1), EndUtc = DateTime.UtcNow.AddDays(1).AddHours(1)
        }, _caller);

        var exported = await _icalService.ExportCalendarAsync(_calendar.Id, _caller);

        var count = 0;
        var idx = 0;
        while ((idx = exported.IndexOf("BEGIN:VEVENT", idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx++;
        }
        Assert.AreEqual(2, count, "Export should contain exactly 2 VEVENTs");
    }

    // ─── Thunderbird/DAVx5 Compatibility ─────────────────────────────

    [TestMethod]
    public async Task Import_WithTimezone_DoesNotThrow()
    {
        // Thunderbird exports VTIMEZONE blocks
        var ical = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Mozilla//EN\r\n" +
                   "BEGIN:VTIMEZONE\r\nTZID:America/New_York\r\n" +
                   "BEGIN:STANDARD\r\nDTSTART:19701101T020000\r\nTZOFFSETFROM:-0400\r\nTZOFFSETTO:-0500\r\nEND:STANDARD\r\n" +
                   "END:VTIMEZONE\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:Timezone Test\r\n" +
                   "DTSTART;TZID=America/New_York:20260615T100000\r\nDTEND;TZID=America/New_York:20260615T110000\r\n" +
                   "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var imported = await _icalService.ImportEventsAsync(_calendar.Id, ical, _caller);

        Assert.AreEqual(1, imported.Count);
        Assert.AreEqual("Timezone Test", imported[0].Title);
    }

    [TestMethod]
    public async Task Import_WithRecurrenceRule_DoesNotThrow()
    {
        var ical = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//EN\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:Weekly Standup\r\n" +
                   "DTSTART:20260615T090000Z\r\nDTEND:20260615T093000Z\r\n" +
                   "RRULE:FREQ=WEEKLY;BYDAY=MO;COUNT=52\r\n" +
                   "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var imported = await _icalService.ImportEventsAsync(_calendar.Id, ical, _caller);

        Assert.AreEqual(1, imported.Count);
    }

    [TestMethod]
    public async Task Import_WithAlarm_DoesNotThrow()
    {
        // CalDAV clients often include VALARM for reminders
        var ical = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//EN\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:Alarm Test\r\n" +
                   "DTSTART:20260615T090000Z\r\nDTEND:20260615T100000Z\r\n" +
                   "BEGIN:VALARM\r\nACTION:DISPLAY\r\nDESCRIPTION:Reminder\r\nTRIGGER:-PT15M\r\nEND:VALARM\r\n" +
                   "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var imported = await _icalService.ImportEventsAsync(_calendar.Id, ical, _caller);

        Assert.AreEqual(1, imported.Count);
    }

    [TestMethod]
    public async Task Import_AllDayEvent_DoesNotThrow()
    {
        // All-day events use DATE instead of DATE-TIME
        var ical = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//EN\r\n" +
                   "BEGIN:VEVENT\r\nSUMMARY:All Day\r\n" +
                   "DTSTART;VALUE=DATE:20260615\r\nDTEND;VALUE=DATE:20260616\r\n" +
                   "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var imported = await _icalService.ImportEventsAsync(_calendar.Id, ical, _caller);

        Assert.AreEqual(1, imported.Count);
    }
}
