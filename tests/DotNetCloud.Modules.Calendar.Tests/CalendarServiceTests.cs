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
/// Tests for <see cref="CalendarService"/>.
/// </summary>
[TestClass]
public class CalendarServiceTests
{
    private CalendarDbContext _db = null!;
    private CalendarService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new CalendarService(_db, _eventBusMock.Object, Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(), NullLogger<CalendarService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateCalendar_ValidDto_ReturnsCalendar()
    {
        var dto = new CreateCalendarDto
        {
            Name = "Work Calendar",
            Description = "My work schedule",
            Color = "#3366CC",
            Timezone = "America/New_York"
        };

        var result = await _service.CreateCalendarAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Work Calendar", result.Name);
        Assert.AreEqual("My work schedule", result.Description);
        Assert.AreEqual("#3366CC", result.Color);
        Assert.AreEqual("America/New_York", result.Timezone);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateCalendar_DefaultFlags_AreCorrect()
    {
        var dto = new CreateCalendarDto { Name = "Test" };

        var result = await _service.CreateCalendarAsync(dto, _caller);

        Assert.IsFalse(result.IsDefault);
        Assert.IsTrue(result.IsVisible);
        Assert.IsFalse(result.IsDeleted);
    }

    [TestMethod]
    public async Task GetCalendar_Exists_ReturnsCalendar()
    {
        var created = await _service.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Lookup" }, _caller);

        var result = await _service.GetCalendarAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Lookup", result.Name);
    }

    [TestMethod]
    public async Task GetCalendar_NotFound_ReturnsNull()
    {
        var result = await _service.GetCalendarAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCalendar_OtherUser_ReturnsNull()
    {
        var created = await _service.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Private" }, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var result = await _service.GetCalendarAsync(created.Id, otherCaller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListCalendars_ReturnsOwnCalendars()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.CreateCalendarAsync(new CreateCalendarDto { Name = "Mine" }, _caller);
        await _service.CreateCalendarAsync(new CreateCalendarDto { Name = "Theirs" }, otherCaller);

        var results = await _service.ListCalendarsAsync(_caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Mine", results[0].Name);
    }

    [TestMethod]
    public async Task UpdateCalendar_ChangesName()
    {
        var created = await _service.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Original" }, _caller);

        var updated = await _service.UpdateCalendarAsync(
            created.Id, new UpdateCalendarDto { Name = "Updated" }, _caller);

        Assert.AreEqual("Updated", updated.Name);
    }

    [TestMethod]
    public async Task UpdateCalendar_NotFound_ThrowsValidation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.UpdateCalendarAsync(Guid.NewGuid(), new UpdateCalendarDto { Name = "X" }, _caller));
    }

    [TestMethod]
    public async Task DeleteCalendar_SoftDeletes()
    {
        var created = await _service.CreateCalendarAsync(
            new CreateCalendarDto { Name = "ToDelete" }, _caller);

        await _service.DeleteCalendarAsync(created.Id, _caller);

        var result = await _service.GetCalendarAsync(created.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteCalendar_NotFound_ThrowsValidation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.DeleteCalendarAsync(Guid.NewGuid(), _caller));
    }

    [TestMethod]
    public async Task UpdateCalendar_PartialUpdate_OnlyChangesProvidedFields()
    {
        var created = await _service.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Full", Description = "Desc", Color = "#FF0000" }, _caller);

        var updated = await _service.UpdateCalendarAsync(
            created.Id, new UpdateCalendarDto { Color = "#00FF00" }, _caller);

        Assert.AreEqual("Full", updated.Name);
        Assert.AreEqual("Desc", updated.Description);
        Assert.AreEqual("#00FF00", updated.Color);
    }
}
