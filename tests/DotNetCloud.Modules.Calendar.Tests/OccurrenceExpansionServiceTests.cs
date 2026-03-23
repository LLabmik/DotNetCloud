using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="OccurrenceExpansionService"/> — recurring event expansion + merge.
/// </summary>
[TestClass]
public class OccurrenceExpansionServiceTests
{
    private CalendarDbContext _db = null!;
    private OccurrenceExpansionService _service = null!;
    private CallerContext _caller = null!;
    private Guid _userId;
    private Models.Calendar _calendar = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _userId = Guid.NewGuid();
        _caller = new CallerContext(_userId, ["user"], CallerType.User);

        var engine = new RecurrenceEngine(NullLogger<RecurrenceEngine>.Instance);
        _service = new OccurrenceExpansionService(_db, engine, NullLogger<OccurrenceExpansionService>.Instance);

        _calendar = new Models.Calendar
        {
            OwnerId = _userId,
            Name = "Test Calendar"
        };
        _db.Calendars.Add(_calendar);
        _db.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task ListExpanded_NonRecurringEvents_ReturnsAsIs()
    {
        var now = DateTime.UtcNow;
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Single Event",
            StartUtc = now.AddHours(1),
            EndUtc = now.AddHours(2)
        });
        await _db.SaveChangesAsync();

        var result = await _service.ListExpandedEventsAsync(
            _calendar.Id, _caller, now, now.AddDays(7));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Single Event", result[0].Title);
    }

    [TestMethod]
    public async Task ListExpanded_RecurringDaily_ExpandsOccurrences()
    {
        var startDate = new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Daily Standup",
            StartUtc = startDate,
            EndUtc = startDate.AddMinutes(30),
            RecurrenceRule = "FREQ=DAILY;COUNT=5"
        });
        await _db.SaveChangesAsync();

        var from = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 6, 7, 0, 0, 0, DateTimeKind.Utc);

        var result = await _service.ListExpandedEventsAsync(_calendar.Id, _caller, from, to);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual("Daily Standup", result[0].Title);
        Assert.AreEqual(new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc), result[0].StartUtc);
        Assert.AreEqual(new DateTime(2025, 6, 5, 9, 0, 0, DateTimeKind.Utc), result[4].StartUtc);
    }

    [TestMethod]
    public async Task ListExpanded_RecurringWithException_SkipsOverriddenOccurrence()
    {
        var startDate = new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var master = new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Daily Standup",
            StartUtc = startDate,
            EndUtc = startDate.AddMinutes(30),
            RecurrenceRule = "FREQ=DAILY;COUNT=5"
        };
        _db.CalendarEvents.Add(master);
        await _db.SaveChangesAsync();

        // Exception: replace June 3rd occurrence with a different time
        var exception = new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Moved Standup",
            StartUtc = new DateTime(2025, 6, 3, 14, 0, 0, DateTimeKind.Utc), // moved to 2pm
            EndUtc = new DateTime(2025, 6, 3, 14, 30, 0, DateTimeKind.Utc),
            RecurringEventId = master.Id,
            OriginalStartUtc = new DateTime(2025, 6, 3, 9, 0, 0, DateTimeKind.Utc)
        };
        _db.CalendarEvents.Add(exception);
        await _db.SaveChangesAsync();

        var from = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 6, 7, 0, 0, 0, DateTimeKind.Utc);

        var result = await _service.ListExpandedEventsAsync(_calendar.Id, _caller, from, to);

        // 4 expanded + 1 exception instance = 5
        Assert.AreEqual(5, result.Count);

        // Should contain the exception instance at 2pm
        var movedEvent = result.FirstOrDefault(e => e.Title == "Moved Standup");
        Assert.IsNotNull(movedEvent);
        Assert.AreEqual(14, movedEvent.StartUtc.Hour);

        // Should NOT have the original 9am occurrence on June 3rd from expansion
        var originalJune3 = result.Where(e => e.Title == "Daily Standup" &&
            e.StartUtc.Day == 3 && e.StartUtc.Hour == 9);
        Assert.AreEqual(0, originalJune3.Count());
    }

    [TestMethod]
    public async Task ListExpanded_MixedEventsAndRecurring_MergesAndSorts()
    {
        var date = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // A single event on June 2 at 11am
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "One-off Lunch",
            StartUtc = date.AddDays(1).AddHours(11),
            EndUtc = date.AddDays(1).AddHours(12)
        });

        // Daily recurring at 9am
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Standup",
            StartUtc = date.AddHours(9),
            EndUtc = date.AddHours(9).AddMinutes(30),
            RecurrenceRule = "FREQ=DAILY;COUNT=3"
        });

        await _db.SaveChangesAsync();

        var result = await _service.ListExpandedEventsAsync(
            _calendar.Id, _caller, date, date.AddDays(7));

        Assert.AreEqual(4, result.Count); // 3 standup + 1 lunch
        Assert.IsTrue(result[0].StartUtc <= result[1].StartUtc); // sorted
    }

    [TestMethod]
    public async Task ListExpanded_NoAccessToCalendar_ReturnsEmpty()
    {
        var otherUser = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Private",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        });
        await _db.SaveChangesAsync();

        var result = await _service.ListExpandedEventsAsync(
            _calendar.Id, otherUser, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListExpanded_Pagination_RespectsSkipAndTake()
    {
        var date = new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Daily",
            StartUtc = date,
            EndUtc = date.AddMinutes(30),
            RecurrenceRule = "FREQ=DAILY;COUNT=10"
        });
        await _db.SaveChangesAsync();

        var from = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var page = await _service.ListExpandedEventsAsync(
            _calendar.Id, _caller, from, to, skip: 2, take: 3);

        Assert.AreEqual(3, page.Count);
        // Third occurrence (0-indexed: 2) should be June 3
        Assert.AreEqual(3, page[0].StartUtc.Day);
    }

    [TestMethod]
    public async Task SearchExpanded_ByQuery_FiltersResults()
    {
        var date = new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Team Standup",
            StartUtc = date,
            EndUtc = date.AddMinutes(30),
            RecurrenceRule = "FREQ=DAILY;COUNT=3"
        });
        _db.CalendarEvents.Add(new CalendarEvent
        {
            CalendarId = _calendar.Id,
            CreatedByUserId = _userId,
            Title = "Lunch Break",
            StartUtc = date.AddHours(3),
            EndUtc = date.AddHours(4)
        });
        await _db.SaveChangesAsync();

        var result = await _service.SearchExpandedEventsAsync(
            _caller, query: "Standup",
            from: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2025, 6, 7, 0, 0, 0, DateTimeKind.Utc));

        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.All(e => e.Title == "Team Standup"));
    }
}
