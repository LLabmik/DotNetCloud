using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrgDirectory = DotNetCloud.Core.Capabilities.IOrganizationDirectory;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Authorization tests for organization-owned calendars: membership-based access,
/// Manager+ write enforcement, and coexistence with user-owned calendars.
/// </summary>
[TestClass]
public class OrganizationCalendarAuthorizationTests
{
    private CalendarDbContext _db = null!;
    private CalendarService _calendarService = null!;
    private CalendarEventService _eventService = null!;
    private CalendarShareService _shareService = null!;
    private Mock<OrgDirectory> _orgDirMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _orgMember = null!;
    private CallerContext _orgManager = null!;
    private CallerContext _nonMember = null!;
    private CallerContext _regularUser = null!;
    private readonly Guid _orgId = Guid.NewGuid();

    // Well-known role GUIDs matching CalendarService.HasManagerOrAboveRole
    private static readonly Guid ManagerRoleId = Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001");
    private static readonly Guid AdminRoleId = Guid.Parse("a1b2c3d4-0002-4000-8000-000000000001");

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _orgDirMock = new Mock<OrgDirectory>();

        _calendarService = new CalendarService(_db, _eventBusMock.Object, _orgDirMock.Object, NullLogger<CalendarService>.Instance);
        _eventService = new CalendarEventService(_db, _eventBusMock.Object, _orgDirMock.Object, NullLogger<CalendarEventService>.Instance);
        _shareService = new CalendarShareService(_db, _eventBusMock.Object, NullLogger<CalendarShareService>.Instance);

        var memberId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var regularUserId = Guid.NewGuid();

        _orgMember = new CallerContext(memberId, ["user"], CallerType.User);
        _orgManager = new CallerContext(managerId, ["user"], CallerType.User);
        _nonMember = new CallerContext(nonMemberId, ["user"], CallerType.User);
        _regularUser = new CallerContext(regularUserId, ["user"], CallerType.User);

        // Set up org directory mock
        _orgDirMock.Setup(d => d.IsOrganizationMemberAsync(_orgId, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orgDirMock.Setup(d => d.IsOrganizationMemberAsync(_orgId, managerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orgDirMock.Setup(d => d.IsOrganizationMemberAsync(_orgId, nonMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Member (no Manager role)
        _orgDirMock.Setup(d => d.GetMemberAsync(_orgId, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationMemberInfo
            {
                OrganizationId = _orgId,
                UserId = memberId,
                RoleIds = new List<Guid>()
            });

        // Manager (has Manager role)
        _orgDirMock.Setup(d => d.GetMemberAsync(_orgId, managerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationMemberInfo
            {
                OrganizationId = _orgId,
                UserId = managerId,
                RoleIds = new List<Guid> { ManagerRoleId }
            });

        // Non-member
        _orgDirMock.Setup(d => d.GetMemberAsync(_orgId, nonMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationMemberInfo?)null);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Calendar Creation (Org) ───────────────────────────────────

    [TestMethod]
    public async Task CreateOrgCalendar_Manager_Succeeds()
    {
        var dto = new CreateCalendarDto
        {
            Name = "Org Calendar",
            OrganizationId = _orgId
        };

        var result = await _calendarService.CreateCalendarAsync(dto, _orgManager);

        Assert.IsNotNull(result);
        Assert.AreEqual(_orgId, result.OrganizationId);
        Assert.AreEqual("Org Calendar", result.Name);
    }

    [TestMethod]
    public async Task CreateOrgCalendar_MemberWithoutManager_Throws()
    {
        var dto = new CreateCalendarDto
        {
            Name = "Org Calendar",
            OrganizationId = _orgId
        };

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _calendarService.CreateCalendarAsync(dto, _orgMember));
    }

    [TestMethod]
    public async Task CreateOrgCalendar_NonMember_Throws()
    {
        var dto = new CreateCalendarDto
        {
            Name = "Org Calendar",
            OrganizationId = _orgId
        };

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _calendarService.CreateCalendarAsync(dto, _nonMember));
    }

    [TestMethod]
    public async Task CreateUserCalendar_NoOrgId_Succeeds()
    {
        var dto = new CreateCalendarDto
        {
            Name = "Personal Calendar"
        };

        var result = await _calendarService.CreateCalendarAsync(dto, _regularUser);

        Assert.IsNotNull(result);
        Assert.IsNull(result.OrganizationId);
    }

    // ─── Calendar Listing (Org) ────────────────────────────────────

    [TestMethod]
    public async Task ListCalendars_OrgMember_SeesOrgCalendar()
    {
        // Create an org calendar as manager
        var cal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Org Cal", OrganizationId = _orgId }, _orgManager);

        // Non-member should NOT see it
        var nonMemberCals = await _calendarService.ListCalendarsAsync(_nonMember);
        Assert.IsFalse(nonMemberCals.Any(c => c.Id == cal.Id),
            "Non-member should not see org calendar");

        // Org member should see it
        var memberCals = await _calendarService.ListCalendarsAsync(_orgMember);
        Assert.IsTrue(memberCals.Any(c => c.Id == cal.Id),
            "Org member should see org calendar");
    }

    // ─── Event CRUD on Org Calendar ────────────────────────────────

    [TestMethod]
    public async Task CreateEventOnOrgCalendar_MemberWithoutManager_Throws()
    {
        var cal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Org Cal", OrganizationId = _orgId }, _orgManager);

        var evtDto = new CreateCalendarEventDto
        {
            CalendarId = cal.Id,
            Title = "Test Event",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        };

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _eventService.CreateEventAsync(evtDto, _orgMember));
    }

    [TestMethod]
    public async Task CreateEventOnOrgCalendar_Manager_Succeeds()
    {
        var cal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Org Cal", OrganizationId = _orgId }, _orgManager);

        var evtDto = new CreateCalendarEventDto
        {
            CalendarId = cal.Id,
            Title = "Manager Event",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        };

        var result = await _eventService.CreateEventAsync(evtDto, _orgManager);

        Assert.IsNotNull(result);
        Assert.AreEqual("Manager Event", result.Title);
    }

    // ─── Calendar Sharing on Org Calendars ──────────────────────────

    [TestMethod]
    public async Task ShareOrgCalendar_ThrowsValidationException()
    {
        var cal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Org Cal", OrganizationId = _orgId }, _orgManager);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareCalendarAsync(cal.Id, Guid.NewGuid(), null, CalendarSharePermission.ReadOnly, _orgManager));
    }

    [TestMethod]
    public async Task ShareUserCalendar_Succeeds()
    {
        var cal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Personal" }, _regularUser);

        var share = await _shareService.ShareCalendarAsync(cal.Id, Guid.NewGuid(), null, CalendarSharePermission.ReadOnly, _regularUser);

        Assert.IsNotNull(share);
    }

    // ─── Coexistence: User + Org Calendars ──────────────────────────

    [TestMethod]
    public async Task UserOwnedAndOrgCalendars_CoexistInListing()
    {
        // Create a user-owned calendar
        var userCal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Personal" }, _orgMember);

        // Create an org calendar
        var orgCal = await _calendarService.CreateCalendarAsync(
            new CreateCalendarDto { Name = "Org Cal", OrganizationId = _orgId }, _orgManager);

        // Org member should see both
        var cals = await _calendarService.ListCalendarsAsync(_orgMember);
        Assert.IsTrue(cals.Any(c => c.Id == userCal.Id), "Should see own user calendar");
        Assert.IsTrue(cals.Any(c => c.Id == orgCal.Id), "Should see org calendar");
    }
}
