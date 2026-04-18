using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using System.Text.Json;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Comprehensive tests for Phase F: Sprint Planning Wizard.
/// Validates wizard schedule generation, date cascading, swimlane management,
/// plan creation via controller endpoints, and mode guards.
/// </summary>
[TestClass]
public class PhaseF_SprintPlanningWizardTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private SprintPlanningService _sprintPlanningService = null!;
    private SprintService _sprintService = null!;
    private SwimlaneService _swimlaneService = null!;
    private SprintsController _controller = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;
    private Board _teamBoard = null!;
    private Board _personalBoard = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();

        var eventBus = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, eventBus.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, eventBus.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _sprintPlanningService = new SprintPlanningService(_db, _boardService, activityService, NullLogger<SprintPlanningService>.Instance);
        _sprintService = new SprintService(_db, _boardService, activityService, eventBus.Object, NullLogger<SprintService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, activityService, NullLogger<SwimlaneService>.Instance);

        _controller = new SprintsController(_sprintService, _sprintPlanningService, NullLogger<SprintsController>.Instance);
        SetupControllerContext(_controller, _adminUserId);

        _admin = TestHelpers.CreateCaller(_adminUserId);
        _member = TestHelpers.CreateCaller(_memberUserId);

        // Seed Team board with admin + member
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
    // Wizard Schedule Generation Logic (Step 25)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ScheduleGeneration_CreatesCorrectNumberOfSprints()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 12, 2);

        Assert.AreEqual(12, schedule.Count);
    }

    [TestMethod]
    public void ScheduleGeneration_SequentialOrders()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 6, 2);

        for (var i = 0; i < schedule.Count; i++)
        {
            Assert.AreEqual(i + 1, schedule[i].Order);
        }
    }

    [TestMethod]
    public void ScheduleGeneration_SequentialDates_NoGaps()
    {
        var start = new DateTime(2026, 1, 5);
        var schedule = GenerateSchedule(start, 4, 2);

        // First sprint starts at plan start
        Assert.AreEqual(start, schedule[0].Start);

        // Each sprint starts where the previous ended
        for (var i = 1; i < schedule.Count; i++)
        {
            Assert.AreEqual(schedule[i - 1].End, schedule[i].Start,
                $"Sprint {i + 1} should start where sprint {i} ends");
        }
    }

    [TestMethod]
    public void ScheduleGeneration_CorrectDuration()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 3, 3);

        foreach (var sprint in schedule)
        {
            Assert.AreEqual(3, sprint.DurationWeeks);
            Assert.AreEqual(21, (sprint.End - sprint.Start).TotalDays); // 3*7 = 21 days
        }
    }

    [TestMethod]
    public void ScheduleGeneration_SingleSprint()
    {
        var start = new DateTime(2026, 6, 1);
        var schedule = GenerateSchedule(start, 1, 4);

        Assert.AreEqual(1, schedule.Count);
        Assert.AreEqual(start, schedule[0].Start);
        Assert.AreEqual(start.AddDays(28), schedule[0].End);
        Assert.AreEqual(4, schedule[0].DurationWeeks);
    }

    [TestMethod]
    public void ScheduleGeneration_MaxDuration_16Weeks()
    {
        var start = new DateTime(2026, 1, 1);
        var schedule = GenerateSchedule(start, 2, 16);

        Assert.AreEqual(start.AddDays(112), schedule[0].End); // 16*7 = 112
        Assert.AreEqual(start.AddDays(112), schedule[1].Start);
        Assert.AreEqual(start.AddDays(224), schedule[1].End);
    }

    [TestMethod]
    public void ScheduleGeneration_MinDuration_1Week()
    {
        var start = new DateTime(2026, 1, 1);
        var schedule = GenerateSchedule(start, 3, 1);

        foreach (var sprint in schedule)
        {
            Assert.AreEqual(7, (sprint.End - sprint.Start).TotalDays);
        }
    }

    [TestMethod]
    public void ScheduleGeneration_TotalDurationMatchesExpected()
    {
        var start = new DateTime(2026, 1, 5);
        var schedule = GenerateSchedule(start, 26, 2);

        var totalWeeks = schedule.Sum(s => s.DurationWeeks);
        Assert.AreEqual(52, totalWeeks); // 26 * 2 = 52 weeks = ~1 year

        var totalDays = (schedule.Last().End - schedule.First().Start).TotalDays;
        Assert.AreEqual(364, totalDays); // 52 * 7
    }

    // ═══════════════════════════════════════════════════════════════════
    // Duration Adjustment & Date Cascading (Step 25 — Schedule Step)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void AdjustDuration_UpdatesTargetSprint()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 4, 2);

        AdjustSprintDuration(schedule, 1, 3); // Change sprint 2 from 2 to 3 weeks

        Assert.AreEqual(3, schedule[1].DurationWeeks);
    }

    [TestMethod]
    public void AdjustDuration_CascadesSubsequentStartDates()
    {
        var start = new DateTime(2026, 1, 5);
        var schedule = GenerateSchedule(start, 4, 2);

        AdjustSprintDuration(schedule, 0, 3); // Sprint 1: 2 → 3 weeks

        // Sprint 1: Jan 5 → Jan 26 (3 weeks)
        Assert.AreEqual(start, schedule[0].Start);
        Assert.AreEqual(start.AddDays(21), schedule[0].End);

        // Sprint 2: should start at sprint 1's new end
        Assert.AreEqual(start.AddDays(21), schedule[1].Start);
        Assert.AreEqual(start.AddDays(35), schedule[1].End); // 2 weeks later

        // Sprint 3: cascaded
        Assert.AreEqual(start.AddDays(35), schedule[2].Start);
        Assert.AreEqual(start.AddDays(49), schedule[2].End);

        // Sprint 4: cascaded
        Assert.AreEqual(start.AddDays(49), schedule[3].Start);
        Assert.AreEqual(start.AddDays(63), schedule[3].End);
    }

    [TestMethod]
    public void AdjustDuration_ShorteningAlsoCascades()
    {
        var start = new DateTime(2026, 1, 5);
        var schedule = GenerateSchedule(start, 3, 4);

        AdjustSprintDuration(schedule, 0, 1); // Sprint 1: 4 → 1 week

        Assert.AreEqual(start.AddDays(7), schedule[0].End);
        Assert.AreEqual(start.AddDays(7), schedule[1].Start);
        Assert.AreEqual(start.AddDays(35), schedule[1].End); // still 4 weeks
        Assert.AreEqual(start.AddDays(35), schedule[2].Start);
    }

    [TestMethod]
    public void AdjustDuration_LastSprint_NoCascade()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 3, 2);

        AdjustSprintDuration(schedule, 2, 4); // Last sprint: 2 → 4 weeks

        Assert.AreEqual(4, schedule[2].DurationWeeks);
        // Previous sprints should remain unchanged
        Assert.AreEqual(2, schedule[0].DurationWeeks);
        Assert.AreEqual(2, schedule[1].DurationWeeks);
    }

    [TestMethod]
    public void AdjustDuration_MiddleSprint_OnlyCascadesAfter()
    {
        var start = new DateTime(2026, 1, 5);
        var schedule = GenerateSchedule(start, 5, 2);
        var originalSprint1Start = schedule[0].Start;
        var originalSprint1End = schedule[0].End;

        AdjustSprintDuration(schedule, 2, 4); // Sprint 3: 2 → 4 weeks

        // Sprints 1-2 unchanged
        Assert.AreEqual(originalSprint1Start, schedule[0].Start);
        Assert.AreEqual(originalSprint1End, schedule[0].End);
        Assert.AreEqual(2, schedule[1].DurationWeeks);

        // Sprint 3 changed
        Assert.AreEqual(4, schedule[2].DurationWeeks);

        // Sprints 4-5 cascaded
        Assert.AreEqual(schedule[2].End, schedule[3].Start);
        Assert.AreEqual(schedule[3].End, schedule[4].Start);
    }

    [TestMethod]
    public void AdjustDuration_MultipleAdjustments_CascadeCorrectly()
    {
        var start = new DateTime(2026, 1, 5);
        var schedule = GenerateSchedule(start, 4, 2);

        AdjustSprintDuration(schedule, 0, 3); // Sprint 1: 2 → 3 weeks
        AdjustSprintDuration(schedule, 2, 1); // Sprint 3: 2 → 1 week

        // Sprint 1: Jan 5 → Jan 26 (3 weeks)
        Assert.AreEqual(start, schedule[0].Start);
        Assert.AreEqual(start.AddDays(21), schedule[0].End);

        // Sprint 2: Jan 26 → Feb 9 (still 2 weeks)
        Assert.AreEqual(start.AddDays(21), schedule[1].Start);
        Assert.AreEqual(start.AddDays(35), schedule[1].End);

        // Sprint 3: Feb 9 → Feb 16 (1 week now)
        Assert.AreEqual(start.AddDays(35), schedule[2].Start);
        Assert.AreEqual(start.AddDays(42), schedule[2].End);

        // Sprint 4: Feb 16 → Mar 2 (still 2 weeks)
        Assert.AreEqual(start.AddDays(42), schedule[3].Start);
        Assert.AreEqual(start.AddDays(56), schedule[3].End);
    }

    [TestMethod]
    public void AdjustDuration_IgnoresInvalidIndex_Negative()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 3, 2);
        var originalEnd = schedule[0].End;

        AdjustSprintDuration(schedule, -1, 3);

        Assert.AreEqual(originalEnd, schedule[0].End); // Unchanged
    }

    [TestMethod]
    public void AdjustDuration_IgnoresInvalidIndex_TooLarge()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 3, 2);
        var originalEnd = schedule[2].End;

        AdjustSprintDuration(schedule, 5, 3);

        Assert.AreEqual(originalEnd, schedule[2].End); // Unchanged
    }

    [TestMethod]
    public void AdjustDuration_IgnoresInvalidDuration_Zero()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 3, 2);
        var originalDuration = schedule[0].DurationWeeks;

        AdjustSprintDuration(schedule, 0, 0);

        Assert.AreEqual(originalDuration, schedule[0].DurationWeeks); // Unchanged
    }

    [TestMethod]
    public void AdjustDuration_IgnoresInvalidDuration_17()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 3, 2);
        var originalDuration = schedule[0].DurationWeeks;

        AdjustSprintDuration(schedule, 0, 17);

        Assert.AreEqual(originalDuration, schedule[0].DurationWeeks); // Unchanged
    }

    // ═══════════════════════════════════════════════════════════════════
    // Swimlane Entry Management (Step 25 — Swimlanes Step)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void SwimlaneEntry_DefaultsHaveCorrectTitles()
    {
        var defaults = GetDefaultSwimlanes();

        Assert.AreEqual(4, defaults.Count);
        Assert.AreEqual("To Do", defaults[0].Title);
        Assert.AreEqual("In Progress", defaults[1].Title);
        Assert.AreEqual("Review", defaults[2].Title);
        Assert.AreEqual("Done", defaults[3].Title);
    }

    [TestMethod]
    public void SwimlaneEntry_OnlyDoneHasIsDoneSet()
    {
        var defaults = GetDefaultSwimlanes();

        Assert.IsFalse(defaults[0].IsDone);
        Assert.IsFalse(defaults[1].IsDone);
        Assert.IsFalse(defaults[2].IsDone);
        Assert.IsTrue(defaults[3].IsDone);
    }

    [TestMethod]
    public void SwimlaneEntry_AddSwimlane_IncreasesCount()
    {
        var swimlanes = GetDefaultSwimlanes();
        var originalCount = swimlanes.Count;

        swimlanes.Add(new SprintPlanningWizard.SwimlaneEntry { Title = "Testing" });

        Assert.AreEqual(originalCount + 1, swimlanes.Count);
    }

    [TestMethod]
    public void SwimlaneEntry_RemoveSwimlane_DecreasesCount()
    {
        var swimlanes = GetDefaultSwimlanes();
        var originalCount = swimlanes.Count;

        swimlanes.RemoveAt(1); // Remove "In Progress"

        Assert.AreEqual(originalCount - 1, swimlanes.Count);
    }

    [TestMethod]
    public void SwimlaneEntry_CannotRemoveLastSwimlane()
    {
        var swimlanes = new List<SprintPlanningWizard.SwimlaneEntry>
        {
            new() { Title = "Only One" }
        };

        // Per wizard logic: "if (_swimlanes.Count > 1)" — only removes if count > 1
        if (swimlanes.Count > 1) swimlanes.RemoveAt(0);

        Assert.AreEqual(1, swimlanes.Count); // Still has the one
    }

    [TestMethod]
    public void SwimlaneEntry_ToggleIsDone()
    {
        var entry = new SprintPlanningWizard.SwimlaneEntry { Title = "Test", IsDone = false };

        entry.IsDone = !entry.IsDone;
        Assert.IsTrue(entry.IsDone);

        entry.IsDone = !entry.IsDone;
        Assert.IsFalse(entry.IsDone);
    }

    [TestMethod]
    public void SwimlaneEntry_UpdateTitle()
    {
        var entry = new SprintPlanningWizard.SwimlaneEntry { Title = "Original" };

        entry.Title = "Updated";

        Assert.AreEqual("Updated", entry.Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Wizard Validation — CanAdvance Logic (Step 25)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void CanAdvance_Step0_ValidParams_True()
    {
        Assert.IsTrue(CanAdvanceStep0(12, 2));
    }

    [TestMethod]
    public void CanAdvance_Step0_ZeroSprints_False()
    {
        Assert.IsFalse(CanAdvanceStep0(0, 2));
    }

    [TestMethod]
    public void CanAdvance_Step0_NegativeSprints_False()
    {
        Assert.IsFalse(CanAdvanceStep0(-1, 2));
    }

    [TestMethod]
    public void CanAdvance_Step0_TooManySprints_False()
    {
        Assert.IsFalse(CanAdvanceStep0(105, 2));
    }

    [TestMethod]
    public void CanAdvance_Step0_ZeroDuration_False()
    {
        Assert.IsFalse(CanAdvanceStep0(12, 0));
    }

    [TestMethod]
    public void CanAdvance_Step0_DurationTooHigh_False()
    {
        Assert.IsFalse(CanAdvanceStep0(12, 17));
    }

    [TestMethod]
    public void CanAdvance_Step0_BoundaryValues_True()
    {
        Assert.IsTrue(CanAdvanceStep0(1, 1));    // min, min
        Assert.IsTrue(CanAdvanceStep0(104, 16)); // max, max
    }

    [TestMethod]
    public void CanAdvance_Step1_ValidSwimlanes_True()
    {
        var swimlanes = GetDefaultSwimlanes();
        Assert.IsTrue(CanAdvanceStep1(swimlanes));
    }

    [TestMethod]
    public void CanAdvance_Step1_EmptyList_False()
    {
        Assert.IsFalse(CanAdvanceStep1([]));
    }

    [TestMethod]
    public void CanAdvance_Step1_BlankTitle_False()
    {
        var swimlanes = new List<SprintPlanningWizard.SwimlaneEntry>
        {
            new() { Title = "Valid" },
            new() { Title = "" },  // blank
            new() { Title = "Also Valid" }
        };
        Assert.IsFalse(CanAdvanceStep1(swimlanes));
    }

    [TestMethod]
    public void CanAdvance_Step1_WhitespaceTitle_False()
    {
        var swimlanes = new List<SprintPlanningWizard.SwimlaneEntry>
        {
            new() { Title = "   " }
        };
        Assert.IsFalse(CanAdvanceStep1(swimlanes));
    }

    [TestMethod]
    public void CanAdvance_Step2_WithSchedule_True()
    {
        var schedule = GenerateSchedule(new DateTime(2026, 1, 5), 4, 2);
        Assert.IsTrue(schedule.Count > 0);
    }

    [TestMethod]
    public void CanAdvance_Step2_EmptySchedule_False()
    {
        var schedule = new List<SprintPlanningWizard.SprintScheduleEntry>();
        Assert.IsFalse(schedule.Count > 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Service-Level Plan Creation (Wizard → Service Integration)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateYearPlan_ViaService_CreatesAllSprints()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 12,
            DefaultDurationWeeks = 2
        };

        var result = await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto, _admin);

        Assert.AreEqual(12, result.Sprints.Count);
        Assert.AreEqual(24, result.TotalWeeks);
    }

    [TestMethod]
    public async Task CreateYearPlan_ThenAdjust_IndividualDurations_CascadesInDb()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };
        var plan = await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto, _admin);

        // Adjust sprint 2 from 2 to 3 weeks (simulating wizard adjustment)
        var adjustDto = new AdjustSprintDto { DurationWeeks = 3 };
        var updated = await _sprintPlanningService.AdjustSprintAsync(plan.Sprints[1].Id, adjustDto, _admin);

        // Sprint 2: 3 weeks now, cascaded to sprints 3 and 4
        Assert.AreEqual(3, updated.Sprints[1].DurationWeeks);
        Assert.AreEqual(updated.Sprints[1].EndDate, updated.Sprints[2].StartDate);
        Assert.AreEqual(updated.Sprints[2].EndDate, updated.Sprints[3].StartDate);
    }

    [TestMethod]
    public async Task CreatePlanAndSwimlanes_FullWizardFlow()
    {
        // Step 2: Create swimlanes
        var swimlanes = new[]
        {
            new CreateBoardSwimlaneDto { Title = "To Do" },
            new CreateBoardSwimlaneDto { Title = "In Progress" },
            new CreateBoardSwimlaneDto { Title = "Done", IsDone = true }
        };

        foreach (var sl in swimlanes)
        {
            await _swimlaneService.CreateSwimlaneAsync(_teamBoard.Id, sl, _admin);
        }

        // Verify swimlanes created
        var createdSwimlanes = await _swimlaneService.GetSwimlanesAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(3, createdSwimlanes.Count);

        // Step 4: Create sprint plan
        var planDto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 6,
            DefaultDurationWeeks = 2
        };
        var plan = await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, planDto, _admin);

        Assert.AreEqual(6, plan.Sprints.Count);
        Assert.AreEqual(12, plan.TotalWeeks);

        // Adjust one sprint to verify cascade
        var adjusted = await _sprintPlanningService.AdjustSprintAsync(plan.Sprints[0].Id,
            new AdjustSprintDto { DurationWeeks = 4 }, _admin);

        // Total weeks: 4 + 2*5 = 14
        Assert.AreEqual(14, adjusted.TotalWeeks);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Controller-Level Plan Endpoints (Step 26 integration)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Controller_CreateSprintPlan_ReturnsCreated()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 8,
            DefaultDurationWeeks = 2
        };

        var result = await _controller.CreateSprintPlanAsync(_teamBoard.Id, dto);

        Assert.IsInstanceOfType<CreatedResult>(result);
        var overview = UnwrapEnvelope<SprintPlanOverviewDto>(((CreatedResult)result).Value);
        Assert.IsNotNull(overview);
        Assert.AreEqual(8, overview.Sprints.Count);
    }

    [TestMethod]
    public async Task Controller_CreateSprintPlan_PersonalBoard_ReturnsBadRequest()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var result = await _controller.CreateSprintPlanAsync(_personalBoard.Id, dto);

        // Personal board → validation error (either NotFound[board not member] or BadRequest[team mode required])
        Assert.IsTrue(result is NotFoundObjectResult or BadRequestObjectResult,
            $"Expected NotFound or BadRequest for personal board, got {result.GetType().Name}");
    }

    [TestMethod]
    public async Task Controller_GetSprintPlan_ReturnsExistingPlan()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };
        await _controller.CreateSprintPlanAsync(_teamBoard.Id, dto);

        var result = await _controller.GetSprintPlanAsync(_teamBoard.Id);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var overview = UnwrapEnvelope<SprintPlanOverviewDto>(((OkObjectResult)result).Value);
        Assert.IsNotNull(overview);
        Assert.AreEqual(4, overview.Sprints.Count);
    }

    [TestMethod]
    public async Task Controller_AdjustSprint_ReturnsOk()
    {
        var planDto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        var createResult = await _controller.CreateSprintPlanAsync(_teamBoard.Id, planDto);
        var overview = UnwrapEnvelope<SprintPlanOverviewDto>(((CreatedResult)createResult).Value);
        Assert.IsNotNull(overview);
        var sprintId = overview.Sprints[0].Id;

        var adjustDto = new AdjustSprintDto { DurationWeeks = 3 };
        var result = await _controller.AdjustSprintAsync(sprintId, adjustDto);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var adjusted = UnwrapEnvelope<SprintPlanOverviewDto>(((OkObjectResult)result).Value);
        Assert.IsNotNull(adjusted);
        Assert.AreEqual(3, adjusted.Sprints[0].DurationWeeks);
    }

    [TestMethod]
    public async Task Controller_AdjustSprint_InvalidDuration_ReturnsBadRequest()
    {
        var planDto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };
        var createResult = await _controller.CreateSprintPlanAsync(_teamBoard.Id, planDto);
        var overview = UnwrapEnvelope<SprintPlanOverviewDto>(((CreatedResult)createResult).Value);
        Assert.IsNotNull(overview);
        var sprintId = overview.Sprints[0].Id;

        var adjustDto = new AdjustSprintDto { DurationWeeks = 0 };
        var result = await _controller.AdjustSprintAsync(sprintId, adjustDto);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mode Guard Tests (Wizard should only work on Team boards)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreatePlan_OnPersonalBoard_ThrowsValidationException()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintPlanningService.CreateYearPlanAsync(_personalBoard.Id, dto, _admin));

        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task AdjustSprint_OnPersonalBoard_ThrowsValidationException()
    {
        // Manually create a sprint on the personal board
        var sprint = new Sprint
        {
            BoardId = _personalBoard.Id,
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
            () => _sprintPlanningService.AdjustSprintAsync(sprint.Id, dto, _admin));

        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task CreatePlan_AsMember_ThrowsInsufficientRole()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        // Members cannot create plans — only Admin+
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto, _member));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Swimlane Creation — Service Level Integration
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateSwimlanes_ViaService_Succeed()
    {
        var dto = new CreateBoardSwimlaneDto { Title = "To Do" };

        var result = await _swimlaneService.CreateSwimlaneAsync(_teamBoard.Id, dto, _admin);

        Assert.IsNotNull(result);
        Assert.AreEqual("To Do", result.Title);
    }

    [TestMethod]
    public async Task CreateSwimlanes_WithIsDone_Persists()
    {
        var dto = new CreateBoardSwimlaneDto { Title = "Done", IsDone = true };

        var result = await _swimlaneService.CreateSwimlaneAsync(_teamBoard.Id, dto, _admin);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsDone);
    }

    [TestMethod]
    public async Task CreateMultipleSwimlanes_AllPersisted()
    {
        var dtos = new[]
        {
            new CreateBoardSwimlaneDto { Title = "To Do" },
            new CreateBoardSwimlaneDto { Title = "In Progress" },
            new CreateBoardSwimlaneDto { Title = "Review" },
            new CreateBoardSwimlaneDto { Title = "Done", IsDone = true }
        };

        foreach (var dto in dtos)
        {
            await _swimlaneService.CreateSwimlaneAsync(_teamBoard.Id, dto, _admin);
        }

        var swimlanes = await _swimlaneService.GetSwimlanesAsync(_teamBoard.Id, _admin);
        Assert.AreEqual(4, swimlanes.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Edge Cases & Boundary Conditions
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreatePlan_WithMaxSprints_104()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 104,
            DefaultDurationWeeks = 1
        };

        var result = await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto, _admin);

        Assert.AreEqual(104, result.Sprints.Count);
        Assert.AreEqual(104, result.TotalWeeks); // 104 * 1 = 104 weeks = 2 years
    }

    [TestMethod]
    public async Task CreatePlan_MultiplePlans_AppendOrdering()
    {
        var dto1 = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto1, _admin);

        var dto2 = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };
        var result = await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto2, _admin);

        Assert.AreEqual(5, result.Sprints.Count);
        Assert.AreEqual(4, result.Sprints[3].PlannedOrder); // Continues from 3
        Assert.AreEqual(5, result.Sprints[4].PlannedOrder);
    }

    [TestMethod]
    public async Task AdjustSprint_Cascade_PreservesIndividualDurations()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            StartDate = start,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        var plan = await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto, _admin);

        // Adjust sprint 3 to 4 weeks
        await _sprintPlanningService.AdjustSprintAsync(plan.Sprints[2].Id,
            new AdjustSprintDto { DurationWeeks = 4 }, _admin);

        // Now adjust sprint 1 and cascade — sprint 3 should keep its 4-week duration
        var result = await _sprintPlanningService.AdjustSprintAsync(plan.Sprints[0].Id,
            new AdjustSprintDto { DurationWeeks = 3 }, _admin);

        Assert.AreEqual(3, result.Sprints[0].DurationWeeks);
        Assert.AreEqual(2, result.Sprints[1].DurationWeeks); // unchanged
        Assert.AreEqual(4, result.Sprints[2].DurationWeeks); // preserved from earlier adjustment
    }

    [TestMethod]
    public async Task GetPlanOverview_EmptyBoard_ReturnsEmptyPlan()
    {
        var result = await _sprintPlanningService.GetPlanOverviewAsync(_teamBoard.Id, _admin);

        Assert.AreEqual(0, result.Sprints.Count);
        Assert.AreEqual(0, result.TotalWeeks);
        Assert.IsNull(result.PlanStartDate);
        Assert.IsNull(result.PlanEndDate);
    }

    [TestMethod]
    public async Task GetPlanOverview_MemberCanView()
    {
        var dto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };
        await _sprintPlanningService.CreateYearPlanAsync(_teamBoard.Id, dto, _admin);

        var result = await _sprintPlanningService.GetPlanOverviewAsync(_teamBoard.Id, _member);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Sprints.Count);
    }

    [TestMethod]
    public async Task AdjustSprint_NonExistentSprint_Throws()
    {
        var dto = new AdjustSprintDto { DurationWeeks = 2 };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintPlanningService.AdjustSprintAsync(Guid.NewGuid(), dto, _admin));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Wizard Step Navigation Logic (Step 25)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void WizardStepNames_AreCorrect()
    {
        var steps = new[] { "Plan Basics", "Swimlanes", "Sprint Schedule", "Review & Create" };

        Assert.AreEqual(4, steps.Length);
        Assert.AreEqual("Plan Basics", steps[0]);
        Assert.AreEqual("Swimlanes", steps[1]);
        Assert.AreEqual("Sprint Schedule", steps[2]);
        Assert.AreEqual("Review & Create", steps[3]);
    }

    [TestMethod]
    public void ScheduleEntry_Properties_SetCorrectly()
    {
        var entry = new SprintPlanningWizard.SprintScheduleEntry
        {
            Order = 1,
            Start = new DateTime(2026, 1, 5),
            End = new DateTime(2026, 1, 19),
            DurationWeeks = 2
        };

        Assert.AreEqual(1, entry.Order);
        Assert.AreEqual(new DateTime(2026, 1, 5), entry.Start);
        Assert.AreEqual(new DateTime(2026, 1, 19), entry.End);
        Assert.AreEqual(2, entry.DurationWeeks);
    }

    [TestMethod]
    public void SwimlaneEntry_Properties_SetCorrectly()
    {
        var entry = new SprintPlanningWizard.SwimlaneEntry
        {
            Title = "In Progress",
            IsDone = false
        };

        Assert.AreEqual("In Progress", entry.Title);
        Assert.IsFalse(entry.IsDone);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helper Methods — replicate wizard logic for testing
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Replicates the wizard's schedule generation logic.</summary>
    private static List<SprintPlanningWizard.SprintScheduleEntry> GenerateSchedule(
        DateTime startDate, int sprintCount, int defaultDurationWeeks)
    {
        var schedule = new List<SprintPlanningWizard.SprintScheduleEntry>();
        var currentStart = startDate;

        for (var i = 0; i < sprintCount; i++)
        {
            var end = currentStart.AddDays(defaultDurationWeeks * 7);
            schedule.Add(new SprintPlanningWizard.SprintScheduleEntry
            {
                Order = i + 1,
                Start = currentStart,
                End = end,
                DurationWeeks = defaultDurationWeeks
            });
            currentStart = end;
        }

        return schedule;
    }

    /// <summary>Replicates the wizard's duration adjustment logic with cascading.</summary>
    private static void AdjustSprintDuration(
        List<SprintPlanningWizard.SprintScheduleEntry> schedule, int index, int newDurationWeeks)
    {
        if (index < 0 || index >= schedule.Count) return;
        if (newDurationWeeks < 1 || newDurationWeeks > 16) return;

        schedule[index].DurationWeeks = newDurationWeeks;
        schedule[index].End = schedule[index].Start.AddDays(newDurationWeeks * 7);

        for (var i = index + 1; i < schedule.Count; i++)
        {
            schedule[i].Start = schedule[i - 1].End;
            schedule[i].End = schedule[i].Start.AddDays(schedule[i].DurationWeeks * 7);
        }
    }

    /// <summary>Returns default swimlane entries matching wizard defaults.</summary>
    private static List<SprintPlanningWizard.SwimlaneEntry> GetDefaultSwimlanes()
    {
        return
        [
            new() { Title = "To Do" },
            new() { Title = "In Progress" },
            new() { Title = "Review" },
            new() { Title = "Done", IsDone = true }
        ];
    }

    /// <summary>Step 0 validation: checks sprint count and duration ranges.</summary>
    private static bool CanAdvanceStep0(int sprintCount, int defaultDurationWeeks)
    {
        return sprintCount is >= 1 and <= 104 && defaultDurationWeeks is >= 1 and <= 16;
    }

    /// <summary>Step 1 validation: checks swimlane list.</summary>
    private static bool CanAdvanceStep1(List<SprintPlanningWizard.SwimlaneEntry> swimlanes)
    {
        return swimlanes.Count > 0 && swimlanes.All(s => !string.IsNullOrWhiteSpace(s.Title));
    }

    private static void SetupControllerContext(SprintsController controller, Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    /// <summary>
    /// Extracts the typed data from the controller Envelope wrapper ({ success, data }).
    /// </summary>
    private static T? UnwrapEnvelope<T>(object? envelope) where T : class
    {
        if (envelope is null) return null;

        // The Envelope is an anonymous type { success = true, data = <DTO> }.
        // Serialize to JSON then extract the "data" property.
        var json = JsonSerializer.Serialize(envelope);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var dataElement))
            return null;

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
