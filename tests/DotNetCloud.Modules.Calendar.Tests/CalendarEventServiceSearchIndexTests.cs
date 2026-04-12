using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests that <see cref="CalendarEventService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on create, update, and delete operations.
/// </summary>
[TestClass]
public class CalendarEventServiceSearchIndexTests
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
        _eventService = new CalendarEventService(_db, _eventBusMock.Object, NullLogger<CalendarEventService>.Instance);
        _calendarService = new CalendarService(_db, _eventBusMock.Object, NullLogger<CalendarService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Test Calendar" }, _caller);
        _eventBusMock.Invocations.Clear();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateEvent_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var dto = new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Meeting",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        };

        var result = await _eventService.CreateEventAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "calendar" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateEvent_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var created = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "Original",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        }, _caller);
        _eventBusMock.Invocations.Clear();

        await _eventService.UpdateEventAsync(created.Id,
            new UpdateCalendarEventDto { Title = "Updated" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "calendar" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteEvent_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var created = await _eventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = _calendar.Id,
            Title = "To Delete",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        }, _caller);
        _eventBusMock.Invocations.Clear();

        await _eventService.DeleteEventAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "calendar" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
