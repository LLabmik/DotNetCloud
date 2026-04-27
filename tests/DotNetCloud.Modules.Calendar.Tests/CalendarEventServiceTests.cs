using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="CalendarEventService"/>.
/// </summary>
[TestClass]
public class CalendarEventServiceTests
{
    private CalendarDbContext _db = null!;
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
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Test Calendar" }, _caller);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private CreateCalendarEventDto MakeEventDto(string title = "Test Event")
    {
        return new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = title,
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        };
    }

    [TestMethod]
    public async Task CreateEvent_ValidDto_ReturnsEvent()
    {
        var dto = MakeEventDto("Team Meeting");

        var result = await _eventService.CreateEventAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Team Meeting", result.Title);
        Assert.AreEqual(_calendar.Id, result.CalendarId);
        Assert.AreEqual(_caller.UserId, result.CreatedByUserId);
    }

    [TestMethod]
    public async Task CreateEvent_WithAttendees_StoresAttendees()
    {
        var dto = new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "With Attendees",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2),
            Attendees =
            [
                new EventAttendeeDto { Email = "alice@example.com", DisplayName = "Alice" },
                new EventAttendeeDto { Email = "bob@example.com", DisplayName = "Bob", Role = AttendeeRole.Optional }
            ]
        };

        var result = await _eventService.CreateEventAsync(dto, _caller);

        Assert.AreEqual(2, result.Attendees.Count);
        Assert.IsTrue(result.Attendees.Any(a => a.Email == "alice@example.com"));
        Assert.IsTrue(result.Attendees.Any(a => a.Email == "bob@example.com" && a.Role == AttendeeRole.Optional));
    }

    [TestMethod]
    public async Task CreateEvent_WithReminders_StoresReminders()
    {
        var dto = new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "With Reminders",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2),
            Reminders =
            [
                new EventReminderDto { MinutesBefore = 15, Method = ReminderMethod.Notification },
                new EventReminderDto { MinutesBefore = 60, Method = ReminderMethod.Email }
            ]
        };

        var result = await _eventService.CreateEventAsync(dto, _caller);

        Assert.AreEqual(2, result.Reminders.Count);
        Assert.IsTrue(result.Reminders.Any(r => r.MinutesBefore == 15));
        Assert.IsTrue(result.Reminders.Any(r => r.MinutesBefore == 60 && r.Method == ReminderMethod.Email));
    }

    [TestMethod]
    public async Task CreateEvent_PublishesCreatedEvent()
    {
        await _eventService.CreateEventAsync(MakeEventDto("Published"), _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<CalendarEventCreatedEvent>(e => e.Title == "Published"),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateEvent_InvalidCalendar_ThrowsValidation()
    {
        var dto = new CreateCalendarEventDto
        {
            CalendarId = Guid.NewGuid(),
            Title = "No Calendar",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _eventService.CreateEventAsync(dto, _caller));
    }

    [TestMethod]
    public async Task CreateEvent_EndBeforeStart_ThrowsValidation()
    {
        var dto = new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Bad Range",
            StartUtc = DateTime.UtcNow.AddHours(2),
            EndUtc = DateTime.UtcNow.AddHours(1)
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _eventService.CreateEventAsync(dto, _caller));
    }

    [TestMethod]
    public async Task GetEvent_Exists_ReturnsEvent()
    {
        var created = await _eventService.CreateEventAsync(MakeEventDto("Findable"), _caller);

        var result = await _eventService.GetEventAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Findable", result.Title);
    }

    [TestMethod]
    public async Task GetEvent_NotFound_ReturnsNull()
    {
        var result = await _eventService.GetEventAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListEvents_ReturnsCalendarEvents()
    {
        await _eventService.CreateEventAsync(MakeEventDto("Event 1"), _caller);
        await _eventService.CreateEventAsync(MakeEventDto("Event 2"), _caller);

        var results = await _eventService.ListEventsAsync(_calendar.Id, _caller);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task UpdateEvent_ChangesTitle()
    {
        var created = await _eventService.CreateEventAsync(MakeEventDto("Original"), _caller);

        var updated = await _eventService.UpdateEventAsync(
            created.Id, new UpdateCalendarEventDto { Title = "Updated" }, _caller);

        Assert.AreEqual("Updated", updated.Title);
    }

    [TestMethod]
    public async Task UpdateEvent_PublishesUpdatedEvent()
    {
        var created = await _eventService.CreateEventAsync(MakeEventDto(), _caller);

        await _eventService.UpdateEventAsync(
            created.Id, new UpdateCalendarEventDto { Title = "Changed" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarEventUpdatedEvent>(),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteEvent_SoftDeletes()
    {
        var created = await _eventService.CreateEventAsync(MakeEventDto("Delete Me"), _caller);

        await _eventService.DeleteEventAsync(created.Id, _caller);

        var result = await _eventService.GetEventAsync(created.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteEvent_PublishesDeletedEvent()
    {
        var created = await _eventService.CreateEventAsync(MakeEventDto(), _caller);

        await _eventService.DeleteEventAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarEventDeletedEvent>(),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateEvent_AllDay_Allowed()
    {
        var dto = new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "All Day",
            StartUtc = DateTime.Today,
            EndUtc = DateTime.Today, // same start/end OK for all-day
            IsAllDay = true
        };

        var result = await _eventService.CreateEventAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsAllDay);
    }

    [TestMethod]
    public async Task SearchEvents_ByQuery_MatchesTitle()
    {
        await _eventService.CreateEventAsync(MakeEventDto("Alpha Meeting"), _caller);
        await _eventService.CreateEventAsync(MakeEventDto("Beta Standup"), _caller);

        var results = await _eventService.SearchEventsAsync(_caller, query: "Alpha");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alpha Meeting", results[0].Title);
    }
}
