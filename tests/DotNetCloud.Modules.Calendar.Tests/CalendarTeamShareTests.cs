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
/// Tests for team-targeted calendar shares: create/list/revoke via the share service and
/// team-membership read access through <see cref="CalendarService"/> /
/// <see cref="CalendarEventService"/>.
/// </summary>
[TestClass]
public class CalendarTeamShareTests
{
    private CalendarDbContext _db = default!;
    private CalendarShareService _shareService = default!;
    private Mock<IEventBus> _eventBusMock = default!;
    private CallerContext _owner = default!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _shareService = new CalendarShareService(_db, _eventBusMock.Object, NullLogger<CalendarShareService>.Instance);
        _owner = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static CallerContext NewUser() => new(Guid.CreateVersion7(), ["user"], CallerType.User);

    private CalendarService CreateCalendarService(DotNetCloud.Core.Capabilities.ITeamDirectory? teamDirectory = null)
        => new(_db, new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(),
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<CalendarService>.Instance,
            teamDirectory);

    private CalendarEventService CreateEventService(DotNetCloud.Core.Capabilities.ITeamDirectory? teamDirectory = null)
        => new(_db, new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(),
            new OccurrenceExpansionService(_db, new RecurrenceEngine(NullLogger<RecurrenceEngine>.Instance), NullLogger<OccurrenceExpansionService>.Instance),
            NullLogger<CalendarEventService>.Instance,
            teamDirectory);

    /// <summary>Builds a team directory that maps a member to a team.</summary>
    private (DotNetCloud.Core.Capabilities.ITeamDirectory Directory, CallerContext Member, Guid TeamId) CreateTeamDirectory(Guid? memberId = null, Guid? teamId = null)
    {
        var team = teamId ?? Guid.CreateVersion7();
        var member = new CallerContext(memberId ?? Guid.CreateVersion7(), ["user"], CallerType.User);

        var teamDirectory = new Mock<DotNetCloud.Core.Capabilities.ITeamDirectory>();
        // Default for everyone else: no memberships. Must be registered before the
        // member-specific setup so the more specific setup (registered last) wins.
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DotNetCloud.Core.Capabilities.TeamInfo>());
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(member.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DotNetCloud.Core.Capabilities.TeamInfo
                {
                    Id = team,
                    OrganizationId = Guid.CreateVersion7(),
                    Name = "Eng",
                    MemberCount = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            });

        return (teamDirectory.Object, member, team);
    }

    private async Task<CalendarDto> CreateOwnedCalendarAsync(string name = "Team calendar")
        => await CreateCalendarService().CreateCalendarAsync(new CreateCalendarDto { Name = name }, _owner);

    // ─── Share service ────────────────────────────────────────────────

    [TestMethod]
    public async Task ShareCalendar_TeamTarget_CreatesTeamShare()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var teamId = Guid.CreateVersion7();

        var share = await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.IsNull(share.SharedWithUserId);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.AreEqual(CalendarSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task ShareCalendar_TeamTarget_PublishesTeamEvent()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var teamId = Guid.CreateVersion7();

        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<ResourceSharedEvent>(e => e.SharedWithTeamId == teamId && e.SharedWithUserId == null),
                _owner,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ShareCalendar_TeamDedupe_UpdatesPermission()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var teamId = Guid.CreateVersion7();

        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);
        var updated = await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadWrite, _owner);

        Assert.AreEqual(teamId, updated.SharedWithTeamId);
        Assert.AreEqual(CalendarSharePermission.ReadWrite, updated.Permission);

        var shares = await _shareService.ListSharesAsync(calendar.Id, _owner);
        Assert.AreEqual(1, shares.Count);
    }

    [TestMethod]
    public async Task ShareCalendar_NoTarget_Throws()
    {
        var calendar = await CreateOwnedCalendarAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareCalendarAsync(calendar.Id, null, null, CalendarSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task ShareCalendar_BothTargets_Throws()
    {
        var calendar = await CreateOwnedCalendarAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareCalendarAsync(calendar.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), CalendarSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task RemoveShare_TeamShare_OwnerCanRevoke()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var teamId = Guid.CreateVersion7();
        var share = await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        var shares = await _shareService.ListSharesAsync(calendar.Id, _owner);
        Assert.AreEqual(0, shares.Count);
    }

    // ─── Read access via membership ───────────────────────────────────

    [TestMethod]
    public async Task GetCalendarAsync_TeamMember_CanReadTeamSharedCalendar()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var (directory, member, teamId) = CreateTeamDirectory();
        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        var service = CreateCalendarService(directory);
        var result = await service.GetCalendarAsync(calendar.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(calendar.Id, result.Id);
    }

    [TestMethod]
    public async Task GetCalendarAsync_NonMember_CannotReadTeamSharedCalendar()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var (directory, member, teamId) = CreateTeamDirectory();
        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        var service = CreateCalendarService(directory);
        var stranger = NewUser();
        var result = await service.GetCalendarAsync(calendar.Id, stranger);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListCalendarsAsync_TeamMember_IncludesTeamSharedCalendar()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var (directory, member, teamId) = CreateTeamDirectory();
        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        var service = CreateCalendarService(directory);
        var calendars = await service.ListCalendarsAsync(member);

        Assert.IsTrue(calendars.Any(c => c.Id == calendar.Id));
    }

    [TestMethod]
    public async Task ListCalendarsAsync_NonMember_ExcludesTeamSharedCalendar()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var (directory, member, teamId) = CreateTeamDirectory();
        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        var service = CreateCalendarService(directory);
        var calendars = await service.ListCalendarsAsync(NewUser());

        Assert.IsFalse(calendars.Any(c => c.Id == calendar.Id));
    }

    [TestMethod]
    public async Task ListEventsAsync_TeamMember_ReturnsTeamSharedCalendarEvents()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var (directory, member, teamId) = CreateTeamDirectory();
        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadWrite, _owner);

        var ownerEventService = CreateEventService();
        var evt = await ownerEventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calendar.Id,
            Title = "Team Standup",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        }, _owner);

        var memberEventService = CreateEventService(directory);
        var events = await memberEventService.ListEventsAsync(calendar.Id, member);

        Assert.IsTrue(events.Any(e => e.Id == evt.Id));
    }

    [TestMethod]
    public async Task GetEventAsync_ReadOnlyTeamMember_CannotEditTeamSharedCalendar()
    {
        var calendar = await CreateOwnedCalendarAsync();
        var (directory, member, teamId) = CreateTeamDirectory();
        await _shareService.ShareCalendarAsync(calendar.Id, null, teamId, CalendarSharePermission.ReadOnly, _owner);

        var ownerEventService = CreateEventService();
        var evt = await ownerEventService.CreateEventAsync(new CreateCalendarEventDto
        {
            CalendarId = calendar.Id,
            Title = "Read Only Event",
            StartUtc = DateTime.UtcNow.AddHours(1),
            EndUtc = DateTime.UtcNow.AddHours(2)
        }, _owner);

        var memberEventService = CreateEventService(directory);
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => memberEventService.UpdateEventAsync(evt.Id, new UpdateCalendarEventDto { Title = "Hijacked" }, member));
    }
}
