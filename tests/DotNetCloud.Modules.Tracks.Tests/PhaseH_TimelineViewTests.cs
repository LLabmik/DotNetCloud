using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Comprehensive tests for Phase H: Timeline / Gantt View.
/// Validates timeline layout computation, sprint status styling, progress percentage,
/// tooltip generation, sprint adjustment with cascading, plan overview for timeline,
/// mode guards, and integration with sprint planning service.
/// </summary>
[TestClass]
public class PhaseH_TimelineViewTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private SprintPlanningService _planningService = null!;
    private SprintService _sprintService = null!;
    private CardService _cardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private ActivityService _activityService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();

    private Board _teamBoard = null!;
    private Board _personalBoard = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, _eventBusMock.Object, _activityService, teamService, NullLogger<BoardService>.Instance);
        _planningService = new SprintPlanningService(_db, _boardService, _activityService, NullLogger<SprintPlanningService>.Instance);
        _sprintService = new SprintService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, _activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, _activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);

        _admin = TestHelpers.CreateCaller(_adminUserId);
        _member = TestHelpers.CreateCaller(_memberUserId);

        // Seed Team board with admin (Owner) + member
        _teamBoard = await TestHelpers.SeedBoardAsync(_db, _adminUserId, "Team Board");
        _teamBoard.Mode = BoardMode.Team;
        _db.Update(_teamBoard);
        await TestHelpers.AddMemberAsync(_db, _teamBoard.Id, _memberUserId, BoardMemberRole.Member);
        await _db.SaveChangesAsync();

        // Seed Personal board
        _personalBoard = await TestHelpers.SeedBoardAsync(_db, _adminUserId, "Personal Board");
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ═══════════════════════════════════════════════════════════════════
    // Plan Overview for Timeline Display (Step 30 — Data Layer)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetPlanOverview_ReturnsSprintsOrderedByPlannedOrder()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 6,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(6, overview.Sprints.Count);
        for (var i = 0; i < overview.Sprints.Count; i++)
        {
            Assert.AreEqual(i + 1, overview.Sprints[i].PlannedOrder);
        }
    }

    [TestMethod]
    public async Task GetPlanOverview_IncludesPlanBoundaryDates()
    {
        var start = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 4,
            DefaultDurationWeeks = 3
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.IsNotNull(overview.PlanStartDate);
        Assert.IsNotNull(overview.PlanEndDate);
        Assert.AreEqual(start, overview.PlanStartDate);
        // 4 sprints × 3 weeks = 84 days
        Assert.AreEqual(start.AddDays(84), overview.PlanEndDate);
    }

    [TestMethod]
    public async Task GetPlanOverview_TotalWeeksReflectsSumOfAllSprintDurations()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 5,
            DefaultDurationWeeks = 3
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(15, overview.TotalWeeks); // 5 × 3
    }

    [TestMethod]
    public async Task GetPlanOverview_EmptyBoard_ReturnsEmptySprintList()
    {
        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.IsNotNull(overview);
        Assert.AreEqual(0, overview.Sprints.Count);
        Assert.AreEqual(0, overview.TotalWeeks);
        Assert.IsNull(overview.PlanStartDate);
        Assert.IsNull(overview.PlanEndDate);
    }

    [TestMethod]
    public async Task GetPlanOverview_IncludesCardCounts()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        var sprint1 = overview.Sprints[0];

        // Add cards to the sprint
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _teamBoard.Id, "To Do");
        var card1 = await TestHelpers.SeedCardAsync(_db, swimlane.Id, _adminUserId, "Card 1");
        card1.StoryPoints = 3;
        _db.Update(card1);
        await _db.SaveChangesAsync();

        var sprintCards = new SprintCard { SprintId = sprint1.Id, CardId = card1.Id };
        _db.Set<SprintCard>().Add(sprintCards);
        await _db.SaveChangesAsync();

        overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(1, overview.Sprints[0].CardCount);
        Assert.AreEqual(3, overview.Sprints[0].TotalStoryPoints);
        Assert.AreEqual(0, overview.Sprints[1].CardCount);
    }

    [TestMethod]
    public async Task GetPlanOverview_PersonalBoard_ReturnsEmptyPlan()
    {
        // GetPlanOverview is read-only — it checks membership, not mode.
        // For a personal board with no sprints, it returns an empty overview.
        var overview = await _planningService.GetPlanOverviewAsync(_personalBoard.Id, _admin);

        Assert.IsNotNull(overview);
        Assert.AreEqual(0, overview.Sprints.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint Adjustment with Cascading (Timeline Drag-to-Adjust)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AdjustSprint_CascadesSubsequentDates()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        var sprint2 = overview.Sprints[1]; // Sprint 2

        // Increase Sprint 2 from 2 weeks to 4 weeks
        var result = await _planningService.AdjustSprintAsync(sprint2.Id, new AdjustSprintDto
        {
            DurationWeeks = 4
        }, _admin);

        Assert.IsNotNull(result);
        // Sprint 2 should now be 4 weeks instead of 2
        Assert.AreEqual(4, result.Sprints[1].DurationWeeks);

        // Sprint 2 start stays the same (Jan 19)
        Assert.AreEqual(start.AddDays(14), result.Sprints[1].StartDate);
        // Sprint 2 end: Jan 19 + 28 days = Feb 16
        Assert.AreEqual(start.AddDays(42), result.Sprints[1].EndDate);

        // Sprint 3 should start where Sprint 2 ends (Feb 16)
        Assert.AreEqual(start.AddDays(42), result.Sprints[2].StartDate);
        // Sprint 3 is still 2 weeks: Feb 16 + 14 = Mar 2
        Assert.AreEqual(start.AddDays(56), result.Sprints[2].EndDate);

        // Sprint 4 cascades from Sprint 3 end
        Assert.AreEqual(start.AddDays(56), result.Sprints[3].StartDate);
        Assert.AreEqual(start.AddDays(70), result.Sprints[3].EndDate);
    }

    [TestMethod]
    public async Task AdjustSprint_ShrinkDuration_CascadesCorrectly()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 4
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        var sprint1 = overview.Sprints[0];

        // Shrink Sprint 1 from 4 weeks to 1 week
        var result = await _planningService.AdjustSprintAsync(sprint1.Id, new AdjustSprintDto
        {
            DurationWeeks = 1
        }, _admin);

        // Sprint 1: Jan 5 → Jan 12 (1 week)
        Assert.AreEqual(1, result.Sprints[0].DurationWeeks);
        Assert.AreEqual(start.AddDays(7), result.Sprints[0].EndDate);

        // Sprint 2: Jan 12 → Feb 9 (4 weeks, unchanged duration)
        Assert.AreEqual(start.AddDays(7), result.Sprints[1].StartDate);
        Assert.AreEqual(start.AddDays(35), result.Sprints[1].EndDate);

        // Sprint 3: Feb 9 → Mar 9 (4 weeks)
        Assert.AreEqual(start.AddDays(35), result.Sprints[2].StartDate);
        Assert.AreEqual(start.AddDays(63), result.Sprints[2].EndDate);
    }

    [TestMethod]
    public async Task AdjustSprint_InvalidDuration_Throws()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        // Duration 0 — invalid
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(overview.Sprints[0].Id, new AdjustSprintDto
            {
                DurationWeeks = 0
            }, _admin));

        // Duration 17 — exceeds max
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(overview.Sprints[0].Id, new AdjustSprintDto
            {
                DurationWeeks = 17
            }, _admin));
    }

    [TestMethod]
    public async Task AdjustSprint_AsMember_Throws()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(overview.Sprints[0].Id, new AdjustSprintDto
            {
                DurationWeeks = 3
            }, _member));
    }

    [TestMethod]
    public async Task AdjustSprint_NonExistentSprint_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(Guid.NewGuid(), new AdjustSprintDto
            {
                DurationWeeks = 2
            }, _admin));
    }

    [TestMethod]
    public async Task AdjustSprint_LastSprint_NoCascade()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        var lastSprint = overview.Sprints[2]; // Sprint 3

        var result = await _planningService.AdjustSprintAsync(lastSprint.Id, new AdjustSprintDto
        {
            DurationWeeks = 8
        }, _admin);

        // Last sprint changed, others unchanged
        Assert.AreEqual(8, result.Sprints[2].DurationWeeks);
        Assert.AreEqual(start.AddDays(28), result.Sprints[2].StartDate); // Same start
        Assert.AreEqual(start.AddDays(84), result.Sprints[2].EndDate); // 28 + 56 days

        // Previous sprints remain unchanged
        Assert.AreEqual(2, result.Sprints[0].DurationWeeks);
        Assert.AreEqual(2, result.Sprints[1].DurationWeeks);
    }

    [TestMethod]
    public async Task AdjustSprint_WithStartDateOverride_CascadesFromNewStart()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        var newStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await _planningService.AdjustSprintAsync(overview.Sprints[1].Id, new AdjustSprintDto
        {
            DurationWeeks = 3,
            StartDate = newStart
        }, _admin);

        // Sprint 2 moves to Feb 1, 3 weeks duration
        Assert.AreEqual(newStart, result.Sprints[1].StartDate);
        Assert.AreEqual(newStart.AddDays(21), result.Sprints[1].EndDate);

        // Sprint 3 cascades from Sprint 2 end
        Assert.AreEqual(newStart.AddDays(21), result.Sprints[2].StartDate);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Timeline View Component Logic (Step 30 — UI Computation)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void GetSprintStatusClass_Planning_ReturnsCorrectClass()
    {
        var result = TimelineView.GetSprintStatusClass(SprintStatus.Planning);
        Assert.AreEqual("sprint-planning", result);
    }

    [TestMethod]
    public void GetSprintStatusClass_Active_ReturnsCorrectClass()
    {
        var result = TimelineView.GetSprintStatusClass(SprintStatus.Active);
        Assert.AreEqual("sprint-active", result);
    }

    [TestMethod]
    public void GetSprintStatusClass_Completed_ReturnsCorrectClass()
    {
        var result = TimelineView.GetSprintStatusClass(SprintStatus.Completed);
        Assert.AreEqual("sprint-completed", result);
    }

    [TestMethod]
    public void GetProgressPercent_NoStoryPoints_ReturnsZero()
    {
        var sprint = CreateSprintDto(totalSp: 0, completedSp: 0);
        Assert.AreEqual(0, TimelineView.GetProgressPercent(sprint));
    }

    [TestMethod]
    public void GetProgressPercent_HalfComplete_Returns50()
    {
        var sprint = CreateSprintDto(totalSp: 10, completedSp: 5);
        Assert.AreEqual(50, TimelineView.GetProgressPercent(sprint));
    }

    [TestMethod]
    public void GetProgressPercent_FullyComplete_Returns100()
    {
        var sprint = CreateSprintDto(totalSp: 8, completedSp: 8);
        Assert.AreEqual(100, TimelineView.GetProgressPercent(sprint));
    }

    [TestMethod]
    public void GetProgressPercent_OverComplete_ClampsTo100()
    {
        var sprint = CreateSprintDto(totalSp: 10, completedSp: 15);
        Assert.AreEqual(100, TimelineView.GetProgressPercent(sprint));
    }

    [TestMethod]
    public void GetProgressPercent_PartialComplete_ReturnsCorrectDecimal()
    {
        var sprint = CreateSprintDto(totalSp: 3, completedSp: 1);
        var result = TimelineView.GetProgressPercent(sprint);
        Assert.IsTrue(result > 33 && result < 34, $"Expected ~33.33%, got {result}%");
    }

    [TestMethod]
    public void GetSprintTooltip_IncludesTitle()
    {
        var sprint = CreateSprintDto(title: "Sprint 5");
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("Sprint 5"));
    }

    [TestMethod]
    public void GetSprintTooltip_IncludesDateRange()
    {
        var sprint = CreateSprintDto(
            start: new DateTime(2026, 3, 2),
            end: new DateTime(2026, 3, 16));
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("Mar 2"), $"Tooltip missing start date: {tooltip}");
        Assert.IsTrue(tooltip.Contains("Mar 16"), $"Tooltip missing end date: {tooltip}");
    }

    [TestMethod]
    public void GetSprintTooltip_IncludesDuration()
    {
        var sprint = CreateSprintDto(durationWeeks: 3);
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("3 week"), $"Tooltip missing duration: {tooltip}");
    }

    [TestMethod]
    public void GetSprintTooltip_IncludesCardCount()
    {
        var sprint = CreateSprintDto(cardCount: 12);
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("12 cards"), $"Tooltip missing card count: {tooltip}");
    }

    [TestMethod]
    public void GetSprintTooltip_IncludesStoryPoints()
    {
        var sprint = CreateSprintDto(totalSp: 20, completedSp: 8);
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("8/20 SP"), $"Tooltip missing story points: {tooltip}");
    }

    [TestMethod]
    public void GetSprintTooltip_IncludesStatus()
    {
        var sprint = CreateSprintDto(status: SprintStatus.Active);
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("Active"), $"Tooltip missing status: {tooltip}");
    }

    [TestMethod]
    public void GetSprintTooltip_NoDates_StillWorks()
    {
        var sprint = CreateSprintDto(start: null, end: null);
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("Sprint 1"), $"Tooltip should still have title: {tooltip}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mode Guard Tests (Timeline only for Team boards)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateYearPlan_OnPersonalBoard_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.CreateYearPlanAsync(_personalBoard.Id, new CreateSprintPlanDto
            {
                StartDate = DateTime.UtcNow,
                SprintCount = 4,
                DefaultDurationWeeks = 2
            }, _admin));
    }

    [TestMethod]
    public async Task AdjustSprint_OnPersonalBoard_Throws()
    {
        // Manually create a sprint on personal board (bypassing service)
        var sprint = new Sprint
        {
            BoardId = _personalBoard.Id,
            Title = "Test Sprint",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _planningService.AdjustSprintAsync(sprint.Id, new AdjustSprintDto
            {
                DurationWeeks = 3
            }, _admin));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Timeline with Large Plan (Performance / 52-Sprint Year Plan)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetPlanOverview_52SprintYearPlan_ReturnsAllSprints()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 52,
            DefaultDurationWeeks = 1
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(52, overview.Sprints.Count);
        Assert.AreEqual(52, overview.TotalWeeks);
        Assert.AreEqual(start, overview.PlanStartDate);
        Assert.AreEqual(start.AddDays(364), overview.PlanEndDate); // 52 × 7 days
    }

    [TestMethod]
    public async Task GetPlanOverview_52SprintPlan_AllSprintsSequential()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 52,
            DefaultDurationWeeks = 1
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        for (var i = 0; i < overview.Sprints.Count - 1; i++)
        {
            var current = overview.Sprints[i];
            var next = overview.Sprints[i + 1];

            // Each sprint ends where the next begins
            Assert.AreEqual(current.EndDate, next.StartDate,
                $"Sprint {i + 1} end ({current.EndDate}) should equal Sprint {i + 2} start ({next.StartDate})");
        }
    }

    [TestMethod]
    public async Task AdjustSprint_MiddleOf52Plan_CascadesCorrectly()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 52,
            DefaultDurationWeeks = 1
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        // Adjust sprint 26 (middle) from 1 week to 3 weeks
        var sprint26 = overview.Sprints[25];
        var result = await _planningService.AdjustSprintAsync(sprint26.Id, new AdjustSprintDto
        {
            DurationWeeks = 3
        }, _admin);

        Assert.AreEqual(3, result.Sprints[25].DurationWeeks);

        // Subsequent sprints should be pushed out by 2 extra weeks
        var expectedStart27 = sprint26.StartDate!.Value.AddDays(21); // 3 weeks
        Assert.AreEqual(expectedStart27, result.Sprints[26].StartDate);

        // All subsequent sprints should still be sequential
        for (var i = 26; i < result.Sprints.Count - 1; i++)
        {
            Assert.AreEqual(result.Sprints[i].EndDate, result.Sprints[i + 1].StartDate,
                $"Sprint {i + 1} end should match Sprint {i + 2} start after cascade");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mixed Duration Plans
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AdjustSprint_MixedDurations_TotalWeeksUpdated()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(8, overview.TotalWeeks); // 4 × 2

        // Change Sprint 1 to 4 weeks, Sprint 3 to 1 week
        await _planningService.AdjustSprintAsync(overview.Sprints[0].Id, new AdjustSprintDto
        {
            DurationWeeks = 4
        }, _admin);

        overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        await _planningService.AdjustSprintAsync(overview.Sprints[2].Id, new AdjustSprintDto
        {
            DurationWeeks = 1
        }, _admin);

        overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        // Total: 4 + 2 + 1 + 2 = 9 weeks
        Assert.AreEqual(9, overview.TotalWeeks);
        Assert.AreEqual(4, overview.Sprints[0].DurationWeeks);
        Assert.AreEqual(2, overview.Sprints[1].DurationWeeks);
        Assert.AreEqual(1, overview.Sprints[2].DurationWeeks);
        Assert.AreEqual(2, overview.Sprints[3].DurationWeeks);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint Status Changes & Timeline Impact
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Timeline_SprintsReflectStatusChanges()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        Assert.IsTrue(overview.Sprints.All(s => s.Status == SprintStatus.Planning));

        // Start Sprint 1
        await _sprintService.StartSprintAsync(overview.Sprints[0].Id, _admin);

        overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(SprintStatus.Active, overview.Sprints[0].Status);
        Assert.AreEqual(SprintStatus.Planning, overview.Sprints[1].Status);
    }

    [TestMethod]
    public async Task Timeline_CompletedSprintShowsInOverview()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        // Start then complete Sprint 1
        await _sprintService.StartSprintAsync(overview.Sprints[0].Id, _admin);
        await _sprintService.CompleteSprintAsync(overview.Sprints[0].Id, _admin);

        overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(SprintStatus.Completed, overview.Sprints[0].Status);
        Assert.AreEqual(3, overview.Sprints.Count); // Still shows all sprints
    }

    // ═══════════════════════════════════════════════════════════════════
    // Story Point Tracking on Timeline
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Timeline_ShowsStoryPointProgress()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        }, _admin);

        // Create swimlanes
        var todoSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, _teamBoard.Id, "To Do");
        var doneSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, _teamBoard.Id, "Done");
        doneSwimlane.IsDone = true;
        _db.Update(doneSwimlane);
        await _db.SaveChangesAsync();

        // Create cards with story points
        var card1 = await TestHelpers.SeedCardAsync(_db, todoSwimlane.Id, _adminUserId, "Card 1");
        card1.StoryPoints = 5;
        _db.Update(card1);

        var card2 = await TestHelpers.SeedCardAsync(_db, doneSwimlane.Id, _adminUserId, "Card 2");
        card2.StoryPoints = 3;
        _db.Update(card2);
        await _db.SaveChangesAsync();

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        var sprint1 = overview.Sprints[0];

        // Add both cards to Sprint 1
        _db.Set<SprintCard>().AddRange(
            new SprintCard { SprintId = sprint1.Id, CardId = card1.Id },
            new SprintCard { SprintId = sprint1.Id, CardId = card2.Id }
        );
        await _db.SaveChangesAsync();

        overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);
        sprint1 = overview.Sprints[0];

        Assert.AreEqual(2, sprint1.CardCount);
        Assert.AreEqual(8, sprint1.TotalStoryPoints); // 5 + 3
        Assert.AreEqual(3, sprint1.CompletedStoryPoints); // Only card in "Done" swimlane
    }

    // ═══════════════════════════════════════════════════════════════════
    // Edge Cases
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AdjustSprint_MinDuration_1Week()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 4
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        var result = await _planningService.AdjustSprintAsync(overview.Sprints[0].Id, new AdjustSprintDto
        {
            DurationWeeks = 1
        }, _admin);

        Assert.AreEqual(1, result.Sprints[0].DurationWeeks);
        Assert.AreEqual(start.AddDays(7), result.Sprints[0].EndDate);
    }

    [TestMethod]
    public async Task AdjustSprint_MaxDuration_16Weeks()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        }, _admin);

        var overview = await _planningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        var result = await _planningService.AdjustSprintAsync(overview.Sprints[0].Id, new AdjustSprintDto
        {
            DurationWeeks = 16
        }, _admin);

        Assert.AreEqual(16, result.Sprints[0].DurationWeeks);
        Assert.AreEqual(start.AddDays(112), result.Sprints[0].EndDate); // 16 × 7
    }

    [TestMethod]
    public async Task CreateYearPlan_SingleSprint_Works()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var result = await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 1,
            DefaultDurationWeeks = 16
        }, _admin);

        Assert.AreEqual(1, result.Sprints.Count);
        Assert.AreEqual(16, result.TotalWeeks);
        Assert.AreEqual("Sprint 1", result.Sprints[0].Title);
    }

    [TestMethod]
    public async Task CreateYearPlan_MaxSprints_104_Works()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var result = await _planningService.CreateYearPlanAsync(_teamBoard.Id, new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 104,
            DefaultDurationWeeks = 1
        }, _admin);

        Assert.AreEqual(104, result.Sprints.Count);
        Assert.AreEqual(104, result.TotalWeeks);

        // Verify sequential ordering
        for (var i = 0; i < 104; i++)
        {
            Assert.AreEqual(i + 1, result.Sprints[i].PlannedOrder);
            Assert.AreEqual($"Sprint {i + 1}", result.Sprints[i].Title);
        }
    }

    [TestMethod]
    public void GetProgressPercent_NegativeTotalPoints_ReturnsZero()
    {
        // Edge case — shouldn't happen but guard against it
        var sprint = CreateSprintDto(totalSp: -1, completedSp: 0);
        Assert.AreEqual(0, TimelineView.GetProgressPercent(sprint));
    }

    [TestMethod]
    public void GetSprintStatusClass_UnknownStatus_ReturnsEmpty()
    {
        var result = TimelineView.GetSprintStatusClass((SprintStatus)999);
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void GetSprintTooltip_ZeroCards_ShowsZero()
    {
        var sprint = CreateSprintDto(cardCount: 0, totalSp: 0, completedSp: 0);
        var tooltip = TimelineView.GetSprintTooltip(sprint);
        Assert.IsTrue(tooltip.Contains("0 cards"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static SprintDto CreateSprintDto(
        string title = "Sprint 1",
        SprintStatus status = SprintStatus.Planning,
        DateTime? start = null,
        DateTime? end = null,
        int? durationWeeks = null,
        int cardCount = 0,
        int totalSp = 0,
        int completedSp = 0,
        int? plannedOrder = 1)
    {
        return new SprintDto
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = title,
            Status = status,
            StartDate = start,
            EndDate = end,
            DurationWeeks = durationWeeks,
            CardCount = cardCount,
            TotalStoryPoints = totalSp,
            CompletedStoryPoints = completedSp,
            PlannedOrder = plannedOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
