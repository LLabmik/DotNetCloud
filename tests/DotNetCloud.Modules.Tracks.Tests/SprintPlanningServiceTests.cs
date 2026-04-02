using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests for <see cref="SprintPlanningService"/> covering year plan creation,
/// sprint adjustment with date cascading, and plan overview (Phase C).
/// </summary>
[TestClass]
public class SprintPlanningServiceTests
{
    private TracksDbContext _db = null!;
    private SprintPlanningService _service = null!;
    private BoardService _boardService = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;
    private Board _board = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _admin = TestHelpers.CreateCaller();
        _member = TestHelpers.CreateCaller();

        var eventBus = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, eventBus.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, eventBus.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new SprintPlanningService(_db, _boardService, activityService, NullLogger<SprintPlanningService>.Instance);

        // Seed a Team-mode board with admin (Owner) + member
        _board = await TestHelpers.SeedBoardAsync(_db, _admin.UserId);
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await TestHelpers.AddMemberAsync(_db, _board.Id, _member.UserId, BoardMemberRole.Member);
        await _db.SaveChangesAsync();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── CreateYearPlan ───────────────────────────────────────────────

    [TestMethod]
    public async Task CreateYearPlan_ValidDto_CreatesSprintsAndReturnsOverview()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var result = await _service.CreateYearPlanAsync(_board.Id, dto, _admin);

        Assert.IsNotNull(result);
        Assert.AreEqual(_board.Id, result.BoardId);
        Assert.AreEqual(4, result.Sprints.Count);
        Assert.AreEqual(8, result.TotalWeeks); // 4 * 2
    }

    [TestMethod]
    public async Task CreateYearPlan_SprintsHaveSequentialDates()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };

        var result = await _service.CreateYearPlanAsync(_board.Id, dto, _admin);

        // Sprint 1: Jan 5 → Jan 19
        Assert.AreEqual(start, result.Sprints[0].StartDate);
        Assert.AreEqual(start.AddDays(14), result.Sprints[0].EndDate);

        // Sprint 2: Jan 19 → Feb 2
        Assert.AreEqual(start.AddDays(14), result.Sprints[1].StartDate);
        Assert.AreEqual(start.AddDays(28), result.Sprints[1].EndDate);

        // Sprint 3: Feb 2 → Feb 16
        Assert.AreEqual(start.AddDays(28), result.Sprints[2].StartDate);
        Assert.AreEqual(start.AddDays(42), result.Sprints[2].EndDate);
    }

    [TestMethod]
    public async Task CreateYearPlan_SprintsHaveSequentialPlannedOrder()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 5,
            DefaultDurationWeeks = 2
        };

        var result = await _service.CreateYearPlanAsync(_board.Id, dto, _admin);

        for (var i = 0; i < result.Sprints.Count; i++)
        {
            Assert.AreEqual(i + 1, result.Sprints[i].PlannedOrder);
        }
    }

    [TestMethod]
    public async Task CreateYearPlan_SprintsHavePlanningStatus()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };

        var result = await _service.CreateYearPlanAsync(_board.Id, dto, _admin);

        foreach (var sprint in result.Sprints)
        {
            Assert.AreEqual(SprintStatus.Planning, sprint.Status);
        }
    }

    [TestMethod]
    public async Task CreateYearPlan_SprintTitlesAreSequential()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };

        var result = await _service.CreateYearPlanAsync(_board.Id, dto, _admin);

        Assert.AreEqual("Sprint 1", result.Sprints[0].Title);
        Assert.AreEqual("Sprint 2", result.Sprints[1].Title);
        Assert.AreEqual("Sprint 3", result.Sprints[2].Title);
    }

    [TestMethod]
    public async Task CreateYearPlan_SecondPlan_ContinuesOrdering()
    {
        var dto1 = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        await _service.CreateYearPlanAsync(_board.Id, dto1, _admin);

        var dto2 = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow.AddDays(42),
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };
        var result = await _service.CreateYearPlanAsync(_board.Id, dto2, _admin);

        // Second batch should have PlannedOrder 4, 5
        var allSprints = result.Sprints;
        Assert.AreEqual(5, allSprints.Count);
        Assert.AreEqual(4, allSprints[3].PlannedOrder);
        Assert.AreEqual(5, allSprints[4].PlannedOrder);
    }

    [TestMethod]
    public async Task CreateYearPlan_PersonalBoard_Throws()
    {
        var personalBoard = await TestHelpers.SeedBoardAsync(_db, _admin.UserId, "Personal");
        // Default Mode is Personal

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.CreateYearPlanAsync(personalBoard.Id, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task CreateYearPlan_AsMember_Throws()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.CreateYearPlanAsync(_board.Id, dto, _member));
    }

    [TestMethod]
    public async Task CreateYearPlan_DurationZero_Throws()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 0
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.CreateYearPlanAsync(_board.Id, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task CreateYearPlan_Duration17_Throws()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 17
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.CreateYearPlanAsync(_board.Id, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task CreateYearPlan_CountZero_Throws()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 0,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.CreateYearPlanAsync(_board.Id, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task CreateYearPlan_Count105_Throws()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 105,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.CreateYearPlanAsync(_board.Id, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task CreateYearPlan_BoundaryValues_Succeed()
    {
        // Duration = 1 (min), Count = 1 (min)
        var dto1 = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 1,
            DefaultDurationWeeks = 1
        };
        var result1 = await _service.CreateYearPlanAsync(_board.Id, dto1, _admin);
        Assert.AreEqual(1, result1.Sprints.Count);

        // Duration = 16 (max), Count = 1
        var dto2 = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow.AddDays(100),
            SprintCount = 1,
            DefaultDurationWeeks = 16
        };
        var result2 = await _service.CreateYearPlanAsync(_board.Id, dto2, _admin);
        Assert.AreEqual(2, result2.Sprints.Count); // 1 from first plan + 1 new
    }

    // ─── AdjustSprint ────────────────────────────────────────────────

    [TestMethod]
    public async Task AdjustSprint_ChangesDuration()
    {
        var plan = await CreateTestPlan(3, 2);
        var sprintId = plan.Sprints[0].Id;

        var dto = new AdjustSprintDto { DurationWeeks = 3 };
        var result = await _service.AdjustSprintAsync(sprintId, dto, _admin);

        Assert.AreEqual(3, result.Sprints[0].DurationWeeks);
    }

    [TestMethod]
    public async Task AdjustSprint_CascadesDatesToSubsequentSprints()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var planDto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        var plan = await _service.CreateYearPlanAsync(_board.Id, planDto, _admin);
        var firstSprintId = plan.Sprints[0].Id;

        // Change first sprint from 2 weeks to 3 weeks
        var adjustDto = new AdjustSprintDto { DurationWeeks = 3 };
        var result = await _service.AdjustSprintAsync(firstSprintId, adjustDto, _admin);

        // Sprint 1: Jan 5 → Jan 26 (3 weeks)
        Assert.AreEqual(start, result.Sprints[0].StartDate);
        Assert.AreEqual(start.AddDays(21), result.Sprints[0].EndDate);

        // Sprint 2 should cascade: Jan 26 → Feb 9 (still 2 weeks)
        Assert.AreEqual(start.AddDays(21), result.Sprints[1].StartDate);
        Assert.AreEqual(start.AddDays(35), result.Sprints[1].EndDate);

        // Sprint 3 should cascade: Feb 9 → Feb 23 (still 2 weeks)
        Assert.AreEqual(start.AddDays(35), result.Sprints[2].StartDate);
        Assert.AreEqual(start.AddDays(49), result.Sprints[2].EndDate);
    }

    [TestMethod]
    public async Task AdjustSprint_WithStartDateOverride_UpdatesStartAndCascades()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var planDto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };
        var plan = await _service.CreateYearPlanAsync(_board.Id, planDto, _admin);
        var firstSprintId = plan.Sprints[0].Id;

        var newStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var adjustDto = new AdjustSprintDto { DurationWeeks = 2, StartDate = newStart };
        var result = await _service.AdjustSprintAsync(firstSprintId, adjustDto, _admin);

        // Sprint 1: Feb 1 → Feb 15
        Assert.AreEqual(newStart, result.Sprints[0].StartDate);
        Assert.AreEqual(newStart.AddDays(14), result.Sprints[0].EndDate);

        // Sprint 2 cascaded: Feb 15 → Mar 1
        Assert.AreEqual(newStart.AddDays(14), result.Sprints[1].StartDate);
    }

    [TestMethod]
    public async Task AdjustSprint_SprintNotFound_Throws()
    {
        var dto = new AdjustSprintDto { DurationWeeks = 2 };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AdjustSprintAsync(Guid.NewGuid(), dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.SprintNotFound));
    }

    [TestMethod]
    public async Task AdjustSprint_DurationZero_Throws()
    {
        var plan = await CreateTestPlan(1, 2);
        var sprintId = plan.Sprints[0].Id;

        var dto = new AdjustSprintDto { DurationWeeks = 0 };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AdjustSprintAsync(sprintId, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task AdjustSprint_Duration17_Throws()
    {
        var plan = await CreateTestPlan(1, 2);
        var sprintId = plan.Sprints[0].Id;

        var dto = new AdjustSprintDto { DurationWeeks = 17 };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AdjustSprintAsync(sprintId, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.InvalidSprintDuration));
    }

    [TestMethod]
    public async Task AdjustSprint_AsMember_Throws()
    {
        var plan = await CreateTestPlan(1, 2);
        var sprintId = plan.Sprints[0].Id;

        var dto = new AdjustSprintDto { DurationWeeks = 3 };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AdjustSprintAsync(sprintId, dto, _member));
    }

    [TestMethod]
    public async Task AdjustSprint_PersonalBoard_Throws()
    {
        // Create a personal board and a sprint on it
        var personalBoard = await TestHelpers.SeedBoardAsync(_db, _admin.UserId, "Personal");
        var sprint = new Sprint
        {
            BoardId = personalBoard.Id,
            Title = "Sprint 1",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1,
            Status = SprintStatus.Planning
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        var dto = new AdjustSprintDto { DurationWeeks = 3 };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AdjustSprintAsync(sprint.Id, dto, _admin));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    // ─── GetPlanOverview ─────────────────────────────────────────────

    [TestMethod]
    public async Task GetPlanOverview_ReturnsAllSprints()
    {
        await CreateTestPlan(4, 2);

        var result = await _service.GetPlanOverviewAsync(_board.Id, _admin);

        Assert.AreEqual(4, result.Sprints.Count);
        Assert.AreEqual(_board.Id, result.BoardId);
    }

    [TestMethod]
    public async Task GetPlanOverview_ComputesTotalWeeks()
    {
        await CreateTestPlan(4, 2);

        var result = await _service.GetPlanOverviewAsync(_board.Id, _admin);

        Assert.AreEqual(8, result.TotalWeeks);
    }

    [TestMethod]
    public async Task GetPlanOverview_SetsPlanStartAndEndDates()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        await _service.CreateYearPlanAsync(_board.Id, dto, _admin);

        var result = await _service.GetPlanOverviewAsync(_board.Id, _admin);

        Assert.AreEqual(start, result.PlanStartDate);
        Assert.AreEqual(start.AddDays(42), result.PlanEndDate);
    }

    [TestMethod]
    public async Task GetPlanOverview_MemberCanView()
    {
        await CreateTestPlan(3, 2);

        var result = await _service.GetPlanOverviewAsync(_board.Id, _member);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Sprints.Count);
    }

    [TestMethod]
    public async Task GetPlanOverview_NonMember_Throws()
    {
        await CreateTestPlan(3, 2);
        var outsider = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.GetPlanOverviewAsync(_board.Id, outsider));
    }

    [TestMethod]
    public async Task GetPlanOverview_EmptyBoard_ReturnsEmptyList()
    {
        var result = await _service.GetPlanOverviewAsync(_board.Id, _admin);

        Assert.AreEqual(0, result.Sprints.Count);
        Assert.AreEqual(0, result.TotalWeeks);
        Assert.IsNull(result.PlanStartDate);
        Assert.IsNull(result.PlanEndDate);
    }

    // ─── Helper ──────────────────────────────────────────────────────

    private async Task<SprintPlanOverviewDto> CreateTestPlan(int count, int durationWeeks)
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = count,
            DefaultDurationWeeks = durationWeeks
        };
        return await _service.CreateYearPlanAsync(_board.Id, dto, _admin);
    }
}
