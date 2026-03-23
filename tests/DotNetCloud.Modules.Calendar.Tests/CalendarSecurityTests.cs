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
/// Security tests for the Calendar module: tenant isolation and authorization boundaries.
/// </summary>
[TestClass]
public class CalendarSecurityTests
{
    private CalendarDbContext _db = null!;
    private CalendarService _calendarService = null!;
    private CalendarEventService _eventService = null!;
    private CalendarShareService _shareService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _userA = null!;
    private CallerContext _userB = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _calendarService = new CalendarService(_db, _eventBusMock.Object, NullLogger<CalendarService>.Instance);
        _eventService = new CalendarEventService(_db, _eventBusMock.Object, NullLogger<CalendarEventService>.Instance);
        _shareService = new CalendarShareService(_db, _eventBusMock.Object, NullLogger<CalendarShareService>.Instance);
        _userA = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _userB = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Calendar Tenant Isolation ───────────────────────────────────

    [TestMethod]
    public async Task GetCalendar_OtherUser_ReturnsNull()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Private" }, _userA);

        var result = await _calendarService.GetCalendarAsync(calendar.Id, _userB);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListCalendars_OnlyReturnsOwnCalendars()
    {
        await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "A's Cal" }, _userA);
        await _calendarService.CreateCalendarAsync(new CreateCalendarDto { Name = "B's Cal" }, _userB);

        var calsA = await _calendarService.ListCalendarsAsync(_userA);
        var calsB = await _calendarService.ListCalendarsAsync(_userB);

        Assert.AreEqual(1, calsA.Count);
        Assert.AreEqual(1, calsB.Count);
    }

    [TestMethod]
    public async Task UpdateCalendar_OtherUser_Throws()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Original" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _calendarService.UpdateCalendarAsync(calendar.Id,
                new UpdateCalendarDto { Name = "Hijacked" }, _userB));
    }

    [TestMethod]
    public async Task DeleteCalendar_OtherUser_Throws()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Protected" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _calendarService.DeleteCalendarAsync(calendar.Id, _userB));
    }

    // ─── Event Tenant Isolation ──────────────────────────────────────

    [TestMethod]
    public async Task GetEvent_OtherUser_ReturnsNull()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Work" }, _userA);
        var evt = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calendar.Id,
            Title = "Secret Meeting",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        }, _userA);

        var result = await _eventService.GetEventAsync(evt.Id, _userB);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListEvents_OnlyReturnsOwnCalendarEvents()
    {
        var calA = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "A's Cal" }, _userA);
        var calB = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "B's Cal" }, _userB);

        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calA.Id, Title = "A's Event",
            StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1)
        }, _userA);
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calB.Id, Title = "B's Event",
            StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1)
        }, _userB);

        var eventsA = await _eventService.ListEventsAsync(calA.Id, _userA);
        var eventsFromB = await _eventService.ListEventsAsync(calA.Id, _userB);

        Assert.AreEqual(1, eventsA.Count);
        Assert.AreEqual(0, eventsFromB.Count);
    }

    [TestMethod]
    public async Task SearchEvents_OnlyReturnsOwnResults()
    {
        var calA = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "A" }, _userA);
        var calB = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "B" }, _userB);

        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calA.Id, Title = "Classified Alpha",
            StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1)
        }, _userA);
        await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calB.Id, Title = "Classified Beta",
            StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1)
        }, _userB);

        var resultsA = await _eventService.SearchEventsAsync(_userA, "Classified");
        var resultsB = await _eventService.SearchEventsAsync(_userB, "Classified");

        Assert.AreEqual(1, resultsA.Count);
        Assert.AreEqual(1, resultsB.Count);
        Assert.IsTrue(resultsA[0].Title.Contains("Alpha"));
        Assert.IsTrue(resultsB[0].Title.Contains("Beta"));
    }

    // ─── Share Authorization ─────────────────────────────────────────

    [TestMethod]
    public async Task ShareCalendar_NonOwner_Throws()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Guarded" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareCalendarAsync(
                calendar.Id, Guid.NewGuid(), null, CalendarSharePermission.ReadOnly, _userB));
    }

    [TestMethod]
    public async Task RemoveShare_NonOwner_Throws()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Shared" }, _userA);
        var share = await _shareService.ShareCalendarAsync(
            calendar.Id, _userB.UserId, null, CalendarSharePermission.ReadOnly, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.RemoveShareAsync(share.Id, _userB));
    }

    [TestMethod]
    public async Task ListShares_NonOwner_ReturnsEmpty()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Hidden" }, _userA);
        await _shareService.ShareCalendarAsync(
            calendar.Id, _userB.UserId, null, CalendarSharePermission.ReadOnly, _userA);

        var shares = await _shareService.ListSharesAsync(calendar.Id, _userB);

        Assert.AreEqual(0, shares.Count);
    }
}
