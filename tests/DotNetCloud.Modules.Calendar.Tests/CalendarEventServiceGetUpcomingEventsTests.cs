using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="CalendarEventService.GetUpcomingEventsAsync"/>.
/// </summary>
[TestClass]
public class CalendarEventServiceGetUpcomingEventsTests
{
    private CalendarDbContext _db = null!;
    private CalendarService _calendarService = null!;
    private CalendarEventService _eventService = null!;
    private CalendarShareService _shareService = null!;
    private CallerContext _userA = null!;
    private CallerContext _userB = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        var eventBusMock = new Mock<IEventBus>();
        _calendarService = new CalendarService(_db, eventBusMock.Object, Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(), Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(), NullLogger<CalendarService>.Instance);
        _eventService = new CalendarEventService(
            _db,
            eventBusMock.Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(),
            new OccurrenceExpansionService(
                _db,
                new RecurrenceEngine(NullLogger<RecurrenceEngine>.Instance),
                NullLogger<OccurrenceExpansionService>.Instance),
            NullLogger<CalendarEventService>.Instance);
        _shareService = new CalendarShareService(_db, eventBusMock.Object, NullLogger<CalendarShareService>.Instance);
        _userA = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        _userB = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task CreateEventAsync(Guid calendarId, CallerContext creator, string title, DateTime startUtc)
    {
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calendarId,
            Title = title,
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1)
        }, creator);
    }

    [TestMethod]
    public async Task GetUpcomingEvents_OwnedAndSharedCalendars_AggregatesOrderedByStartAscending()
    {
        var calA = await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "A's Cal" }, _userA);
        var calB = await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "B's Cal" }, _userB);
        await _shareService.ShareCalendarAsync(calA.Id, _userB.UserId, null, CalendarSharePermission.ReadOnly, _userA);

        var baseTime = DateTime.UtcNow;
        await CreateEventAsync(calA.Id, _userA, "A1", baseTime.AddHours(1));
        await CreateEventAsync(calB.Id, _userB, "B1", baseTime.AddHours(2));
        await CreateEventAsync(calA.Id, _userA, "A2", baseTime.AddHours(3));

        var result = await _eventService.GetUpcomingEventsAsync(_userB, baseTime, baseTime.AddDays(1));

        CollectionAssert.AreEqual(
            new[] { "A1", "B1", "A2" },
            result.Select(e => e.Title).ToArray());
    }

    [TestMethod]
    public async Task GetUpcomingEvents_Window_FiltersToRequestedFromToRange()
    {
        var calB = await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "B's Cal" }, _userB);

        var baseTime = DateTime.UtcNow;
        await CreateEventAsync(calB.Id, _userB, "Before", baseTime.AddHours(-1));
        await CreateEventAsync(calB.Id, _userB, "Inside", baseTime.AddHours(1));
        await CreateEventAsync(calB.Id, _userB, "After", baseTime.AddDays(2));

        var result = await _eventService.GetUpcomingEventsAsync(_userB, baseTime, baseTime.AddDays(1));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Inside", result[0].Title);
    }

    [TestMethod]
    public async Task GetUpcomingEvents_RespectsCount_ReturnsOnlyRequestedNumberOfEarliest()
    {
        var calB = await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "B's Cal" }, _userB);

        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
            await CreateEventAsync(calB.Id, _userB, $"Event{i}", baseTime.AddHours(i + 1));

        var result = await _eventService.GetUpcomingEventsAsync(_userB, baseTime, baseTime.AddDays(1), count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "Event0", "Event1" },
            result.Select(e => e.Title).ToArray());
    }

    [TestMethod]
    public async Task GetUpcomingEvents_NoEvents_ReturnsEmptyList()
    {
        await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "B's Cal" }, _userB);
        var baseTime = DateTime.UtcNow;

        var result = await _eventService.GetUpcomingEventsAsync(_userB, baseTime, baseTime.AddDays(1));

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetUpcomingEvents_RecurringMasterWithOccurrenceInWindow_ReturnsOccurrence()
    {
        var calB = await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "B's Cal" }, _userB);
        var baseTime = DateTime.UtcNow;

        // A recurring daily event whose SERIES START predates the window, but which has
        // occurrences inside the window — must be discovered via recurrence expansion.
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = calB.Id,
            CreatedByUserId = _userB.UserId,
            Title = "Daily Standup",
            StartUtc = baseTime.AddDays(-3),
            EndUtc = baseTime.AddDays(-3).AddHours(1),
            RecurrenceRule = "FREQ=DAILY;INTERVAL=1"
        });
        await _db.SaveChangesAsync();

        var result = await _eventService.GetUpcomingEventsAsync(_userB, baseTime, baseTime.AddDays(1));

        Assert.IsTrue(
            result.Any(e => e.Title == "Daily Standup"),
            "Expected the recurring event's occurrence(s) inside the window to be returned.");
        Assert.IsTrue(result.All(e => e.StartUtc >= baseTime && e.StartUtc <= baseTime.AddDays(1)));
    }
}
