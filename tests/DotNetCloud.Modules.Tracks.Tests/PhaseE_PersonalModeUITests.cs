using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Comprehensive tests for Phase E: Personal Mode Simplification.
/// Validates board mode creation, mode-aware service behavior, mode guards,
/// and controller-level mode handling.
/// </summary>
[TestClass]
public class PhaseE_PersonalModeUITests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private CardService _cardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private SprintService _sprintService = null!;
    private SprintPlanningService _sprintPlanningService = null!;
    private ReviewSessionService _reviewSessionService = null!;
    private PokerService _pokerService = null!;
    private TeamService _teamService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private NullTracksRealtimeService _realtimeService = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _realtimeService = new NullTracksRealtimeService();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        _teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, _eventBusMock.Object, activityService, _teamService, NullLogger<BoardService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _sprintService = new SprintService(_db, _boardService, activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _sprintPlanningService = new SprintPlanningService(_db, _boardService, activityService, NullLogger<SprintPlanningService>.Instance);
        _pokerService = new PokerService(_db, _boardService, activityService, _realtimeService, NullLogger<PokerService>.Instance);
        _reviewSessionService = new ReviewSessionService(_db, _boardService, _pokerService, _realtimeService, NullLogger<ReviewSessionService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ═══════════════════════════════════════════════════════════════════
    // Board Creation with Mode (Step 22)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateBoard_PersonalMode_DefaultBehavior()
    {
        var dto = new CreateBoardDto { Title = "My Personal Board" };

        var result = await _boardService.CreateBoardAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(BoardMode.Personal, result.Mode);
        Assert.IsNull(result.TeamId);
    }

    [TestMethod]
    public async Task CreateBoard_ExplicitPersonalMode_SetsPersonal()
    {
        var dto = new CreateBoardDto
        {
            Title = "Explicit Personal",
            Mode = BoardMode.Personal
        };

        var result = await _boardService.CreateBoardAsync(dto, _caller);

        Assert.AreEqual(BoardMode.Personal, result.Mode);
    }

    [TestMethod]
    public async Task CreateBoard_TeamMode_SetsTeam()
    {
        var dto = new CreateBoardDto
        {
            Title = "Team Board",
            Mode = BoardMode.Team
        };

        var result = await _boardService.CreateBoardAsync(dto, _caller);

        Assert.AreEqual(BoardMode.Team, result.Mode);
    }

    [TestMethod]
    public async Task CreateBoard_TeamModeWithColor_PersistsBoth()
    {
        var dto = new CreateBoardDto
        {
            Title = "Colorful Team",
            Color = "#ef4444",
            Mode = BoardMode.Team
        };

        var result = await _boardService.CreateBoardAsync(dto, _caller);

        Assert.AreEqual(BoardMode.Team, result.Mode);
        Assert.AreEqual("#ef4444", result.Color);
    }

    [TestMethod]
    public async Task CreateBoard_PersonalModeIgnoresTeamId_WhenNotSet()
    {
        var dto = new CreateBoardDto
        {
            Title = "Personal Board",
            Mode = BoardMode.Personal,
            TeamId = null
        };

        var result = await _boardService.CreateBoardAsync(dto, _caller);

        Assert.AreEqual(BoardMode.Personal, result.Mode);
        Assert.IsNull(result.TeamId);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mode Persistence and Retrieval
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetBoard_ReturnsModeInDto()
    {
        var dto = new CreateBoardDto { Title = "Check Mode", Mode = BoardMode.Team };
        var created = await _boardService.CreateBoardAsync(dto, _caller);

        var retrieved = await _boardService.GetBoardAsync(created.Id, _caller);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(BoardMode.Team, retrieved.Mode);
    }

    [TestMethod]
    public async Task GetBoard_PersonalMode_ReturnedCorrectly()
    {
        var dto = new CreateBoardDto { Title = "Personal Check" };
        var created = await _boardService.CreateBoardAsync(dto, _caller);

        var retrieved = await _boardService.GetBoardAsync(created.Id, _caller);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(BoardMode.Personal, retrieved.Mode);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mode-Filtered Board Listing (Step 23 backend support)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ListBoards_FilterByPersonalMode_OnlyPersonal()
    {
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P1", Mode = BoardMode.Personal }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P2", Mode = BoardMode.Personal }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "T1", Mode = BoardMode.Team }, _caller);

        var results = await _boardService.ListBoardsAsync(_caller, modeFilter: BoardMode.Personal);

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(b => b.Mode == BoardMode.Personal));
    }

    [TestMethod]
    public async Task ListBoards_FilterByTeamMode_OnlyTeam()
    {
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P1" }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "T1", Mode = BoardMode.Team }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "T2", Mode = BoardMode.Team }, _caller);

        var results = await _boardService.ListBoardsAsync(_caller, modeFilter: BoardMode.Team);

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(b => b.Mode == BoardMode.Team));
    }

    [TestMethod]
    public async Task ListBoards_NoFilter_ReturnsBothModes()
    {
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P1" }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "T1", Mode = BoardMode.Team }, _caller);

        var results = await _boardService.ListBoardsAsync(_caller);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task ListBoards_FilterByMode_ExcludesArchivedByDefault()
    {
        var board = await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "Archived P" }, _caller);
        var entity = await _db.Boards.FindAsync(board.Id);
        entity!.IsArchived = true;
        await _db.SaveChangesAsync();

        var results = await _boardService.ListBoardsAsync(_caller, modeFilter: BoardMode.Personal);

        Assert.AreEqual(0, results.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Personal Board Blocks Team Operations (Step 23/24 — Mode Guards)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PersonalBoard_SprintPlanning_Throws()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal", Mode = BoardMode.Personal }, _caller);

        var planDto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintPlanningService.CreateYearPlanAsync(board.Id, planDto, _caller));
    }

    [TestMethod]
    public async Task PersonalBoard_ReviewSession_Throws()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal", Mode = BoardMode.Personal }, _caller);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewSessionService.StartSessionAsync(board.Id, _caller));
    }

    [TestMethod]
    public async Task PersonalBoard_EnsureTeamMode_ThrowsWithCorrectCode()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _boardService.EnsureTeamModeAsync(board.Id));

        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Team Board Allows All Operations
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TeamBoard_SprintPlanning_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var planDto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 3,
            DefaultDurationWeeks = 2
        };

        var result = await _sprintPlanningService.CreateYearPlanAsync(board.Id, planDto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Sprints.Count);
    }

    [TestMethod]
    public async Task TeamBoard_ReviewSession_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var session = await _reviewSessionService.StartSessionAsync(board.Id, _caller);

        Assert.IsNotNull(session);
        Assert.AreEqual(board.Id, session.BoardId);
    }

    [TestMethod]
    public async Task TeamBoard_EnsureTeamMode_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        // Should not throw
        await _boardService.EnsureTeamModeAsync(board.Id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Personal Board: Basic Kanban Ops Still Work (Step 24)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PersonalBoard_CreateSwimlane_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        var swimlane = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "To Do" }, _caller);

        Assert.IsNotNull(swimlane);
        Assert.AreEqual("To Do", swimlane.Title);
    }

    [TestMethod]
    public async Task PersonalBoard_CreateCard_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);
        var swimlane = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "Backlog" }, _caller);

        var card = await _cardService.CreateCardAsync(
            swimlane.Id, new CreateCardDto { Title = "My Task" }, _caller);

        Assert.IsNotNull(card);
        Assert.AreEqual("My Task", card.Title);
    }

    [TestMethod]
    public async Task PersonalBoard_MoveCard_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);
        var lane1 = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "To Do" }, _caller);
        var lane2 = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "Done" }, _caller);
        var card = await _cardService.CreateCardAsync(
            lane1.Id, new CreateCardDto { Title = "Task" }, _caller);

        var moved = await _cardService.MoveCardAsync(
            card.Id, new MoveCardDto { TargetSwimlaneId = lane2.Id, Position = 1000 }, _caller);

        Assert.IsNotNull(moved);
        Assert.AreEqual(lane2.Id, moved.SwimlaneId);
    }

    [TestMethod]
    public async Task PersonalBoard_UpdateBoard_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        var updated = await _boardService.UpdateBoardAsync(
            board.Id, new UpdateBoardDto { Title = "Renamed Personal" }, _caller);

        Assert.AreEqual("Renamed Personal", updated.Title);
        Assert.AreEqual(BoardMode.Personal, updated.Mode);
    }

    [TestMethod]
    public async Task PersonalBoard_DeleteBoard_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        await _boardService.DeleteBoardAsync(board.Id, _caller);

        var dbBoard = await _db.Boards.FindAsync(board.Id);
        Assert.IsTrue(dbBoard!.IsDeleted);
    }

    [TestMethod]
    public async Task PersonalBoard_MultipleBoards_AllowedPerUser()
    {
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P1" }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P2" }, _caller);
        await _boardService.CreateBoardAsync(new CreateBoardDto { Title = "P3" }, _caller);

        var results = await _boardService.ListBoardsAsync(_caller);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(b => b.Mode == BoardMode.Personal));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Team Board: Full Kanban + Sprint Operations
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task TeamBoard_CreateSwimlane_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var swimlane = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "In Progress" }, _caller);

        Assert.IsNotNull(swimlane);
    }

    [TestMethod]
    public async Task TeamBoard_CreateCard_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);
        var swimlane = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "Backlog" }, _caller);

        var card = await _cardService.CreateCardAsync(
            swimlane.Id, new CreateCardDto { Title = "Team Task" }, _caller);

        Assert.IsNotNull(card);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Controller-Level Mode Tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Controller_CreateBoard_PersonalMode_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var controller = CreateBoardsController(userId);

        var dto = new CreateBoardDto { Title = "Personal", Mode = BoardMode.Personal };
        var result = await controller.CreateBoardAsync(dto);

        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task Controller_CreateBoard_TeamMode_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var controller = CreateBoardsController(userId);

        var dto = new CreateBoardDto { Title = "Team", Mode = BoardMode.Team };
        var result = await controller.CreateBoardAsync(dto);

        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task Controller_ListBoards_NoFilter_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var (controller, boardService) = CreateBoardsControllerWithService(userId);

        var caller = TestHelpers.CreateCaller(userId);
        await boardService.CreateBoardAsync(new CreateBoardDto { Title = "P1" }, caller);
        await boardService.CreateBoardAsync(new CreateBoardDto { Title = "T1", Mode = BoardMode.Team }, caller);

        var result = await controller.ListBoardsAsync();
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Controller_ListBoards_ModeFilter_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var (controller, boardService) = CreateBoardsControllerWithService(userId);

        var caller = TestHelpers.CreateCaller(userId);
        await boardService.CreateBoardAsync(new CreateBoardDto { Title = "P1" }, caller);
        await boardService.CreateBoardAsync(new CreateBoardDto { Title = "P2" }, caller);
        await boardService.CreateBoardAsync(new CreateBoardDto { Title = "T1", Mode = BoardMode.Team }, caller);

        // Verify service layer filters correctly (controller delegates to service)
        var personalBoards = await boardService.ListBoardsAsync(caller, modeFilter: BoardMode.Personal);
        Assert.AreEqual(2, personalBoards.Count);
        Assert.IsTrue(personalBoards.All(b => b.Mode == BoardMode.Personal));

        var result = await controller.ListBoardsAsync(mode: BoardMode.Personal);
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Controller_GetBoard_AfterCreate_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var (controller, boardService) = CreateBoardsControllerWithService(userId);

        var caller = TestHelpers.CreateCaller(userId);
        var created = await boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, caller);

        var getResult = await controller.GetBoardAsync(created.Id);
        Assert.IsInstanceOfType<OkObjectResult>(getResult);

        // Verify mode through service layer
        var board = await boardService.GetBoardAsync(created.Id, caller);
        Assert.IsNotNull(board);
        Assert.AreEqual(BoardMode.Team, board.Mode);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mode Immutability — Update Board Cannot Change Mode
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task UpdateBoard_DoesNotChangeMode()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        // UpdateBoardDto does not include Mode — mode is immutable after creation
        var updated = await _boardService.UpdateBoardAsync(
            board.Id, new UpdateBoardDto { Title = "New Title" }, _caller);

        Assert.AreEqual(BoardMode.Personal, updated.Mode);
    }

    [TestMethod]
    public async Task UpdateBoard_TeamBoardStaysTeam()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var updated = await _boardService.UpdateBoardAsync(
            board.Id, new UpdateBoardDto { Title = "Updated Team" }, _caller);

        Assert.AreEqual(BoardMode.Team, updated.Mode);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mixed Mode Isolation — Operations Don't Cross-Pollinate
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task MixedBoards_PersonalSwimlaneOps_IndependentOfTeamBoard()
    {
        var personal = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);
        var team = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var personalSwimlane = await _swimlaneService.CreateSwimlaneAsync(
            personal.Id, new CreateBoardSwimlaneDto { Title = "Personal Lane" }, _caller);
        var teamSwimlane = await _swimlaneService.CreateSwimlaneAsync(
            team.Id, new CreateBoardSwimlaneDto { Title = "Team Lane" }, _caller);

        Assert.AreNotEqual(personalSwimlane.Id, teamSwimlane.Id);
        Assert.AreEqual(personal.Id, personalSwimlane.BoardId);
        Assert.AreEqual(team.Id, teamSwimlane.BoardId);
    }

    [TestMethod]
    public async Task MixedBoards_TeamCardWithSprint_PersonalCardWithout()
    {
        var personal = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);
        var team = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var pLane = await _swimlaneService.CreateSwimlaneAsync(
            personal.Id, new CreateBoardSwimlaneDto { Title = "To Do" }, _caller);
        var tLane = await _swimlaneService.CreateSwimlaneAsync(
            team.Id, new CreateBoardSwimlaneDto { Title = "To Do" }, _caller);

        var pCard = await _cardService.CreateCardAsync(
            pLane.Id, new CreateCardDto { Title = "Personal Task" }, _caller);
        var tCard = await _cardService.CreateCardAsync(
            tLane.Id, new CreateCardDto { Title = "Team Task" }, _caller);

        // Personal card has no sprint assigned
        Assert.IsNull(pCard.SprintId);
        Assert.IsNull(pCard.SprintTitle);

        // Team board can have sprints (not auto-assigned, but creation should work)
        Assert.IsNotNull(tCard);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sprint Planning Bulk Creation on Team Board Only
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SprintPlanning_TeamBoard_CreatesMultipleSprints()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);

        var planDto = new CreateSprintPlanDto
        {
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            SprintCount = 6,
            DefaultDurationWeeks = 2
        };

        var plan = await _sprintPlanningService.CreateYearPlanAsync(board.Id, planDto, _caller);

        Assert.IsNotNull(plan);
        Assert.AreEqual(6, plan.Sprints.Count);

        // Verify sprints are sequential
        for (var i = 1; i < plan.Sprints.Count; i++)
        {
            Assert.IsTrue(plan.Sprints[i].StartDate >= plan.Sprints[i - 1].EndDate,
                $"Sprint {i} should start after sprint {i - 1} ends");
        }
    }

    [TestMethod]
    public async Task SprintPlanning_PersonalBoard_AlwaysBlocked()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        var planDto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 1,
            DefaultDurationWeeks = 1
        };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _sprintPlanningService.CreateYearPlanAsync(board.Id, planDto, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Review Session on Personal Board — Always Blocked
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ReviewSession_PersonalBoard_AlwaysBlocked()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _reviewSessionService.StartSessionAsync(board.Id, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TeamModeRequired));
    }

    [TestMethod]
    public async Task ReviewSession_TeamBoard_FullLifecycle()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);
        var swimlane = await _swimlaneService.CreateSwimlaneAsync(
            board.Id, new CreateBoardSwimlaneDto { Title = "In Review" }, _caller);
        var card = await _cardService.CreateCardAsync(
            swimlane.Id, new CreateCardDto { Title = "Review Card" }, _caller);

        // Start session
        var session = await _reviewSessionService.StartSessionAsync(board.Id, _caller);
        Assert.IsNotNull(session);
        Assert.AreEqual(ReviewSessionStatus.Active, session.Status);

        // Set current card
        var updated = await _reviewSessionService.SetCurrentCardAsync(session.Id, card.Id, _caller);
        Assert.AreEqual(card.Id, updated.CurrentCardId);

        // End session
        await _reviewSessionService.EndSessionAsync(session.Id, _caller);

        // Verify session ended by checking it's no longer active
        var state = await _reviewSessionService.GetSessionStateAsync(session.Id, _caller);
        Assert.IsNotNull(state);
        Assert.AreEqual(ReviewSessionStatus.Ended, state.Status);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Board Members on Both Modes
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PersonalBoard_AddMember_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Personal" }, _caller);
        var newUser = Guid.NewGuid();

        var member = await _boardService.AddMemberAsync(board.Id, newUser, BoardMemberRole.Member, _caller);

        Assert.IsNotNull(member);
        Assert.AreEqual(BoardMemberRole.Member, member.Role);
    }

    [TestMethod]
    public async Task TeamBoard_AddMember_Succeeds()
    {
        var board = await _boardService.CreateBoardAsync(
            new CreateBoardDto { Title = "Team", Mode = BoardMode.Team }, _caller);
        var newUser = Guid.NewGuid();

        var member = await _boardService.AddMemberAsync(board.Id, newUser, BoardMemberRole.Admin, _caller);

        Assert.IsNotNull(member);
        Assert.AreEqual(BoardMemberRole.Admin, member.Role);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════

    private BoardsController CreateBoardsController(Guid userId)
    {
        var (controller, _) = CreateBoardsControllerWithService(userId);
        return controller;
    }

    private (BoardsController controller, BoardService boardService) CreateBoardsControllerWithService(Guid userId)
    {
        var db = TestHelpers.CreateDb();
        var eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(db, eventBusMock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(db, eventBusMock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        var labelService = new LabelService(db, boardService, activityService, NullLogger<LabelService>.Instance);

        var controller = new BoardsController(
            boardService,
            activityService,
            labelService,
            teamService,
            NullLogger<BoardsController>.Instance);

        SetupControllerContext(controller, userId);
        return (controller, boardService);
    }

    private static void SetupControllerContext(ControllerBase controller, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "user")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
