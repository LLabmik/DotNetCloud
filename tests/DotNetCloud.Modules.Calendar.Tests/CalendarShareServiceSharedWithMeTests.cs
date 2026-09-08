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
/// Tests for <see cref="CalendarShareService.ListSharedWithMeAsync"/> — the user + team "shared
/// with me" query that feeds aggregated surfaces such as the Files shared-with-me tree.
/// </summary>
[TestClass]
public class CalendarShareServiceSharedWithMeTests
{
    private CalendarDbContext _db = null!;
    private CalendarShareService _shareService = null!;
    private CalendarService _calendarService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _owner = null!;
    private CallerContext _recipient = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _calendarService = new CalendarService(
            _db,
            _eventBusMock.Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(),
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<CalendarService>.Instance);
        _shareService = new CalendarShareService(_db, _eventBusMock.Object, NullLogger<CalendarShareService>.Instance);
        _owner = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        _recipient = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task ListSharedWithMe_UserShare_ReturnsSharedCalendarWithName()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Family" }, _owner);
        await _shareService.ShareCalendarAsync(
            calendar.Id, _recipient.UserId, null, CalendarSharePermission.ReadOnly, _owner);

        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual(calendar.Id, item.CalendarId);
        Assert.AreEqual("Family", item.Name);
        Assert.AreEqual(_owner.UserId, item.OwnerId);
        Assert.AreEqual(_recipient.UserId, item.SharedWithUserId);
        Assert.IsNull(item.SharedWithTeamId);
    }

    [TestMethod]
    public async Task ListSharedWithMe_TeamShare_ReturnsCalendarForTeamMember()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Team Calendar" }, _owner);
        var teamId = Guid.CreateVersion7();
        await _shareService.ShareCalendarAsync(
            calendar.Id, null, teamId, CalendarSharePermission.ReadWrite, _owner);

        // Recipient is a member of the target team.
        var teamDirectory = new Mock<DotNetCloud.Core.Capabilities.ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(_recipient.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DotNetCloud.Core.Capabilities.TeamInfo
                {
                    Id = teamId,
                    OrganizationId = Guid.CreateVersion7(),
                    Name = "Eng",
                    MemberCount = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            });
        var shareService = new CalendarShareService(_db, _eventBusMock.Object, NullLogger<CalendarShareService>.Instance, teamDirectory.Object);

        var items = await shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual(calendar.Id, item.CalendarId);
        Assert.AreEqual("Team Calendar", item.Name);
        Assert.AreEqual(teamId, item.SharedWithTeamId);
    }

    [TestMethod]
    public async Task ListSharedWithMe_NotTargetedUser_ReturnsEmpty()
    {
        var calendar = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Private" }, _owner);
        await _shareService.ShareCalendarAsync(
            calendar.Id, Guid.CreateVersion7(), null, CalendarSharePermission.ReadOnly, _owner);

        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task ListSharedWithMe_NoShares_ReturnsEmpty()
    {
        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(0, items.Count);
    }
}
