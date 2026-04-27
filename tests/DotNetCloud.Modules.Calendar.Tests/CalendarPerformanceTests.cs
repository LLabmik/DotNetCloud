using System.Diagnostics;
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
/// Performance baseline tests for the Calendar module.
/// Establishes timing thresholds for event creation, listing, and search at scale.
/// </summary>
[TestClass]
public class CalendarPerformanceTests
{
    private CalendarDbContext _db = null!;
    private CalendarEventService _eventService = null!;
    private CalendarService _calendarService = null!;
    private ICalService _icalService = null!;
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
            new CreateCalendarDto { Name = "Perf Test" }, _caller);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateEvents_200Records_CompletesWithinThreshold()
    {
        var baseStart = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 200; i++)
        {
            await _eventService.CreateEventAsync(new CreateCalendarEventDto
            {
                CalendarId = _calendar.Id,
                Title = $"Event {i}",
                StartUtc = baseStart.AddDays(i),
                EndUtc = baseStart.AddDays(i).AddHours(1)
            }, _caller);
        }

        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 30_000,
            $"Creating 200 events took {sw.ElapsedMilliseconds}ms, expected < 30000ms");
    }

    [TestMethod]
    public async Task ListEvents_LargeCalendar_CompletesWithinThreshold()
    {
        var baseStart = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 200; i++)
        {
            await _eventService.CreateEventAsync(new CreateCalendarEventDto
            {
                CalendarId = _calendar.Id,
                Title = $"Event {i}",
                StartUtc = baseStart.AddDays(i),
                EndUtc = baseStart.AddDays(i).AddHours(1)
            }, _caller);
        }

        var sw = Stopwatch.StartNew();

        var events = await _eventService.ListEventsAsync(_calendar.Id, _caller, take: 200);

        sw.Stop();
        Assert.AreEqual(200, events.Count);
        Assert.IsTrue(sw.ElapsedMilliseconds < 5_000,
            $"Listing 200 events took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    [TestMethod]
    public async Task SearchEvents_LargeCalendar_CompletesWithinThreshold()
    {
        var baseStart = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 200; i++)
        {
            await _eventService.CreateEventAsync(new CreateCalendarEventDto
            {
                CalendarId = _calendar.Id,
                Title = i % 10 == 0 ? $"Standup {i}" : $"Meeting {i}",
                StartUtc = baseStart.AddDays(i),
                EndUtc = baseStart.AddDays(i).AddHours(1)
            }, _caller);
        }

        var sw = Stopwatch.StartNew();

        var results = await _eventService.SearchEventsAsync(_caller, "Standup");

        sw.Stop();
        Assert.AreEqual(20, results.Count);
        Assert.IsTrue(sw.ElapsedMilliseconds < 5_000,
            $"Searching 200 events took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    [TestMethod]
    public async Task ExportCalendar_200Events_CompletesWithinThreshold()
    {
        var baseStart = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 200; i++)
        {
            await _eventService.CreateEventAsync(new CreateCalendarEventDto
            {
                CalendarId = _calendar.Id,
                Title = $"Export Event {i}",
                StartUtc = baseStart.AddDays(i),
                EndUtc = baseStart.AddDays(i).AddHours(1)
            }, _caller);
        }

        var sw = Stopwatch.StartNew();

        var ical = await _icalService.ExportCalendarAsync(_calendar.Id, _caller);

        sw.Stop();
        Assert.IsTrue(ical.Contains("BEGIN:VEVENT"));
        Assert.IsTrue(sw.ElapsedMilliseconds < 10_000,
            $"Exporting 200 events took {sw.ElapsedMilliseconds}ms, expected < 10000ms");
    }
}
