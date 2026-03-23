using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="ICalService"/> (iCalendar import/export).
/// </summary>
[TestClass]
public class ICalServiceTests
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
        _eventService = new CalendarEventService(_db, _eventBusMock.Object, NullLogger<CalendarEventService>.Instance);
        _calendarService = new CalendarService(_db, _eventBusMock.Object, NullLogger<CalendarService>.Instance);
        _icalService = new ICalService(_db, _eventService, NullLogger<ICalService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "ICal Test" }, _caller);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task ExportEvent_ReturnsValidICal()
    {
        var created = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Exported Meeting",
            StartUtc = new DateTime(2025, 6, 15, 14, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2025, 6, 15, 15, 0, 0, DateTimeKind.Utc)
        }, _caller);

        var ical = await _icalService.ExportEventAsync(created.Id, _caller);

        Assert.IsTrue(ical.Contains("BEGIN:VCALENDAR"));
        Assert.IsTrue(ical.Contains("BEGIN:VEVENT"));
        Assert.IsTrue(ical.Contains("SUMMARY:Exported Meeting"));
        Assert.IsTrue(ical.Contains("END:VEVENT"));
        Assert.IsTrue(ical.Contains("END:VCALENDAR"));
    }

    [TestMethod]
    public async Task ExportCalendar_ReturnsAllEvents()
    {
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Event One",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        }, _caller);
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Event Two",
            StartUtc = DateTime.UtcNow.AddHours(3),
            EndUtc = DateTime.UtcNow.AddHours(4)
        }, _caller);

        var ical = await _icalService.ExportCalendarAsync(_calendar.Id, _caller);

        Assert.IsTrue(ical.Contains("SUMMARY:Event One"));
        Assert.IsTrue(ical.Contains("SUMMARY:Event Two"));
        Assert.IsTrue(ical.Contains("X-WR-CALNAME:ICal Test"));
    }

    [TestMethod]
    public async Task ImportEvents_CreatesEvents()
    {
        var icalText = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            SUMMARY:Imported Meeting
            DTSTART:20250720T100000Z
            DTEND:20250720T110000Z
            DESCRIPTION:Test import
            LOCATION:Room 101
            END:VEVENT
            END:VCALENDAR
            """;

        var results = await _icalService.ImportEventsAsync(_calendar.Id, icalText, _caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Imported Meeting", results[0].Title);
    }

    [TestMethod]
    public async Task ImportEvents_MultipleEvents_ImportsAll()
    {
        var icalText = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            SUMMARY:First
            DTSTART:20250720T100000Z
            DTEND:20250720T110000Z
            END:VEVENT
            BEGIN:VEVENT
            SUMMARY:Second
            DTSTART:20250721T100000Z
            DTEND:20250721T110000Z
            END:VEVENT
            END:VCALENDAR
            """;

        var results = await _icalService.ImportEventsAsync(_calendar.Id, icalText, _caller);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task ExportEvent_WithAttendees_IncludesAttendeeLines()
    {
        var created = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "With Attendees",
            StartUtc = new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2025, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            Attendees =
            [
                new EventAttendeeDto { Email = "alice@example.com", DisplayName = "Alice", Role = AttendeeRole.Required }
            ]
        }, _caller);

        var ical = await _icalService.ExportEventAsync(created.Id, _caller);

        Assert.IsTrue(ical.Contains("ATTENDEE;"));
        Assert.IsTrue(ical.Contains("alice@example.com"));
    }

    [TestMethod]
    public async Task ExportEvent_WithReminders_IncludesValarm()
    {
        var created = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "With Alarm",
            StartUtc = new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2025, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            Reminders =
            [
                new EventReminderDto { MinutesBefore = 30, Method = ReminderMethod.Notification }
            ]
        }, _caller);

        var ical = await _icalService.ExportEventAsync(created.Id, _caller);

        Assert.IsTrue(ical.Contains("BEGIN:VALARM"));
        Assert.IsTrue(ical.Contains("TRIGGER:-PT30M"));
        Assert.IsTrue(ical.Contains("END:VALARM"));
    }

    [TestMethod]
    public async Task ImportEvents_EmptyText_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _icalService.ImportEventsAsync(_calendar.Id, "", _caller));
    }
}
