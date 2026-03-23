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
/// Tests for <see cref="CalendarShareService"/>.
/// </summary>
[TestClass]
public class CalendarShareServiceTests
{
    private CalendarDbContext _db = null!;
    private CalendarShareService _shareService = null!;
    private CalendarService _calendarService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _owner = null!;
    private CallerContext _otherUser = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _calendarService = new CalendarService(_db, _eventBusMock.Object, NullLogger<CalendarService>.Instance);
        _shareService = new CalendarShareService(_db, NullLogger<CalendarShareService>.Instance);
        _owner = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _otherUser = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task ShareCalendar_ValidUserShare_ReturnsShare()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Work" }, _owner);

        var share = await _shareService.ShareCalendarAsync(
            calendar.Id, _otherUser.UserId, null, CalendarSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.AreEqual(calendar.Id, share.CalendarId);
        Assert.AreEqual(_otherUser.UserId, share.SharedWithUserId);
        Assert.IsNull(share.SharedWithTeamId);
        Assert.AreEqual(CalendarSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task ShareCalendar_TeamShare_SetsTeamId()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Team Calendar" }, _owner);
        var teamId = Guid.NewGuid();

        var share = await _shareService.ShareCalendarAsync(
            calendar.Id, null, teamId, CalendarSharePermission.ReadWrite, _owner);

        Assert.IsNotNull(share);
        Assert.IsNull(share.SharedWithUserId);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.AreEqual(CalendarSharePermission.ReadWrite, share.Permission);
    }

    [TestMethod]
    public async Task ShareCalendar_NonExistentCalendar_ThrowsValidation()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareCalendarAsync(
                Guid.NewGuid(), _otherUser.UserId, null, CalendarSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task ShareCalendar_NotOwner_ThrowsValidation()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Private" }, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareCalendarAsync(
                calendar.Id, Guid.NewGuid(), null, CalendarSharePermission.ReadOnly, _otherUser));
    }

    [TestMethod]
    public async Task RemoveShare_OwnerCanRemove()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Removable" }, _owner);
        var share = await _shareService.ShareCalendarAsync(
            calendar.Id, _otherUser.UserId, null, CalendarSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        var shares = await _shareService.ListSharesAsync(calendar.Id, _owner);
        Assert.AreEqual(0, shares.Count);
    }

    [TestMethod]
    public async Task RemoveShare_NonOwner_ThrowsValidation()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Protected" }, _owner);
        var share = await _shareService.ShareCalendarAsync(
            calendar.Id, _otherUser.UserId, null, CalendarSharePermission.ReadOnly, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.RemoveShareAsync(share.Id, _otherUser));
    }

    [TestMethod]
    public async Task ListShares_ReturnsOnlyOwnCalendarShares()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Shared" }, _owner);
        await _shareService.ShareCalendarAsync(
            calendar.Id, _otherUser.UserId, null, CalendarSharePermission.ReadOnly, _owner);
        await _shareService.ShareCalendarAsync(
            calendar.Id, null, Guid.NewGuid(), CalendarSharePermission.ReadWrite, _owner);

        var shares = await _shareService.ListSharesAsync(calendar.Id, _owner);

        Assert.AreEqual(2, shares.Count);
    }

    [TestMethod]
    public async Task ListShares_NonOwner_ReturnsEmpty()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Hidden" }, _owner);
        await _shareService.ShareCalendarAsync(
            calendar.Id, _otherUser.UserId, null, CalendarSharePermission.ReadOnly, _owner);

        var shares = await _shareService.ListSharesAsync(calendar.Id, _otherUser);

        Assert.AreEqual(0, shares.Count);
    }
}
